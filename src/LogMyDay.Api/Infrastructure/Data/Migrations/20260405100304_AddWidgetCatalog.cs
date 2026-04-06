using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWidgetCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data migration: map old PanelTypeId+AggregationTypeId combinations to new WidgetTypeId values.
            // TimeRangeId column is reused as WidgetTypeId (renamed below), so we update it in-place first.
            migrationBuilder.Sql(
                """
                UPDATE "LogMyDay_DashboardPanels" SET "TimeRangeId" = 1 WHERE "PanelTypeId" = 1 AND "AggregationTypeId" = 1;
                UPDATE "LogMyDay_DashboardPanels" SET "TimeRangeId" = 2 WHERE "PanelTypeId" = 1 AND "AggregationTypeId" = 2;
                UPDATE "LogMyDay_DashboardPanels" SET "TimeRangeId" = 3 WHERE "PanelTypeId" = 7;
                UPDATE "LogMyDay_DashboardPanels" SET "TimeRangeId" = 1 WHERE "TimeRangeId" NOT IN (1, 2, 3);
                """);

            migrationBuilder.DropColumn(
                name: "AggregationTypeId",
                table: "LogMyDay_DashboardPanels");

            migrationBuilder.DropColumn(
                name: "PanelTypeId",
                table: "LogMyDay_DashboardPanels");

            migrationBuilder.RenameColumn(
                name: "TimeRangeId",
                table: "LogMyDay_DashboardPanels",
                newName: "WidgetTypeId");

            migrationBuilder.AddColumn<string>(
                name: "Parameters",
                table: "LogMyDay_DashboardPanels",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Parameters",
                table: "LogMyDay_DashboardPanels");

            migrationBuilder.RenameColumn(
                name: "WidgetTypeId",
                table: "LogMyDay_DashboardPanels",
                newName: "TimeRangeId");

            migrationBuilder.AddColumn<int>(
                name: "AggregationTypeId",
                table: "LogMyDay_DashboardPanels",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PanelTypeId",
                table: "LogMyDay_DashboardPanels",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
