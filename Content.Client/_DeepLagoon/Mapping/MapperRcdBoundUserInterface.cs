using Content.Shared._DeepLagoon.Mapping;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;

namespace Content.Client._DeepLagoon.Mapping;

[UsedImplicitly]
public sealed partial class MapperRcdBoundUserInterface : BoundUserInterface
{
    [Dependency] private IClyde _display = default!;
    [Dependency] private IInputManager _input = default!;

    private MapperRcdMenu? _menu;

    public MapperRcdBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        // Choosing from the menu must not leave a half-painted stroke behind.
        EntMan.System<MapperRcdBrushSystem>().CancelStroke();

        _menu = this.CreateWindow<MapperRcdMenu>();
        _menu.EntrySelected += id => SendMessage(new MapperRcdSelectMessage(id));
        _menu.OpenCenteredAt(_input.MouseScreenPosition.Position / _display.ScreenSize);
    }
}
