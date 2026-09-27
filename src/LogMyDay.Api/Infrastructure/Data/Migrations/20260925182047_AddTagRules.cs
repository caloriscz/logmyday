using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTagRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsComputed",
                table: "LogMyDay_Tags",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RuleId",
                table: "LogMyDay_Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WindowKey",
                table: "LogMyDay_Activities",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LogMyDay_TagRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Template = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetTagId = table.Column<int>(type: "INTEGER", nullable: false),
                    IgnoreZero = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TagRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagRules_LogMyDay_Tags_TargetTagId",
                        column: x => x.TargetTagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_TagRuleSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceTagId = table.Column<int>(type: "INTEGER", nullable: false),
                    Factor = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TagRuleSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagRuleSources_LogMyDay_TagRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "LogMyDay_TagRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagRuleSources_LogMyDay_Tags_SourceTagId",
                        column: x => x.SourceTagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Activities_RuleId_WindowKey",
                table: "LogMyDay_Activities",
                columns: new[] { "RuleId", "WindowKey" },
                unique: true,
                filter: "[RuleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Activities_UserId_TagId_DateStarted",
                table: "LogMyDay_Activities",
                columns: new[] { "UserId", "TagId", "DateStarted" });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagRules_TargetTagId",
                table: "LogMyDay_TagRules",
                column: "TargetTagId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagRules_UserId",
                table: "LogMyDay_TagRules",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagRuleSources_RuleId_SourceTagId",
                table: "LogMyDay_TagRuleSources",
                columns: new[] { "RuleId", "SourceTagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagRuleSources_SourceTagId",
                table: "LogMyDay_TagRuleSources",
                column: "SourceTagId");


            // NOTE: The Activity -> TagRule FK is intentionally NOT added here. On SQLite, adding a
            // foreign key to an existing table forces a full table rebuild (see
            // database-migrations-guide.md). The relationship is configured in LogMyDayDbContext and
            // rule delete removes its generated rows itself; this migration adds columns and indexes.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_TagRuleSources");

            migrationBuilder.DropTable(
                name: "LogMyDay_TagRules");

            migrationBuilder.DropIndex(
                name: "IX_LogMyDay_Activities_RuleId_WindowKey",
                table: "LogMyDay_Activities");

            migrationBuilder.DropIndex(
                name: "IX_LogMyDay_Activities_UserId_TagId_DateStarted",
                table: "LogMyDay_Activities");

            migrationBuilder.DropColumn(
                name: "IsComputed",
                table: "LogMyDay_Tags");

            migrationBuilder.DropColumn(
                name: "RuleId",
                table: "LogMyDay_Activities");

            migrationBuilder.DropColumn(
                name: "WindowKey",
                table: "LogMyDay_Activities");
        }
    }
}
