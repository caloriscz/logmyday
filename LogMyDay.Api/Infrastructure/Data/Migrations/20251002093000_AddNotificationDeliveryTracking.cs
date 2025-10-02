using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDeliveryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastDeliverySentAtUtc",
                table: "Notifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastDeliveryDate",
                table: "Notifications",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveriesOnLastDate",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextEligibleSendAfterUtc",
                table: "Notifications",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveriesOnLastDate",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "LastDeliveryDate",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "LastDeliverySentAtUtc",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "NextEligibleSendAfterUtc",
                table: "Notifications");
        }
    }
}
