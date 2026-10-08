using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Content.Server.Database.Migrations.Sqlite;
[DbContext(typeof(SqliteServerDbContext))]
[Migration("20261008160000_CharacterNotesAndHeadshot")]
public sealed partial class CharacterNotesAndHeadshot : Migration
{
    protected override void Up(MigrationBuilder builder)
    {
        builder.AddColumn<string>("ooc_notes", "profile", type: "TEXT", nullable: false, defaultValue: "");
        builder.AddColumn<string>("headshot_id", "profile", type: "TEXT", nullable: false, defaultValue: "");
    }
    protected override void Down(MigrationBuilder builder)
    {
        builder.DropColumn("ooc_notes", "profile");
        builder.DropColumn("headshot_id", "profile");
    }
}
