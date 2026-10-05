using Microsoft.EntityFrameworkCore.Migrations;
namespace Content.Server.Database.Migrations.Sqlite;
public partial class ChatPanelSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "chat_panel_settings", table: "preference", type: "TEXT", nullable: false, defaultValue: "");
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "chat_panel_settings", table: "preference");
}
