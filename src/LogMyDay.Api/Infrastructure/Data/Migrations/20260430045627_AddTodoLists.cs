using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTodoLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogMyDay_TodoLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CompletionTagId = table.Column<int>(type: "INTEGER", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TodoLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TodoLists_LogMyDay_Tags_CompletionTagId",
                        column: x => x.CompletionTagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_TodoItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ListId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    NotifyAt = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    IsDone = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DoneAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TodoItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TodoItems_LogMyDay_TodoLists_ListId",
                        column: x => x.ListId,
                        principalTable: "LogMyDay_TodoLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TodoItems_ListId",
                table: "LogMyDay_TodoItems",
                column: "ListId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TodoLists_CompletionTagId",
                table: "LogMyDay_TodoLists",
                column: "CompletionTagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TodoLists_UserId",
                table: "LogMyDay_TodoLists",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_TodoItems");

            migrationBuilder.DropTable(
                name: "LogMyDay_TodoLists");
        }
    }
}
