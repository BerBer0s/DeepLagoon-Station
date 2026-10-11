using System.IO;
using Content.Client._DeepLagoon.WebUI;
using Content.Shared._DeepLagoon.Mapping;
using Robust.Client.UserInterface;

namespace Content.Client._DeepLagoon.Mapping;

public sealed partial class GridEditorSystem
{
    [Dependency] private IFileDialogManager _dialogs = default!;
    private Stream? _download;
    private Guid _downloadRequest;
    private int _downloadIndex, _downloadCount;
    private bool _downloadBusy;

    private void BeginDownload(bool wholeMap)
    {
        if (_downloadBusy || _state == null) return;
        _downloadBusy = true;
        DownloadState(true);
        _download = new MemoryStream();
        _downloadRequest = Guid.NewGuid(); _downloadIndex = 0; _downloadCount = 0;
        RaiseNetworkEvent(new GridEditorDownloadEvent(_state.Token, _downloadRequest, wholeMap));
    }

    private void ReceiveFile(GridEditorFileEvent ev)
    {
        if (_download == null || ev.Request != _downloadRequest) return;
        try
        {
            if (ev.Error.Length > 0) throw new IOException(ev.Error);
            if (ev.Count is < 1 or > 512 || ev.Index != _downloadIndex || ev.Bytes.Length > 65536 ||
                _downloadCount != 0 && _downloadCount != ev.Count) throw new InvalidDataException("Неполный файл карты.");
            _downloadCount = ev.Count;
            _download.Write(ev.Bytes); _downloadIndex++;
            if (_downloadIndex != _downloadCount) return;
            var bytes = ((MemoryStream) _download).ToArray();
            _download.Dispose(); _download = null;
            SaveDownloadedFile(bytes, _downloadRequest);
        }
        catch (Exception e) { CancelDownload(); Status($"Скачивание не завершено: {e.Message}", false); }
    }

    private async void SaveDownloadedFile(byte[] bytes, Guid request)
    {
        try
        {
            // Open the destination only after the whole YAML has arrived, so a failed transfer cannot truncate it.
            var file = await _dialogs.SaveFile(new FileDialogFilters(new FileDialogFilters.Group("yml", "yaml")));
            if (file == null) { CancelDownload(); Status("Сохранение файла отменено.", true); return; }
            using var stream = file.Value.fileStream;
            if (_window == null || request != _downloadRequest) return;
            await stream.WriteAsync(bytes); await stream.FlushAsync();
            CancelDownload(); Status("Файл .yml сохранён на вашем компьютере.", true);
        }
        catch (Exception e) { CancelDownload(); Status($"Не удалось сохранить файл: {e.Message}", false); }
    }

    private void DownloadState(bool busy) => _window?.Panel.Web.Send("update", new TguiData().Object("data",
        new TguiData().Bool("downloading", busy)).ToString());
    private void CancelDownload()
    {
        _download?.Dispose(); _download = null; _downloadBusy = false;
        DownloadState(false);
    }
}
