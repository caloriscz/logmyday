using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTagChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogMyDay_TagChecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IfTagId = table.Column<int>(type: "INTEGER", nullable: false),
                    IfOperator = table.Column<int>(type: "INTEGER", nullable: false),
                    IfOperand = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ThenTagId = table.Column<int>(type: "INTEGER", nullable: false),
                    ThenOperator = table.Column<int>(type: "INTEGER", nullable: false),
                    ThenOperand = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TagChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagChecks_LogMyDay_Tags_IfTagId",
                        column: x => x.IfTagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagChecks_LogMyDay_Tags_ThenTagId",
                        column: x => x.ThenTagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagChecks_IfTagId",
                table: "LogMyDay_TagChecks",
                column: "IfTagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagChecks_ThenTagId",
                table: "LogMyDay_TagChecks",
                column: "ThenTagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagChecks_UserId",
                table: "LogMyDay_TagChecks",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_TagChecks");
        }
    }
}
