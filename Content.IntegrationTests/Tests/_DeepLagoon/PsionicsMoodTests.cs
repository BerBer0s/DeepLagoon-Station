using Content.Server.Abilities.Psionics;
using Content.Server.Mood;
using Content.Shared.Abilities.Psionics;
using Content.Shared.Mood;
using Content.Shared.Psionics;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;
using Robust.Shared.Configuration;
using Content.Shared.CCVar;

namespace Content.IntegrationTests.Tests._DeepLagoon;

[TestFixture, NonParallelizable]
public sealed class PsionicsMoodTests
{
    [Test]
    public async Task NoosphericZapCreatesLightningAndParalyzesTarget()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var caster = entities.SpawnEntity("MobHuman", map.GridCoords);
            var target = entities.SpawnEntity("MobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(2, 0)));
            var power = pair.Server.ResolveDependency<IPrototypeManager>().Index<PsionicPowerPrototype>("NoosphericZapPower");
            entities.System<PsionicAbilitiesSystem>().InitializePsionicPower(caster, power, false);
            var action = new Content.Shared.Actions.Events.NoosphericZapPowerActionEvent { Performer = caster, Target = target };
            entities.EventBus.RaiseLocalEvent(caster, action, broadcast: true);
            Assert.That(action.Handled, Is.True);
            var statuses = entities.System<Content.Shared.StatusEffect.StatusEffectsSystem>();
            Assert.That(statuses.HasStatusEffect(target, "Stun"), Is.True);
            Assert.That(statuses.HasStatusEffect(target, "KnockedDown"), Is.True);
            Assert.That(statuses.HasStatusEffect(target, "Stutter"), Is.True);
            var beams = entities.EntityQueryEnumerator<Content.Server.Beam.Components.BeamComponent, MetaDataComponent>();
            var found = false;
            while (beams.MoveNext(out _, out _, out var metadata))
                found |= metadata.EntityPrototype?.ID == "LightningNoospheric";
            Assert.That(found, Is.True, "Casting must create the noospheric lightning beam");
            entities.DeleteEntity(caster);
            entities.DeleteEntity(target);
        });
        await pair.CleanReturnAsync();
    }
    [Test]
    public async Task PsionicObjectsAndAllPowerInitializersWork()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            foreach (var id in new[] { "GlimmerProber", "GlimmerDrain", "GlimmerDeviceFrame",
                         "ComputerPsionicsRecords", "GlimmerMonitorCartridge", "MobGlimmerWisp", "MobGlimmerMite",
                         "MobPsionicFamiliarImp", "MobIfritFamiliar", "MobTelegnosisObserver", "ShadowkinShadow",
                         "AnomalyPyroclastic", "AnomalyGravity", "AnomalyElectricity", "AnomalyFlesh",
                         "AnomalyBluespace", "AnomalyIce", "AnomalyFlora", "AnomalyLiquid", "AnomalyShadow",
                         "AnomalyTech", "AnomalySanta", "MobQuartzCrab", "ReagentSlime", "MobXeno", "MobRevenant",
                         "MobBatRemilia", "MobCorgiCerberus", "MobIfritGuardian", "MobIPC" })
            {
                var uid = entities.SpawnEntity(id, map.GridCoords);
                entities.DeleteEntity(uid);
            }
            var abilities = entities.System<PsionicAbilitiesSystem>();
            foreach (var power in pair.Server.ResolveDependency<IPrototypeManager>().EnumeratePrototypes<PsionicPowerPrototype>())
            {
                var human = entities.SpawnEntity("MobHuman", map.GridCoords);
                abilities.InitializePsionicPower(human, power, false);
                var psionic = entities.GetComponent<PsionicComponent>(human);
                Assert.That(psionic.ActivePowers, Does.Contain(power), power.ID);
                abilities.RemoveAllPsionicPowers(human);
                Assert.That(psionic.ActivePowers, Is.Empty, power.ID);
                Assert.That(psionic.Actions, Is.Empty, power.ID);
                entities.DeleteEntity(human);
            }
        });
        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var factory = pair.Client.ResolveDependency<IComponentFactory>();
            foreach (var (id, state) in new[] { ("MobGlimmerWisp", "willowisp"), ("MobGlimmerMite", "mite"),
                         ("GlimmerProber", "prober"), ("ComputerPsionicsRecords", "registry") })
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.That(prototype.TryGetComponent<Robust.Client.GameObjects.SpriteComponent>(out var sprite, factory), Is.True, id);
                Assert.That(sprite.BaseRSI.TryGetState(state, out _), Is.True, $"{id}: {state}");
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ImportedPrototypesValidate()
    {
        await using var pair = await PoolManager.GetServerClient();
        // Server validation includes chemistry and ability handlers that intentionally do not exist on the client.
        await pair.Server.WaitAssertion(() =>
        {
            var errors = pair.Server.ResolveDependency<IPrototypeManager>()
                .ValidateDirectory(new ResPath("/Prototypes"))
                .Where(file => file.Key.Contains("/_DL/Psionics/"))
                .ToDictionary(file => file.Key, file => file.Value);
            Assert.That(errors, Is.Empty, string.Join("\n", errors.SelectMany(file =>
                file.Value.Select(error => $"{file.Key}: {error.ErrorReason}"))));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MoodReplacesNeedsAndSynchronizesLevel()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            var mood = entities.GetComponent<MoodComponent>(human);
            var neutral = mood.CurrentMoodLevel;
            entities.EventBus.RaiseLocalEvent(human, new MoodEffectEvent("PetAnimal"));
            Assert.That(mood.CurrentMoodLevel, Is.EqualTo(neutral + 3));
            entities.EventBus.RaiseLocalEvent(human, new MoodEffectEvent("PetAnimal"));
            Assert.That(mood.CurrentMoodLevel, Is.EqualTo(neutral + 3), "Repeated moodlets must refresh without stacking");
            entities.EventBus.RaiseLocalEvent(human, new MoodRemoveEffectEvent("PetAnimal"));
            Assert.That(mood.CurrentMoodLevel, Is.EqualTo(neutral));
            entities.EventBus.RaiseLocalEvent(human, new MoodEffectEvent("HealthHeavyDamage"));
            entities.EventBus.RaiseLocalEvent(human, new MoodEffectEvent("HealthNoDamage"));
            Assert.That(mood.CurrentMoodLevel, Is.EqualTo(neutral), "Recovery must replace the injury moodlet");
            Assert.That(entities.GetComponent<NetMoodComponent>(human).CurrentMoodLevel, Is.EqualTo(mood.CurrentMoodLevel));
            var config = pair.Server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(CCVars.MoodEnabled, false);
            Assert.That(entities.HasComponent<NetMoodComponent>(human), Is.False);
            config.SetCVar(CCVars.MoodEnabled, true);
            Assert.That(entities.GetComponent<NetMoodComponent>(human).CurrentMoodLevel, Is.EqualTo(neutral));
            entities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovingAndRegrantingPowerRestoresActions()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            var abilities = entities.System<PsionicAbilitiesSystem>();
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var power = prototypes.Index<PsionicPowerPrototype>("NoosphericZapPower");
            abilities.InitializePsionicPower(human, power, false);
            var psionic = entities.GetComponent<PsionicComponent>(human);
            Assert.That(psionic.ActivePowers, Does.Contain(power));
            Assert.That(psionic.Actions.Count, Is.GreaterThan(0));
            var casting = entities.System<SharedPsionicAbilitiesSystem>();
            entities.AddComponent<PsionicsDisabledComponent>(human);
            Assert.That(casting.OnAttemptPowerUse(human, "Test"), Is.False);
            entities.RemoveComponent<PsionicsDisabledComponent>(human);
            abilities.RemoveAllPsionicPowers(human);
            Assert.That(psionic.ActivePowers, Is.Empty);
            Assert.That(psionic.Actions, Is.Empty);
            Assert.That(psionic.PowerSlotsTaken, Is.Zero);
            abilities.InitializePsionicPower(human, power, false);
            Assert.That(psionic.ActivePowers, Does.Contain(power));
            Assert.That(psionic.Actions.Count, Is.GreaterThan(0));
            entities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EquipmentGrantsActionsAndPreservesInnatePowers()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            var gloves = entities.SpawnEntity("ClothingHandsGlovesColorYellowUnsulated", map.GridCoords);
            var inventory = entities.System<InventorySystem>();
            var power = pair.Server.ResolveDependency<IPrototypeManager>().Index<PsionicPowerPrototype>("NoosphericZapPower");
            var psionic = entities.GetComponent<PsionicComponent>(human);
            Assert.That(inventory.TryEquip(human, gloves, "gloves", force: true), Is.True);
            Assert.That(psionic.ActivePowers, Does.Contain(power));
            Assert.That(psionic.Actions.Count, Is.GreaterThan(0));
            Assert.That(inventory.TryUnequip(human, "gloves", force: true), Is.True);
            Assert.That(psionic.ActivePowers, Does.Not.Contain(power));
            Assert.That(psionic.Actions, Is.Empty);
            entities.System<PsionicAbilitiesSystem>().InitializePsionicPower(human, power, false);
            Assert.That(inventory.TryEquip(human, gloves, "gloves", force: true), Is.True);
            Assert.That(inventory.TryUnequip(human, "gloves", force: true), Is.True);
            Assert.That(psionic.ActivePowers, Does.Contain(power), "Unequipping must preserve a power acquired independently of equipment");
            entities.DeleteEntity(human);
            entities.DeleteEntity(gloves);
        });
        await pair.CleanReturnAsync();
    }
}
