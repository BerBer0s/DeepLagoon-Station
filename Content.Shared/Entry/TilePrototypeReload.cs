using System.Collections.Generic;
using Content.Shared.Maps;
using Robust.Shared.Map;

namespace Content.Shared.Entry;

/// <summary>Restore stable tile IDs after prototype objects are recreated by hot reload.</summary>
public static class TilePrototypeReload
{
    public static List<string> RestoreIds(ITileDefinitionManager registry, IEnumerable<ContentTileDefinition> prototypes)
    {
        var missing = new List<string>();
        foreach (var prototype in prototypes)
        {
            if (registry.TryGetDefinition(prototype.ID, out var registered))
                prototype.AssignTileId(registered.TileId);
            else
                missing.Add(prototype.ID);
        }
        // New names cannot be registered independently: clients and server share numeric IDs.
        return missing;
    }
}
