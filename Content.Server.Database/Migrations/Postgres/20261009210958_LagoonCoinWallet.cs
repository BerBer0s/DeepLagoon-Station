using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class LagoonCoinWallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "lagoon_coin_bonus_ticks",
                table: "preference",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "lagoon_coin_played_ticks",
                table: "preference",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "lagoon_coins",
                table: "preference",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "lagoon_coin_operations",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    played_ticks = table.Column<long>(type: "bigint", nullable: false),
                    subscriber_ticks = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lagoon_coin_operations", x => new { x.user_id, x.operation_id });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lagoon_coin_operations");

            migrationBuilder.DropColumn(
                name: "lagoon_coin_bonus_ticks",
                table: "preference");

            migrationBuilder.DropColumn(
                name: "lagoon_coin_played_ticks",
                table: "preference");

            migrationBuilder.DropColumn(
                name: "lagoon_coins",
                table: "preference");
        }
    }
}
