using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TodoItem_CompletionTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LogMyDay_TodoLists_LogMyDay_Tags_CompletionTagId",
                table: "LogMyDay_TodoLists");

            migrationBuilder.DropIndex(
                name: "IX_LogMyDay_TodoLists_CompletionTagId",
                table: "LogMyDay_TodoLists");

            migrationBuilder.DropColumn(
                name: "CompletionTagId",
                table: "LogMyDay_TodoLists");

            migrationBuilder.AddColumn<int>(
                name: "CompletionTagId",
                table: "LogMyDay_TodoItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TodoItems_CompletionTagId",
                table: "LogMyDay_TodoItems",
                column: "CompletionTagId");

            migrationBuilder.AddForeignKey(
                name: "FK_LogMyDay_TodoItems_LogMyDay_Tags_CompletionTagId",
                table: "LogMyDay_TodoItems",
                column: "CompletionTagId",
                principalTable: "LogMyDay_Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LogMyDay_TodoItems_LogMyDay_Tags_CompletionTagId",
                table: "LogMyDay_TodoItems");

            migrationBuilder.DropIndex(
                name: "IX_LogMyDay_TodoItems_CompletionTagId",
                table: "LogMyDay_TodoItems");

            migrationBuilder.DropColumn(
                name: "CompletionTagId",
                table: "LogMyDay_TodoItems");

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
    }
}
