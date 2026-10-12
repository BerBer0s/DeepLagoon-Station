using System.IO;
using System.Threading.Tasks;
using Content.Shared._DeepLagoon.Tattoos;
using Robust.Client.UserInterface;
using Content.Shared._DeepLagoon.DiscordLink;
using Robust.Client.Player;

namespace Content.Client._DeepLagoon.Tattoos;

public static class TattooFileDialogs
{
    public static bool CanImportExternalPng => TattooImportPolicy.CanImportExternalPng(
        IoCManager.Resolve<IEntityManager>().System<SharedDiscordBoostSystem>()
            .GetSupporterTier(IoCManager.Resolve<IPlayerManager>().LocalSession));
    public static async Task<string> Export(Tattoo tattoo)
    {
        try
        {
            var bytes = TattooPng.Export(tattoo);
            var destination = await IoCManager.Resolve<IFileDialogManager>().SaveFile(new FileDialogFilters(new FileDialogFilters.Group("png")));
            if (destination is not { } output) return "";
            await using var stream = output.fileStream; await stream.WriteAsync(bytes);
            return "Татуировка сохранена в PNG.";
        }
        catch (Exception exception) { return "Не удалось сохранить PNG: " + exception.Message; }
    }

    public static async Task<(Tattoo? Tattoo, string Status)> Import(Tattoo template)
    {
        try
        {
            await using var file = await IoCManager.Resolve<IFileDialogManager>().OpenFile(new FileDialogFilters(new FileDialogFilters.Group("png")), FileAccess.Read);
            if (file == null) return (null, "");
            using var buffer = new MemoryStream(); var block = new byte[8192]; int size;
            while ((size = await file.ReadAsync(block)) > 0)
            {
                if (buffer.Length + size > TattooPng.MaxFileBytes) return (null, "Максимальный размер PNG — 512 КБ.");
                buffer.Write(block, 0, size);
            }
            // Check the current entitlement after file selection, not just when the dialog opened.
            return (TattooPng.Import(buffer.ToArray(), template, CanImportExternalPng), "Татуировка загружена из PNG.");
        }
        catch (Exception exception) { return (null, "Не удалось загрузить PNG: " + exception.Message); }
    }
}
