using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._DeepLagoon.Mapping;

[AdminCommand(AdminFlags.Mapping)]
public sealed class GridEditorCommand : IConsoleCommand
{
    public string Command => "grideditor";
    public string Description => "Открыть редактор grid для маппинга.";
    public string Help => "grideditor [netGridId [x y]] — текущий grid или область 96×96 указанного grid.";
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player || args.Length is not (0 or 1 or 3)) { shell.WriteLine(Help); return; }
        var entities = IoCManager.Resolve<IEntityManager>();
        EntityUid? grid = null;
        if (args.Length > 0 && NetEntity.TryParse(args[0], out var net)) grid = entities.GetEntity(net);
        else if (args.Length == 0 && player.AttachedEntity is { } body && entities.TryGetComponent<TransformComponent>(body, out var transform))
            grid = transform.GridUid;
        int? x = null, y = null;
        if (args.Length == 3)
        {
            if (!int.TryParse(args[1], out var px) || !int.TryParse(args[2], out var py)) { shell.WriteLine(Help); return; }
            x = px; y = py;
        }
        shell.WriteLine(grid is { } uid ? entities.System<GridEditorSystem>().Open(player, uid, x, y) : "Укажите grid или встаньте на него.");
    }
}
