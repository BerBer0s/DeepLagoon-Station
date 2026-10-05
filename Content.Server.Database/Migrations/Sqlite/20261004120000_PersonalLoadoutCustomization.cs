using Microsoft.EntityFrameworkCore.Migrations;
namespace Content.Server.Database.Migrations.Sqlite;
public partial class PersonalLoadoutCustomization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<string>(name: "customization", table: "profile_loadout", type: "TEXT", maxLength: 4096, nullable: true);
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "customization", table: "profile_loadout");
}
