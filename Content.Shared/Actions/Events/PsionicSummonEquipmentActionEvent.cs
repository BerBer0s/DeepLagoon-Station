using Robust.Shared.Prototypes;

namespace Content.Shared.Actions.Events;

public sealed partial class PsionicSummonEquipmentActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EntProtoId Prototype;
}
