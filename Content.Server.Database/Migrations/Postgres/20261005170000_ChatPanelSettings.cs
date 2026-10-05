using Microsoft.EntityFrameworkCore.Migrations;
namespace Content.Server.Database.Migrations.Postgres;
public partial class ChatPanelSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "chat_panel_settings", table: "preference", type: "text", nullable: false, defaultValue: "");
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "chat_panel_settings", table: "preference");
}
