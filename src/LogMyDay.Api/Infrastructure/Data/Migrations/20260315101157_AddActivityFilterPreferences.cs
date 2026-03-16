using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityFilterPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivityDisplayType",
                table: "LogMyDay_Users",
                nullable: false,
                defaultValue: "daily");

            migrationBuilder.AddColumn<string>(
                name: "ActivityPeriodSort",
                table: "LogMyDay_Users",
                nullable: false,
                defaultValue: "desc");

            migrationBuilder.AddColumn<string>(
                name: "ActivitySortOrder",
                table: "LogMyDay_Users",
                nullable: false,
                defaultValue: "desc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivityDisplayType",
                table: "LogMyDay_Users");

            migrationBuilder.DropColumn(
                name: "ActivityPeriodSort",
                table: "LogMyDay_Users");

            migrationBuilder.DropColumn(
                name: "ActivitySortOrder",
                table: "LogMyDay_Users");
        }
    }
}
