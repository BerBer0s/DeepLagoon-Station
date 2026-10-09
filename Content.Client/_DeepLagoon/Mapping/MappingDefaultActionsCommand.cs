using Content.Client.Actions;
using Content.Shared.Administration;
using JetBrains.Annotations;
using Robust.Shared.Console;

namespace Content.Client._DeepLagoon.Mapping;

/// <summary>
/// Loads the preset mapping action bar (<c>/mapping_actions.yml</c>: floors, walls, pipes, airlocks, ...).
/// <c>/mapping</c> no longer does this on its own, since it filled the action bar and menu with 72 entries.
/// </summary>
[UsedImplicitly, AnyCommand]
internal sealed partial class MappingDefaultActionsCommand : LocalizedCommands
{
    [Dependency] private IEntitySystemManager _entitySystemManager = default!;

    public override string Command => "mappingdefaultacts";

    public override string Help => LocalizationManager.GetString($"cmd-{Command}-help", ("command", Command));

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _entitySystemManager.GetEntitySystem<ActionsSystem>().LoadActionAssignments("/mapping_actions.yml", false);
    }
}
