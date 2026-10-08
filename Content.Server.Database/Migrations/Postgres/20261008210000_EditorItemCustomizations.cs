using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Content.Server.Database.Migrations.Postgres;
[DbContext(typeof(PostgresServerDbContext))]
[Migration("20261008210000_EditorItemCustomizations")]
public sealed partial class EditorItemCustomizations : Migration
{
    protected override void Up(MigrationBuilder builder) => builder.AddColumn<string>("customizations", "profile_role_loadout", type: "text", nullable: true);
    protected override void Down(MigrationBuilder builder) => builder.DropColumn("customizations", "profile_role_loadout");
}
