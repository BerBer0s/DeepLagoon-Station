using System.Threading;
using Content.Server.Database;
using Content.Shared.Preferences;
using Robust.Shared.Network;

namespace Content.Server.Preferences.Managers;

public sealed partial class ServerPreferencesManager
{
    private readonly Dictionary<NetUserId, SemaphoreSlim> _chatPanelSaveGates = new();

    private async void HandleChatPanelSettings(MsgChatPanelSettings message)
    {
        var user = message.MsgChannel.UserId;
        var success = false;
        if (!_cachedPlayerPrefs.TryGetValue(user, out var data) || !data.PrefsLoaded || data.Prefs == null ||
            message.Data.Length > MsgChatPanelSettings.MaxLength || !ShouldStorePrefs(message.MsgChannel.AuthType))
        {
            _netManager.ServerSendMessage(new MsgChatPanelSettingsSaved { Revision = message.Revision, Success = false }, message.MsgChannel);
            return;
        }
        if (!_chatPanelSaveGates.TryGetValue(user, out var gate))
            _chatPanelSaveGates[user] = gate = new SemaphoreSlim(1, 1);
        await gate.WaitAsync();
        try
        {
            var settings = ChatPanelSettingsStorage.WithoutImage(message.Data);
            success = await _db.SaveChatPanelSettingsAsync(user, settings);
            if (success && _cachedPlayerPrefs.TryGetValue(user, out var current) && current.Prefs != null)
                current.Prefs.ChatPanelSettings = settings;
        }
        catch (Exception e)
        {
            _sawmill.Error($"Could not save chat settings for {user}: {e}");
        }
        finally
        {
            gate.Release();
        }
        if (message.MsgChannel.IsConnected)
            _netManager.ServerSendMessage(new MsgChatPanelSettingsSaved { Revision = message.Revision, Success = success }, message.MsgChannel);
    }
}
