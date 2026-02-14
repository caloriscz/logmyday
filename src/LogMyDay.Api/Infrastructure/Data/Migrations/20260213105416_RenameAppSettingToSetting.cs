using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameAppSettingToSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename table (preserves data)
            migrationBuilder.RenameTable(
                name: "LogMyDay_AppSettings",
                newName: "LogMyDay_Settings");

            // Rename primary key constraint
            migrationBuilder.RenameIndex(
                name: "PK_LogMyDay_AppSettings",
                table: "LogMyDay_Settings",
                newName: "PK_LogMyDay_Settings");

            // Rename unique index
            migrationBuilder.RenameIndex(
                name: "IX_LogMyDay_AppSettings_Key",
                table: "LogMyDay_Settings",
                newName: "IX_LogMyDay_Settings_Key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rename table back (preserves data)
            migrationBuilder.RenameTable(
                name: "LogMyDay_Settings",
                newName: "LogMyDay_AppSettings");

            // Rename primary key constraint back
            migrationBuilder.RenameIndex(
                name: "PK_LogMyDay_Settings",
                table: "LogMyDay_AppSettings",
                newName: "PK_LogMyDay_AppSettings");

            // Rename unique index back
            migrationBuilder.RenameIndex(
                name: "IX_LogMyDay_Settings_Key",
                table: "LogMyDay_AppSettings",
                newName: "IX_LogMyDay_AppSettings_Key");
        }
    }
}
