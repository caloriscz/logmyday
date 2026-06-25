using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTodoListCompletionTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutoLogMode",
                table: "LogMyDay_TodoLists",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompletionTagId",
                table: "LogMyDay_TodoLists",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TodoLists_CompletionTagId",
                table: "LogMyDay_TodoLists",
                column: "CompletionTagId");

            migrationBuilder.AddForeignKey(
                name: "FK_LogMyDay_TodoLists_LogMyDay_Tags_CompletionTagId",
                table: "LogMyDay_TodoLists",
                column: "CompletionTagId",
                principalTable: "LogMyDay_Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LogMyDay_TodoLists_LogMyDay_Tags_CompletionTagId",
                table: "LogMyDay_TodoLists");

            migrationBuilder.DropIndex(
                name: "IX_LogMyDay_TodoLists_CompletionTagId",
                table: "LogMyDay_TodoLists");

            migrationBuilder.DropColumn(
                name: "AutoLogMode",
                table: "LogMyDay_TodoLists");

            migrationBuilder.DropColumn(
                name: "CompletionTagId",
                table: "LogMyDay_TodoLists");
        }
    }
}
