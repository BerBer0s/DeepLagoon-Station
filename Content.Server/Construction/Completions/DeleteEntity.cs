using Content.Shared.Construction;
using JetBrains.Annotations;

namespace Content.Server.Construction.Completions
{
    public sealed class ConstructionBeforeDeleteEvent : CancellableEntityEventArgs
    {
        public EntityUid? User;

        public ConstructionBeforeDeleteEvent(EntityUid? user)
        {
            User = user;
        }
    }

    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class DeleteEntity : IGraphAction
    {
        public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
        {
            if (entityManager.TryGetComponent<TransformComponent>(uid, out var transform) &&
                entityManager.HasComponent<Content.Shared._DeepLagoon.Apartments.ApartmentProtectedGridComponent>(transform.GridUid))
                return;

            var ev = new ConstructionBeforeDeleteEvent(userUid);
            entityManager.EventBus.RaiseLocalEvent(uid, ev);

            if (!ev.Cancelled)
                entityManager.DeleteEntity(uid);
        }
    }
}
