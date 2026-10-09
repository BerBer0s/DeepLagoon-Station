using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Content.Server.Database.Migrations.Sqlite;
[DbContext(typeof(SqliteServerDbContext))]
[Migration("20261009160000_UnequippedLoadoutSlots")]
public sealed partial class UnequippedLoadoutSlots : Migration
{
    protected override void Up(MigrationBuilder builder) => builder.AddColumn<string>("unequipped_slots", "profile_role_loadout", type: "TEXT", nullable: true);
    protected override void Down(MigrationBuilder builder) => builder.DropColumn("unequipped_slots", "profile_role_loadout");
}
