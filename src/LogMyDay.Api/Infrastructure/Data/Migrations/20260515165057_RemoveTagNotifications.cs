using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTagNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_Notifications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogMyDay_Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TagId = table.Column<int>(type: "INTEGER", nullable: false),
                    NotificationText = table.Column<string>(type: "TEXT", nullable: true),
                    NotBeforeTime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    NotAfterTime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    MaxNudges = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 3),
                    NudgeInterval = table.Column<TimeSpan>(type: "TEXT", nullable: true, defaultValue: new TimeSpan(0, 0, 15, 0, 0)),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    LastDeliveryDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DeliveriesOnLastDate = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    LastDeliverySentAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextEligibleSendAfterUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_Notifications_LogMyDay_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Notifications_TagId",
                table: "LogMyDay_Notifications",
                column: "TagId");
        }
    }
}
