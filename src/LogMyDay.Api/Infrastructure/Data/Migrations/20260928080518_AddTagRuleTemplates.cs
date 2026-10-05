using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTagRuleTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AggregateKind",
                table: "LogMyDay_TagRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LogMyDay_TagRuleCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Operator = table.Column<int>(type: "INTEGER", nullable: false),
                    Operand = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ResultValue = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TagRuleCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagRuleCases_LogMyDay_TagRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "LogMyDay_TagRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagRuleCases_RuleId_SortOrder",
                table: "LogMyDay_TagRuleCases",
                columns: new[] { "RuleId", "SortOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMyDay_TagRuleCases");

            migrationBuilder.DropColumn(
                name: "AggregateKind",
                table: "LogMyDay_TagRules");
        }
    }
}
