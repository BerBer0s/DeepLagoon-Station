using Content.Server.Speech;
using Content.Shared.Containers.ItemSlots;
using Content.Shared._DV.TapeRecorder;
using Content.Shared._DV.TapeRecorder.Components;
using Content.Shared._DV.TapeRecorder.Systems;
using Content.Shared.Preferences.Loadouts;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using System.Linq;
using System.Numerics;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Preferences;

[TestFixture]
public sealed class TapeRecorderPortTests
{
    [Test]
    public async Task CassetteRecordsRewindsPrintsAndLocksWhileActive()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        await server.WaitAssertion(() =>
        {
            var map = new EntityCoordinates(entities.System<SharedMapSystem>().CreateMap(), Vector2.Zero);
            var recorder = entities.SpawnEntity("TapeRecorder", map);
            var cassette = entities.SpawnEntity("CassetteTape", map);
            var speaker = entities.SpawnEntity("MobHuman", map);
            var slots = entities.System<ItemSlotsSystem>();
            var system = entities.System<Content.Server._DV.TapeRecorder.TapeRecorderSystem>();
            Assert.That(slots.TryInsert(recorder, "cassette_tape", cassette, null), Is.True);
            entities.EventBus.RaiseLocalEvent(recorder, new ChangeModeTapeRecorderMessage(TapeRecorderMode.Recording));
            Assert.That(entities.HasComponent<ActiveTapeRecorderComponent>(recorder), Is.True);
            Assert.That(slots.TryEject(recorder, "cassette_tape", null, out _), Is.False);
            entities.EventBus.RaiseLocalEvent(recorder, new ListenEvent("Проверка записи", speaker));
            system.Update(1f);
            var tape = entities.GetComponent<TapeCassetteComponent>(cassette);
            Assert.That(tape.RecordedData.Single().Message, Is.EqualTo("Проверка записи"));
            entities.EventBus.RaiseLocalEvent(recorder, new ChangeModeTapeRecorderMessage(TapeRecorderMode.Rewinding));
            system.Update(1f);
            Assert.That(tape.CurrentPosition, Is.Zero);
            Assert.That(entities.GetComponent<TapeRecorderComponent>(recorder).Mode, Is.EqualTo(TapeRecorderMode.Stopped));
            entities.EventBus.RaiseLocalEvent(recorder, new PrintTapeRecorderMessage());
            Assert.That(entities.EntityQuery<MetaDataComponent>().Any(m => m.EntityPrototype?.ID == "TapeRecorderTranscript"), Is.True);
            Assert.That(slots.TryEject(recorder, "cassette_tape", null, out var ejected), Is.True);
            Assert.That(ejected, Is.EqualTo(cassette));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CatalogueRetainsOnlyApprovedMechanicItems()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        await server.WaitAssertion(() =>
        {
            var items = prototypes.EnumeratePrototypes<LoadoutPrototype>().Where(p => p.ID.StartsWith("DLEE")).SelectMany(p => p.PersonalItems).Select(p => p.Id).ToHashSet();
            Assert.That(items, Does.Contain("TapeRecorder"));
            Assert.That(items, Does.Contain("CassetteTape"));
            foreach (var excluded in new[] { "AACTablet", "TelescopicBaton", "ClothingBeltCorporateJudo", "BSOManual", "Wakizashi", "Terminus" })
                Assert.That(items, Does.Not.Contain(excluded));
        });
        await pair.CleanReturnAsync();
    }
}
