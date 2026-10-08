using System.Linq;
using System.Threading.Tasks;
using Content.Client._DeepLagoon.WebUI;
using Content.Shared._DeepLagoon.Loadouts;
using Content.Shared.CCVar;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using NUnit.Framework;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._DeepLagoon;

[TestFixture, NonParallelizable]
public sealed class PersonalLoadoutCustomizationTests
{
    [Test]
    public async Task EntireCataloguePreviewDoesNotStartGameplayEntities()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true, InLobby = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var count = entities.EntityCount;
            var images = new TguiSpriteImages();
            foreach (var id in prototypes.EnumeratePrototypes<LoadoutPrototype>().SelectMany(p => p.PersonalItems).Distinct())
                _ = images.Item(id.Id).ToArray();
            Assert.That(entities.EntityCount, Is.EqualTo(count));
            Assert.That(images.Item("ClothingUniformJumpsuitSuitBlack").Any(image => image.ToString().Contains("data:image/png;base64,")), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PaintAndTextBelongToSpawnedItemAndDoNotChangeAnotherCharacter()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true, InLobby = true });
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.PersonalLoadoutsEnabled, true);
            var entities = server.ResolveDependency<IEntityManager>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            const string id = "DLEELoadoutClothingJumpsuitSuitBlack";
            var role = new RoleLoadout(PersonalLoadoutSystem.Role);
            var group = prototypes.Index<RoleLoadoutPrototype>(PersonalLoadoutSystem.Role).Groups
                .First(g => prototypes.Index(g).Loadouts.Any(l => l.Id == id));
            role.SelectedLoadouts[group] = new() { new() { Prototype = id, Customization = new()
            { Color = "#1188cc", Name = "Личный комбинезон", Description = "Тестовое описание" } } };
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human").WithLoadout(role);
            var plainRole = role.Clone();
            plainRole.SelectedLoadouts[group][0] = new Loadout { Prototype = id };
            var plainProfile = HumanoidCharacterProfile.DefaultWithSpecies("Human").WithLoadout(plainRole);
            var first = entities.SpawnEntity("MobHuman", Robust.Shared.Map.MapCoordinates.Nullspace);
            var second = entities.SpawnEntity("MobHuman", Robust.Shared.Map.MapCoordinates.Nullspace);
            try
            {
                var equip = entities.System<PersonalLoadoutEquipSystem>();
                equip.Apply(first, profile, "Passenger", null, true);
                equip.Apply(second, plainProfile, "Passenger", null, true);
                var inventory = entities.System<InventorySystem>();
                Assert.That(inventory.TryGetSlotEntity(first, "jumpsuit", out var firstItem), Is.True);
                Assert.That(inventory.TryGetSlotEntity(second, "jumpsuit", out var secondItem), Is.True);
                Assert.That(firstItem, Is.Not.EqualTo(secondItem));
                var metadata = entities.GetComponent<MetaDataComponent>(firstItem.Value);
                Assert.That(metadata.EntityName, Is.EqualTo("Личный комбинезон"));
                Assert.That(metadata.EntityDescription, Is.EqualTo("Тестовое описание"));
                Assert.That(entities.System<SharedAppearanceSystem>().TryGetData<Color>(firstItem.Value, PersonalLoadoutVisuals.Color, out var color), Is.True);
                Assert.That(color, Is.EqualTo(Color.FromHex("#1188cc")));
                Assert.That(entities.HasComponent<PersonalLoadoutVisualsComponent>(secondItem.Value), Is.False);
                Assert.That(entities.GetComponent<MetaDataComponent>(secondItem.Value).EntityName,
                    Is.EqualTo(prototypes.Index<EntityPrototype>("ClothingUniformJumpsuitSuitBlack").Name));
            }
            finally { entities.DeleteEntity(first); entities.DeleteEntity(second); }
        });
        await pair.CleanReturnAsync();
    }
}
