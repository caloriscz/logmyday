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
            migrationBuilder.AddColumn<int>(
                name: "DayCombine",
                table: "LogMyDay_Tags",
                type: "INTEGER",
                nullable: true);

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
                name: "LogMyDay_RecordTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GroupId = table.Column<int>(type: "INTEGER", nullable: true),
                    OncePerDay = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_RecordTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_RecordTypes_LogMyDay_TagGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "LogMyDay_TagGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

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
                        name: "FK_LogMyDay_Records_LogMyDay_RecordTypes_RecordTypeId",
                        column: x => x.RecordTypeId,
                        principalTable: "LogMyDay_RecordTypes",
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

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_RecordTypes_GroupId",
                table: "LogMyDay_RecordTypes",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_RecordTypes_UserId",
                table: "LogMyDay_RecordTypes",
                column: "UserId");

            // NOTE: The Activity -> Record and Tag -> RecordType FKs are intentionally NOT added
            // here. On SQLite, adding a foreign key to an existing table forces a full table
            // rebuild (see database-migrations-guide.md). The relationships are configured in
            // LogMyDayDbContext; RecordService and RecordTypeService remove a record's values and a
            // type's fields themselves. This migration adds columns and indexes.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_Records");

            migrationBuilder.DropTable(
                name: "LogMyDay_RecordTypes");

            migrationBuilder.DropIndex(
                name: "IX_LogMyDay_Tags_RecordTypeId",
                table: "LogMyDay_Tags");

            migrationBuilder.DropIndex(
                name: "IX_LogMyDay_Activities_RecordId",
                table: "LogMyDay_Activities");

            migrationBuilder.DropColumn(
                name: "DayCombine",
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
