using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScanMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogMyDay_ScanMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeValue = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CodeType = table.Column<int>(type: "int", nullable: false),
                    TagId = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DefaultDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_ScanMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_ScanMappings_LogMyDay_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ScanMappings_CodeValue",
                table: "LogMyDay_ScanMappings",
                column: "CodeValue");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ScanMappings_TagId",
                table: "LogMyDay_ScanMappings",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ScanMappings_UserId_CodeValue",
                table: "LogMyDay_ScanMappings",
                columns: new[] { "UserId", "CodeValue" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_ScanMappings");
        }
    }
}
