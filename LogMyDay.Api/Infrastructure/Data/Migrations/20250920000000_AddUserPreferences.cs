using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Culture",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "en-US");

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Europe/Vienna");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Culture",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "Users");
        }
    }
}
