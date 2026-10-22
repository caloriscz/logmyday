using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRecordType",
                table: "LogMyDay_Tags",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RecordFieldOrder",
                table: "LogMyDay_Tags",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecordTypeId",
                table: "LogMyDay_Tags",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecordId",
                table: "LogMyDay_Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LogMyDay_Records",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecordTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    DateStarted = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_Records_LogMyDay_Tags_RecordTypeId",
                        column: x => x.RecordTypeId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Tags_RecordTypeId",
                table: "LogMyDay_Tags",
                column: "RecordTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Activities_RecordId",
                table: "LogMyDay_Activities",
                column: "RecordId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Records_RecordTypeId",
                table: "LogMyDay_Records",
                column: "RecordTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Records_UserId_DateStarted",
                table: "LogMyDay_Records",
                columns: new[] { "UserId", "DateStarted" });

            // NOTE: The Activity -> Record and Tag (field) -> Tag (record type) FKs are
            // intentionally NOT added here. On SQLite, adding a foreign key to an existing table
            // forces a full table rebuild (see database-migrations-guide.md). The relationships
            // are configured in LogMyDayDbContext; RecordService and RecordTypeService remove a
            // record's values and a type's fields themselves. This migration adds columns and
            // indexes.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_Records");

            migrationBuilder.DropIndex(
                name: "IX_LogMyDay_Tags_RecordTypeId",
                table: "LogMyDay_Tags");

            migrationBuilder.DropIndex(
                name: "IX_LogMyDay_Activities_RecordId",
                table: "LogMyDay_Activities");

            migrationBuilder.DropColumn(
                name: "IsRecordType",
                table: "LogMyDay_Tags");

            migrationBuilder.DropColumn(
                name: "RecordFieldOrder",
                table: "LogMyDay_Tags");

            migrationBuilder.DropColumn(
                name: "RecordTypeId",
                table: "LogMyDay_Tags");

            migrationBuilder.DropColumn(
                name: "RecordId",
                table: "LogMyDay_Activities");
        }
    }
}
