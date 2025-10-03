using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TagMetadataEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Quantities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BaseUnitId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quantities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TagOptionLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagOptionLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Units",
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
                    table.PrimaryKey("PK_Units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Units_Quantities_QuantityId",
                        column: x => x.QuantityId,
                        principalTable: "Quantities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TagOptions",
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
                    table.PrimaryKey("PK_TagOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TagOptions_TagOptionLists_OptionListId",
                        column: x => x.OptionListId,
                        principalTable: "TagOptionLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<string>(
                name: "DefaultValue",
                table: "Tags",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaxValue",
                table: "Tags",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MinValue",
                table: "Tags",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OptionListId",
                table: "Tags",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Step",
                table: "Tags",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnitId",
                table: "Tags",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quantities_BaseUnitId",
                table: "Quantities",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_TagOptions_OptionListId",
                table: "TagOptions",
                column: "OptionListId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_OptionListId",
                table: "Tags",
                column: "OptionListId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_UnitId",
                table: "Tags",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Units_QuantityId",
                table: "Units",
                column: "QuantityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quantities_Units_BaseUnitId",
                table: "Quantities",
                column: "BaseUnitId",
                principalTable: "Units",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_TagOptionLists_OptionListId",
                table: "Tags",
                column: "OptionListId",
                principalTable: "TagOptionLists",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_Units_UnitId",
                table: "Tags",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quantities_Units_BaseUnitId",
                table: "Quantities");

            migrationBuilder.DropForeignKey(
                name: "FK_Tags_TagOptionLists_OptionListId",
                table: "Tags");

            migrationBuilder.DropForeignKey(
                name: "FK_Tags_Units_UnitId",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Quantities_BaseUnitId",
                table: "Quantities");

            migrationBuilder.DropIndex(
                name: "IX_TagOptions_OptionListId",
                table: "TagOptions");

            migrationBuilder.DropIndex(
                name: "IX_Tags_OptionListId",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Tags_UnitId",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Units_QuantityId",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "DefaultValue",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "MaxValue",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "MinValue",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "OptionListId",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "Step",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "Tags");

            migrationBuilder.DropTable(
                name: "TagOptions");

            migrationBuilder.DropTable(
                name: "Units");

            migrationBuilder.DropTable(
                name: "TagOptionLists");

            migrationBuilder.DropTable(
                name: "Quantities");
        }
    }
}
