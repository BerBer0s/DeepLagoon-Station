using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class LagoonCoinWebhookDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "balance_after",
                table: "lagoon_coin_operations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "webhook_delivered",
                table: "lagoon_coin_operations",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "webhook_lease_owner",
                table: "lagoon_coin_operations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "webhook_lease_until",
                table: "lagoon_coin_operations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_lagoon_coin_operations_webhook_delivered_webhook_lease_until",
                table: "lagoon_coin_operations",
                columns: new[] { "webhook_delivered", "webhook_lease_until" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lagoon_coin_operations_webhook_delivered_webhook_lease_until",
                table: "lagoon_coin_operations");

            migrationBuilder.DropColumn(
                name: "balance_after",
                table: "lagoon_coin_operations");

            migrationBuilder.DropColumn(
                name: "webhook_delivered",
                table: "lagoon_coin_operations");

            migrationBuilder.DropColumn(
                name: "webhook_lease_owner",
                table: "lagoon_coin_operations");

            migrationBuilder.DropColumn(
                name: "webhook_lease_until",
                table: "lagoon_coin_operations");
        }
    }
}
