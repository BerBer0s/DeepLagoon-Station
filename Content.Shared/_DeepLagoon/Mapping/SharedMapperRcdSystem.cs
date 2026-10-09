using Content.Shared.Examine;
using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Shared._DeepLagoon.Mapping;

/// <summary>
/// Holds the catalog of brush entries built from <see cref="MapperRcdCategoryPrototype"/> and handles
/// the selection made in the radial menu.
/// </summary>
public abstract partial class SharedMapperRcdSystem : EntitySystem
{
    [Dependency] protected IPrototypeManager ProtoManager = default!;

    private readonly Dictionary<string, MapperRcdEntry> _entries = new();

    /// <summary>
    /// Entity prototypes of structure-slot entries. Used to recognise tile occupants that are
    /// replaceable even though their collision layer does not say so (girders).
    /// </summary>
    protected readonly HashSet<string> StructureProtos = new();

    /// <summary>
    /// Entity prototypes of edge-slot entries.
    /// </summary>
    protected readonly HashSet<string> EdgeProtos = new();

    private bool _catalogDirty = true;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<MapperRcdComponent, MapperRcdSelectMessage>(OnSelect);
        SubscribeLocalEvent<MapperRcdComponent, ExaminedEvent>(OnExamined);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<MapperRcdCategoryPrototype>() || args.WasModified<EntityPrototype>())
            _catalogDirty = true;
    }

    private void EnsureCatalog()
    {
        if (!_catalogDirty)
            return;

        _catalogDirty = false;
        _entries.Clear();
        StructureProtos.Clear();
        EdgeProtos.Clear();

        foreach (var category in ProtoManager.EnumeratePrototypes<MapperRcdCategoryPrototype>())
        {
            foreach (var entry in category.Entries)
            {
                var id = entry.EffectiveId;

                if (string.IsNullOrEmpty(id) || !_entries.TryAdd(id, entry))
                {
                    Log.Error($"Mapper RCD category {category.ID} has an entry with a missing or duplicate id '{id}'.");
                    continue;
                }

                if (entry.Mode != MapperRcdMode.Entity || entry.Prototype == null)
                    continue;

                switch (entry.Slot)
                {
                    case MapperRcdSlot.Structure:
                        StructureProtos.Add(entry.Prototype);
                        break;
                    case MapperRcdSlot.Edge:
                        EdgeProtos.Add(entry.Prototype);
                        break;
                }
            }
        }
    }

    public bool TryGetEntry(string id, out MapperRcdEntry entry)
    {
        EnsureCatalog();
        return _entries.TryGetValue(id, out entry!);
    }

    /// <summary>
    /// Entity prototypes that count as full-tile structures for the replacement rule.
    /// </summary>
    protected HashSet<string> GetStructureProtos()
    {
        EnsureCatalog();
        return StructureProtos;
    }

    protected HashSet<string> GetEdgeProtos()
    {
        EnsureCatalog();
        return EdgeProtos;
    }

    public string GetEntryName(MapperRcdEntry entry)
    {
        var name = GetBaseEntryName(entry);

        return entry.Suffix == null
            ? name
            : Loc.GetString("mapper-rcd-name-with-suffix", ("name", name), ("suffix", Loc.GetString(entry.Suffix)));
    }

    private string GetBaseEntryName(MapperRcdEntry entry)
    {
        if (entry.Name != null)
            return Loc.GetString(entry.Name);

        if (entry.Prototype == null)
            return entry.EffectiveId;

        switch (entry.Mode)
        {
            case MapperRcdMode.Entity:
            case MapperRcdMode.Deconstruct:
                if (ProtoManager.TryIndex<EntityPrototype>(entry.Prototype, out var entProto))
                    return entProto.Name;
                break;
            case MapperRcdMode.Tile:
                if (ProtoManager.TryIndex<ContentTileDefinition>(entry.Prototype, out var tileDef))
                    return Loc.GetString(tileDef.Name);
                break;
        }

        return entry.Prototype;
    }

    private void OnSelect(EntityUid uid, MapperRcdComponent comp, MapperRcdSelectMessage args)
    {
        if (!TryGetEntry(args.EntryId, out _))
            return;

        comp.SelectedEntry = args.EntryId;
        Dirty(uid, comp);
    }

    private void OnExamined(Entity<MapperRcdComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.SelectedEntry == null || !TryGetEntry(ent.Comp.SelectedEntry, out var entry))
            return;

        args.PushMarkup(Loc.GetString("mapper-rcd-examine-selected", ("name", GetEntryName(entry))));
    }
}
