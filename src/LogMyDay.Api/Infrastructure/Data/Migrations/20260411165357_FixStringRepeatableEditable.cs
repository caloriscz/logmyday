using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixStringRepeatableEditable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "LogMyDay_InputTypes",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsRepeatableEditable",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "LogMyDay_InputTypes",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsRepeatableEditable",
                value: false);
        }
    }
}
