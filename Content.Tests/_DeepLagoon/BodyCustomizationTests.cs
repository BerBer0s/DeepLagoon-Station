using System;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._DeepLagoon.BodyCustomization;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moq;
using NUnit.Framework;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Network;

namespace Content.Tests._DeepLagoon;

[TestFixture]
public sealed class BodyCustomizationTests
{
    [Test]
    public void EditingOtherAppearanceFieldsPreservesAnatomyAndClonesTheDraft()
    {
        var breast = BodyCustomizationCatalog.Default("breasts");
        breast.Enabled = true; breast.Size = "g";
        breast.SecondaryEnabled = true; breast.SecondaryColor = "#FF3300";
        var appearance = new HumanoidCharacterAppearance { BodyFeatures = [breast] };
        var changed = appearance.WithSkinColor(Robust.Shared.Maths.Color.Blue).WithWidth(1.1f);
        Assert.That(changed.BodyFeatures.Single().ContentEquals(breast), Is.True);
        changed.BodyFeatures[0].Size = "a";
        Assert.That(appearance.BodyFeatures.Single().Size, Is.EqualTo("g"));
        Assert.That(appearance.MemberwiseEquals(changed), Is.False);
    }

    [Test]
    public void ImportedProfilesCannotSelectResourcePathsOrInvalidScales()
    {
        var sanitized = BodyCustomizationCatalog.Sanitize([
            new() { Part = "penis", Shape = "../../other", Size = "bad", Scale = float.NaN, Color = "bad", Visibility = 900 },
            new() { Part = "penis", Enabled = true }, new() { Part = "unknown" },
            new() { Part = "womb", Shape = "external", Size = "missing", Scale = float.PositiveInfinity },
        ]);
        Assert.That(sanitized, Has.Count.EqualTo(2));
        var external = sanitized.Single(f => f.Part == "penis");
        Assert.That(BodyCustomizationCatalog.Parts["penis"][external.Shape].ContainsKey(external.Size), Is.True);
        Assert.That(external.Scale, Is.EqualTo(1));
        Assert.That(external.Visibility, Is.EqualTo(2));
        Assert.That(sanitized.Single(f => f.Part == "womb").Shape, Is.EqualTo("internal"));
    }

    [Test]
    public async Task ExistingSqliteDatabaseUpgradesAndSavesAllEightParts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SqliteServerDbContext>().UseSqlite(connection).Options;
        var user = new NetUserId(Guid.NewGuid());
        await using (var context = new SqliteServerDbContext(options))
        {
            await context.GetService<IMigrator>().MigrateAsync("20261012090000_DeepLagoonTattoos");
            context.Preference.Add(new Preference { UserId = user.UserId, AdminOOCColor = "#FFFFFF" });
            await context.SaveChangesAsync();
            await context.Database.MigrateAsync();
            Assert.That(context.Database.HasPendingModelChanges(), Is.False);
        }
        var database = new ServerDbSqlite(() => options, true, Mock.Of<IConfigurationManager>(), true, Mock.Of<ISawmill>());
        var features = BodyCustomizationCatalog.Order.Select(BodyCustomizationCatalog.Default).ToList();
        foreach (var feature in features) feature.Enabled = true;
        var breast = features.Single(f => f.Part == "breasts");
        breast.SecondaryEnabled = true; breast.SecondaryColor = "#4466FF";
        var profile = new HumanoidCharacterProfile().WithCharacterAppearance(new HumanoidCharacterAppearance { BodyFeatures = features });
        await database.SaveCharacterSlotAsync(user, profile, 0);
        var loaded = await database.GetPlayerPreferencesAsync(user);
        var saved = (HumanoidCharacterProfile) loaded!.Characters[0];
        Assert.That(BodyCustomizationCatalog.ContentEquals(saved.Appearance.BodyFeatures, features), Is.True);
        Assert.That(saved.Appearance.BodyFeatures, Has.Count.EqualTo(8));
    }

    [Test]
    public void PostgresMigrationMatchesModelAndOnlyAddsCosmeticData()
    {
        var options = new DbContextOptionsBuilder<PostgresServerDbContext>().UseNpgsql("Host=localhost;Database=unused;Username=unused").Options;
        using var context = new PostgresServerDbContext(options);
        Assert.That(context.Database.HasPendingModelChanges(), Is.False);
        var sql = context.GetService<IMigrator>().GenerateScript("20261012090000_DeepLagoonTattoos", "20261012120000_DeepLagoonBodyCustomization");
        Assert.That(sql, Does.Contain("ADD body_features_json text"));
        Assert.That(sql, Does.Not.Contain("DROP TABLE"));
    }
}
