using Microsoft.EntityFrameworkCore.Migrations;

namespace Content.Server.Database.Migrations.Postgres;

public partial class DeepLagoonBodyCustomization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "body_features_json", table: "profile", type: "text", nullable: false, defaultValue: "[]");
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "body_features_json", table: "profile");
}
