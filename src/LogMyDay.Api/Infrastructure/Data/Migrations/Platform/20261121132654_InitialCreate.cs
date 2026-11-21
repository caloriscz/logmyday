using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations.Platform
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogMyDay_Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Settings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    IsAdmin = table.Column<bool>(type: "INTEGER", nullable: false),
                    Culture = table.Column<string>(type: "TEXT", nullable: false),
                    TimeZone = table.Column<string>(type: "TEXT", nullable: false),
                    ActivityDisplayType = table.Column<string>(type: "TEXT", nullable: false),
                    ActivitySortOrder = table.Column<string>(type: "TEXT", nullable: false),
                    ActivityPeriodSort = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_ApiKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastUsedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_ApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_ApiKeys_LogMyDay_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "LogMyDay_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_PasswordResets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Token = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_PasswordResets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_PasswordResets_LogMyDay_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "LogMyDay_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ApiKeys_Prefix",
                table: "LogMyDay_ApiKeys",
                column: "Prefix");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ApiKeys_UserId",
                table: "LogMyDay_ApiKeys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_PasswordResets_Token",
                table: "LogMyDay_PasswordResets",
                column: "Token");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_PasswordResets_UserId",
                table: "LogMyDay_PasswordResets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Settings_Key",
                table: "LogMyDay_Settings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Users_Email",
                table: "LogMyDay_Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_ApiKeys");

            migrationBuilder.DropTable(
                name: "LogMyDay_PasswordResets");

            migrationBuilder.DropTable(
                name: "LogMyDay_Settings");

            migrationBuilder.DropTable(
                name: "LogMyDay_Users");
        }
    }
}
