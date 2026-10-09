using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._DeepLagoon.Mapping;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._DeepLagoon.Mapping;

/// <summary>
/// Radial menu of the mapper RCD. Every <see cref="MapperRcdCategoryPrototype"/> is one layer of the menu;
/// a category button moves to the layer of that category, an entry button selects the entry and closes the menu.
/// </summary>
public sealed partial class MapperRcdMenu : RadialMenu
{
    private const float ButtonSize = 64f;
    private const float IconScale = 2f;

    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;

    private readonly MapperRcdSystem _catalog;
    private readonly SpriteSystem _sprites;

    public event Action<string>? EntrySelected;

    public MapperRcdMenu()
    {
        IoCManager.InjectDependencies(this);
        _catalog = _entManager.System<MapperRcdSystem>();
        _sprites = _entManager.System<SpriteSystem>();

        BackButtonStyleClass = "RadialMenuBackButton";
        CloseButtonStyleClass = "RadialMenuCloseButton";
        VerticalExpand = true;
        HorizontalExpand = true;
        MinSize = new Vector2(450, 450);

        Build();
    }

    private void Build()
    {
        var children = new Dictionary<string, List<MapperRcdCategoryPrototype>>();
        MapperRcdCategoryPrototype? root = null;

        foreach (var category in _protoManager.EnumeratePrototypes<MapperRcdCategoryPrototype>())
        {
            if (category.Parent is not { } parent)
            {
                if (root == null || string.CompareOrdinal(category.ID, root.ID) < 0)
                    root = category;

                continue;
            }

            if (!children.TryGetValue(parent, out var list))
                children[parent] = list = new List<MapperRcdCategoryPrototype>();

            list.Add(category);
        }

        if (root == null)
            return;

        foreach (var list in children.Values)
        {
            list.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.ID, b.ID));
        }

        AddLayer(root, children);
    }

    private void AddLayer(MapperRcdCategoryPrototype category, Dictionary<string, List<MapperRcdCategoryPrototype>> children)
    {
        var layer = new RadialContainer
        {
            Name = category.ID,
            VerticalExpand = true,
            HorizontalExpand = true,
            InitialRadius = 100f,
            ReserveSpaceForHiddenChildren = false,
        };

        AddChild(layer);

        children.TryGetValue(category.ID, out var subCategories);

        if (subCategories != null)
        {
            foreach (var sub in subCategories)
            {
                var button = new RadialMenuTextureButtonWithSector
                {
                    SetSize = new Vector2(ButtonSize, ButtonSize),
                    ToolTip = Loc.GetString(sub.Name),
                    TargetLayer = sub.ID,
                };

                AddIcon(button, GetCategoryIcon(sub, children));
                layer.AddChild(button);
            }
        }

        foreach (var entry in category.Entries)
        {
            var id = entry.EffectiveId;
            var button = new MapperRcdMenuButton
            {
                SetSize = new Vector2(ButtonSize, ButtonSize),
                ToolTip = GetEntryTooltip(entry),
                EntryId = id,
            };

            button.OnButtonUp += _ =>
            {
                EntrySelected?.Invoke(button.EntryId);
                Close();
            };

            AddIcon(button, GetEntryIcon(entry));
            layer.AddChild(button);
        }

        if (subCategories == null)
            return;

        foreach (var sub in subCategories)
        {
            AddLayer(sub, children);
        }
    }

    private string GetEntryTooltip(MapperRcdEntry entry)
    {
        var name = _catalog.GetEntryName(entry);

        // Siblings often share a name (airlocks, wasteland windows), the id tells them apart.
        return entry.Mode == MapperRcdMode.Deconstruct
            ? name
            : Loc.GetString("mapper-rcd-tooltip", ("name", name), ("id", entry.EffectiveId));
    }

    private void AddIcon(Control button, Texture? texture)
    {
        if (texture == null)
            return;

        button.AddChild(new TextureRect
        {
            VerticalAlignment = VAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
            Texture = texture,
            TextureScale = new Vector2(IconScale, IconScale),
        });
    }

    private Texture GetIcon(SpriteSpecifier icon)
    {
        var texture = _sprites.Frame0(icon);

        // A tile texture is a strip of square variants side by side; show the first one only.
        return icon is SpriteSpecifier.Texture && texture.Width > texture.Height
            ? new AtlasTexture(texture, UIBox2.FromDimensions(Vector2.Zero, new Vector2(texture.Height, texture.Height)))
            : texture;
    }

    private Texture? GetEntryIcon(MapperRcdEntry entry)
    {
        if (entry.Icon != null)
            return GetIcon(entry.Icon);

        if (entry.Mode != MapperRcdMode.Tile && entry.Prototype != null &&
            _protoManager.TryIndex<EntityPrototype>(entry.Prototype, out var proto))
        {
            return _sprites.Frame0(proto);
        }

        return null;
    }

    private Texture? GetCategoryIcon(MapperRcdCategoryPrototype category, Dictionary<string, List<MapperRcdCategoryPrototype>> children)
    {
        if (category.Icon != null)
            return GetIcon(category.Icon);

        foreach (var entry in category.Entries)
        {
            var icon = GetEntryIcon(entry);
            if (icon != null)
                return icon;
        }

        if (!children.TryGetValue(category.ID, out var subCategories))
            return null;

        foreach (var sub in subCategories)
        {
            var icon = GetCategoryIcon(sub, children);
            if (icon != null)
                return icon;
        }

        return null;
    }
}

public sealed class MapperRcdMenuButton : RadialMenuTextureButtonWithSector
{
    public string EntryId = string.Empty;
}
