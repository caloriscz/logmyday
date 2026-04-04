using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogMyDay_Dashboards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Dashboards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_DashboardPanels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DashboardId = table.Column<int>(type: "INTEGER", nullable: false),
                    PanelTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagId = table.Column<int>(type: "INTEGER", nullable: true),
                    AggregationTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeRangeId = table.Column<int>(type: "INTEGER", nullable: false),
                    SizeId = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_DashboardPanels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_DashboardPanels_LogMyDay_Dashboards_DashboardId",
                        column: x => x.DashboardId,
                        principalTable: "LogMyDay_Dashboards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LogMyDay_DashboardPanels_LogMyDay_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_DashboardPanels_DashboardId",
                table: "LogMyDay_DashboardPanels",
                column: "DashboardId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_DashboardPanels_TagId",
                table: "LogMyDay_DashboardPanels",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Dashboards_UserId",
                table: "LogMyDay_Dashboards",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_DashboardPanels");

            migrationBuilder.DropTable(
                name: "LogMyDay_Dashboards");
        }
    }
}
