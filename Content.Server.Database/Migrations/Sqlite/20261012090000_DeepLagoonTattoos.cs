using Microsoft.EntityFrameworkCore.Migrations;

namespace Content.Server.Database.Migrations.Sqlite;

public partial class DeepLagoonTattoos : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "tattoos_json", table: "profile", type: "TEXT", nullable: false, defaultValue: "[]");
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "tattoos_json", table: "profile");
}
