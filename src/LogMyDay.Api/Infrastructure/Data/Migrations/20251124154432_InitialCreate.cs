using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var dateCreatedSql = ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite"
                ? "CURRENT_TIMESTAMP"
                : "GETUTCDATE()";

            migrationBuilder.CreateTable(
                name: "LogMyDay_InputTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_InputTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_Patterns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatternValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Patterns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_TagOptionLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TagOptionLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsAdmin = table.Column<bool>(type: "bit", nullable: false),
                    Culture = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_TagOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OptionListId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TagOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagOptions_LogMyDay_TagOptionLists_OptionListId",
                        column: x => x.OptionListId,
                        principalTable: "LogMyDay_TagOptionLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_PasswordResets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "LogMyDay_Activities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateStarted = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateFinished = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TagId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Activities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TagId = table.Column<int>(type: "int", nullable: false),
                    NotificationText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NotBeforeTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    NotAfterTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    MaxNudges = table.Column<int>(type: "int", nullable: false, defaultValue: 3),
                    NudgeInterval = table.Column<TimeSpan>(type: "time", nullable: true, defaultValue: new TimeSpan(0, 0, 15, 0, 0)),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: dateCreatedSql),
                    LastDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DeliveriesOnLastDate = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastDeliverySentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextEligibleSendAfterUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_Quantities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BaseUnitId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Quantities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_Units",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuantityId = table.Column<int>(type: "int", nullable: false),
                    AToBase = table.Column<double>(type: "float", nullable: false),
                    BToBase = table.Column<double>(type: "float", nullable: false),
                    Decimals = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_Units_LogMyDay_Quantities_QuantityId",
                        column: x => x.QuantityId,
                        principalTable: "LogMyDay_Quantities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TagName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InputTypeId = table.Column<int>(type: "int", nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    TimeGranularity = table.Column<int>(type: "int", nullable: false),
                    IsRepeatable = table.Column<bool>(type: "bit", nullable: false),
                    IsRange = table.Column<bool>(type: "bit", nullable: false),
                    PatternId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnitId = table.Column<int>(type: "int", nullable: true),
                    MinValue = table.Column<double>(type: "float", nullable: true),
                    MaxValue = table.Column<double>(type: "float", nullable: true),
                    Step = table.Column<double>(type: "float", nullable: true),
                    DefaultValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OptionListId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_Tags_LogMyDay_InputTypes_InputTypeId",
                        column: x => x.InputTypeId,
                        principalTable: "LogMyDay_InputTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LogMyDay_Tags_LogMyDay_Patterns_PatternId",
                        column: x => x.PatternId,
                        principalTable: "LogMyDay_Patterns",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LogMyDay_Tags_LogMyDay_TagOptionLists_OptionListId",
                        column: x => x.OptionListId,
                        principalTable: "LogMyDay_TagOptionLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LogMyDay_Tags_LogMyDay_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "LogMyDay_Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "LogMyDay_InputTypes",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Integer" },
                    { 2, "String" },
                    { 3, "Boolean" },
                    { 4, "Date" },
                    { 5, "Time" },
                    { 6, "Decimal, precision 2" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Activities_TagId",
                table: "LogMyDay_Activities",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Notifications_TagId",
                table: "LogMyDay_Notifications",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_PasswordResets_Token",
                table: "LogMyDay_PasswordResets",
                column: "Token");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_PasswordResets_UserId",
                table: "LogMyDay_PasswordResets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Quantities_BaseUnitId",
                table: "LogMyDay_Quantities",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagOptions_OptionListId",
                table: "LogMyDay_TagOptions",
                column: "OptionListId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Tags_InputTypeId",
                table: "LogMyDay_Tags",
                column: "InputTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Tags_OptionListId",
                table: "LogMyDay_Tags",
                column: "OptionListId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Tags_PatternId",
                table: "LogMyDay_Tags",
                column: "PatternId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Tags_UnitId",
                table: "LogMyDay_Tags",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Units_QuantityId",
                table: "LogMyDay_Units",
                column: "QuantityId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Users_Email",
                table: "LogMyDay_Users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LogMyDay_Activities_LogMyDay_Tags_TagId",
                table: "LogMyDay_Activities",
                column: "TagId",
                principalTable: "LogMyDay_Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LogMyDay_Notifications_LogMyDay_Tags_TagId",
                table: "LogMyDay_Notifications",
                column: "TagId",
                principalTable: "LogMyDay_Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LogMyDay_Quantities_LogMyDay_Units_BaseUnitId",
                table: "LogMyDay_Quantities",
                column: "BaseUnitId",
                principalTable: "LogMyDay_Units",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LogMyDay_Quantities_LogMyDay_Units_BaseUnitId",
                table: "LogMyDay_Quantities");

            migrationBuilder.DropTable(
                name: "LogMyDay_Activities");

            migrationBuilder.DropTable(
                name: "LogMyDay_Notifications");

            migrationBuilder.DropTable(
                name: "LogMyDay_PasswordResets");

            migrationBuilder.DropTable(
                name: "LogMyDay_TagOptions");

            migrationBuilder.DropTable(
                name: "LogMyDay_Tags");

            migrationBuilder.DropTable(
                name: "LogMyDay_Users");

            migrationBuilder.DropTable(
                name: "LogMyDay_InputTypes");

            migrationBuilder.DropTable(
                name: "LogMyDay_Patterns");

            migrationBuilder.DropTable(
                name: "LogMyDay_TagOptionLists");

            migrationBuilder.DropTable(
                name: "LogMyDay_Units");

            migrationBuilder.DropTable(
                name: "LogMyDay_Quantities");
        }
    }
}
