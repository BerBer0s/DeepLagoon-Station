using System;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
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

namespace Content.Tests.Server;

[TestFixture]
public sealed class ChatPanelDatabaseTest
{
    [TestCase(true)]
    [TestCase(false)]
    public void MigrationHistoryRetainsDeployedDeepLagoonIdentifiers(bool postgres)
    {
        using DbContext context = postgres
            ? new PostgresServerDbContext(new DbContextOptionsBuilder<PostgresServerDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused").Options)
            : new SqliteServerDbContext(new DbContextOptionsBuilder<SqliteServerDbContext>()
                .UseSqlite("Data Source=:memory:").Options);
        var migrations = context.Database.GetMigrations().ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(migrations, Does.Contain("20261002120000_DeepLagoonSocialPreferences"));
            Assert.That(migrations, Does.Contain("20261002190000_DeepLagoonSocialLibrary"));
            Assert.That(migrations, Does.Not.Contain("20261002120000_DeepLagoonInteractionPanelPreferences"));
            Assert.That(migrations, Does.Not.Contain("20261002190000_DeepLagoonInteractionPanelLibrary"));
        });
    }

    [Test]
    public async Task SqliteMigrationAndAccountScopedSavePreserveOtherPreferences()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SqliteServerDbContext>().UseSqlite(connection).Options;
        var user = new NetUserId(Guid.NewGuid());
        var other = new NetUserId(Guid.NewGuid());
        await using (var context = new SqliteServerDbContext(options))
        {
            var previous = context.Database.GetMigrations().TakeWhile(id => id != "20261005170000_ChatPanelSettings").Last();
            await context.GetService<IMigrator>().MigrateAsync(previous);
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO preference (user_id, selected_character_slot, admin_ooc_color, mono_coins) VALUES ({user.UserId}, 0, '#FF0000', 42)");
            await context.Database.MigrateAsync();
            Assert.That(context.Database.HasPendingModelChanges(), Is.False);
            var existing = await context.Preference.SingleAsync();
            Assert.That(existing.ChatPanelSettings, Is.Empty);
            context.Preference.Add(new Preference { UserId = other.UserId, AdminOOCColor = "#00FF00" });
            await context.SaveChangesAsync();
        }
        var database = new ServerDbSqlite(() => options, true, Mock.Of<IConfigurationManager>(), true, Mock.Of<ISawmill>());
        const string payload = "Tabs:\n- Name: Важное\nAppearance:\n  FontSize: 20";
        Assert.That(await database.SaveChatPanelSettingsAsync(user, payload), Is.True);
        await database.SaveCharacterSlotAsync(user, new HumanoidCharacterProfile(), 0);
        Assert.That((await database.GetPlayerPreferencesAsync(user))!.ChatPanelSettings, Is.EqualTo(payload));
        Assert.That(await database.SaveChatPanelSettingsAsync(new NetUserId(Guid.NewGuid()), "missing"), Is.False);
        await using var read = new SqliteServerDbContext(options);
        var saved = await read.Preference.SingleAsync(p => p.UserId == user.UserId);
        Assert.Multiple(() =>
        {
            Assert.That(saved.ChatPanelSettings, Is.EqualTo(payload));
            Assert.That(saved.MonoCoins, Is.EqualTo(42));
            Assert.That(saved.AdminOOCColor, Is.EqualTo("#FF0000"));
        });
        Assert.That((await read.Preference.SingleAsync(p => p.UserId == other.UserId)).ChatPanelSettings, Is.Empty);
        const string legacy = "Tabs:\n- Name: Важное\nAppearance:\n  FontSize: 20\n  Image: aGVsbG8=";
        Assert.That(await database.SaveChatPanelSettingsAsync(user, legacy), Is.True);
        var loaded = await database.GetPlayerPreferencesAsync(user);
        Assert.That(loaded!.ChatPanelSettings, Does.Not.Contain("aGVsbG8="));
        Assert.That(loaded.ChatPanelSettings, Does.Contain("FontSize: 20"));
    }

    [Test]
    public void PostgresModelAndAdditiveMigrationAreConsistent()
    {
        var options = new DbContextOptionsBuilder<PostgresServerDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options;
        using var context = new PostgresServerDbContext(options);
        Assert.That(context.Database.HasPendingModelChanges(), Is.False);
        var previous = context.Database.GetMigrations().TakeWhile(id => id != "20261005170000_ChatPanelSettings").Last();
        var sql = context.GetService<IMigrator>().GenerateScript(previous, "20261005170000_ChatPanelSettings");
        Assert.That(sql, Does.Contain("ADD chat_panel_settings text NOT NULL DEFAULT ''"));
        Assert.That(sql, Does.Not.Contain("DROP TABLE"));
    }
}
