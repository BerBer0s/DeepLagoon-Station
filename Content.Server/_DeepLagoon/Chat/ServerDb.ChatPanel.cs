using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Network;

namespace Content.Server.Database;

public partial interface IServerDbManager
{
    Task<bool> SaveChatPanelSettingsAsync(NetUserId user, string settings);
}

public sealed partial class ServerDbManager
{
    public Task<bool> SaveChatPanelSettingsAsync(NetUserId user, string settings) => RunDbCommand(() => _db.SaveChatPanelSettingsAsync(user, settings));
}

public abstract partial class ServerDbBase
{
    public async Task<bool> SaveChatPanelSettingsAsync(NetUserId user, string settings)
    {
        settings = ChatPanelSettingsStorage.WithoutImage(settings);
        await using var db = await GetDb();
        return await db.DbContext.Preference.Where(p => p.UserId == user.UserId)
            .ExecuteUpdateAsync(update => update.SetProperty(p => p.ChatPanelSettings, settings)) == 1;
    }
}
