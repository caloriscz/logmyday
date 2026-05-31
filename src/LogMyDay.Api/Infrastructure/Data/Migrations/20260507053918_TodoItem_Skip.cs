using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TodoItem_Skip : Migration
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

            migrationBuilder.AddColumn<DateTime>(
                name: "SkippedAt",
                table: "LogMyDay_TodoItems",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkippedAt",
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
