using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InputTypeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tags_InputTypes_TypeId",
                table: "Tags");

            migrationBuilder.RenameColumn(
                name: "TypeId",
                table: "Tags",
                newName: "InputTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Tags_TypeId",
                table: "Tags",
                newName: "IX_Tags_InputTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_InputTypes_InputTypeId",
                table: "Tags",
                column: "InputTypeId",
                principalTable: "InputTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tags_InputTypes_InputTypeId",
                table: "Tags");

            migrationBuilder.RenameColumn(
                name: "InputTypeId",
                table: "Tags",
                newName: "TypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Tags_InputTypeId",
                table: "Tags",
                newName: "IX_Tags_TypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_InputTypes_TypeId",
                table: "Tags",
                column: "TypeId",
                principalTable: "InputTypes",
                principalColumn: "Id");
        }
    }
}
