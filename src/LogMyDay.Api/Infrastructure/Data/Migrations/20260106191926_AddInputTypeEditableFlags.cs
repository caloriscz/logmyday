using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInputTypeEditableFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new columns to InputTypes table
            migrationBuilder.AddColumn<bool>(
                name: "IsRangeEditable",
                table: "LogMyDay_InputTypes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMinimumEditable",
                table: "LogMyDay_InputTypes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMaximumEditable",
                table: "LogMyDay_InputTypes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsStepEditable",
                table: "LogMyDay_InputTypes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRepeatableEditable",
                table: "LogMyDay_InputTypes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "LogMyDay_InputTypes",
                type: "nvarchar(max)",
                nullable: true);

            // Update existing InputType seed data with correct values

            // Integer (Id=1) - all editable
            migrationBuilder.UpdateData(
                table: "LogMyDay_InputTypes",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "IsRangeEditable", "IsMinimumEditable", "IsMaximumEditable", "IsStepEditable", "IsRepeatableEditable" },
                values: new object[] { "Whole number input (e.g., 1, 42, -5)", true, true, true, true, true });

            // String (Id=2) - limited editing
            migrationBuilder.UpdateData(
                table: "LogMyDay_InputTypes",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "IsRangeEditable", "IsMinimumEditable", "IsMaximumEditable", "IsStepEditable", "IsRepeatableEditable" },
                values: new object[] { "Free-form text input", true, false, false, false, false });

            // Boolean (Id=3) - limited editing
            migrationBuilder.UpdateData(
                table: "LogMyDay_InputTypes",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "IsRangeEditable", "IsMinimumEditable", "IsMaximumEditable", "IsStepEditable", "IsRepeatableEditable" },
                values: new object[] { "True/false checkbox input", true, false, false, false, false });

            // Date (Id=4) - limited editing
            migrationBuilder.UpdateData(
                table: "LogMyDay_InputTypes",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "IsRangeEditable", "IsMinimumEditable", "IsMaximumEditable", "IsStepEditable", "IsRepeatableEditable" },
                values: new object[] { "Date selection input", true, false, false, false, false });

            // Time (Id=5) - limited editing
            migrationBuilder.UpdateData(
                table: "LogMyDay_InputTypes",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Description", "IsRangeEditable", "IsMinimumEditable", "IsMaximumEditable", "IsStepEditable", "IsRepeatableEditable" },
                values: new object[] { "Time selection input", true, false, false, false, false });

            // Decimal (Id=6) - all editable
            migrationBuilder.UpdateData(
                table: "LogMyDay_InputTypes",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Description", "IsRangeEditable", "IsMinimumEditable", "IsMaximumEditable", "IsStepEditable", "IsRepeatableEditable" },
                values: new object[] { "Decimal number with 2 decimal places (e.g., 3.14, 99.99)", true, true, true, true, true });

            // Insert new InputTypes (7, 8, 9)
            migrationBuilder.InsertData(
                table: "LogMyDay_InputTypes",
                columns: new[] { "Id", "Name", "Description", "IsRangeEditable", "IsMinimumEditable", "IsMaximumEditable", "IsStepEditable", "IsRepeatableEditable" },
                values: new object[] { 7, "Rating 1-5", "Rating scale from 1 to 5 (commonly used for star ratings)", true, false, false, false, false });

            migrationBuilder.InsertData(
                table: "LogMyDay_InputTypes",
                columns: new[] { "Id", "Name", "Description", "IsRangeEditable", "IsMinimumEditable", "IsMaximumEditable", "IsStepEditable", "IsRepeatableEditable" },
                values: new object[] { 8, "Rating 1-10", "Rating scale from 1 to 10", true, false, false, false, false });

            migrationBuilder.InsertData(
                table: "LogMyDay_InputTypes",
                columns: new[] { "Id", "Name", "Description", "IsRangeEditable", "IsMinimumEditable", "IsMaximumEditable", "IsStepEditable", "IsRepeatableEditable" },
                values: new object[] { 9, "Percentage", "Percentage value from 0 to 100", true, false, false, false, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Delete new InputTypes
            migrationBuilder.DeleteData(
                table: "LogMyDay_InputTypes",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "LogMyDay_InputTypes",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "LogMyDay_InputTypes",
                keyColumn: "Id",
                keyValue: 7);

            // Drop new columns
            migrationBuilder.DropColumn(
                name: "Description",
                table: "LogMyDay_InputTypes");

            migrationBuilder.DropColumn(
                name: "IsRepeatableEditable",
                table: "LogMyDay_InputTypes");

            migrationBuilder.DropColumn(
                name: "IsStepEditable",
                table: "LogMyDay_InputTypes");

            migrationBuilder.DropColumn(
                name: "IsMaximumEditable",
                table: "LogMyDay_InputTypes");

            migrationBuilder.DropColumn(
                name: "IsMinimumEditable",
                table: "LogMyDay_InputTypes");

            migrationBuilder.DropColumn(
                name: "IsRangeEditable",
                table: "LogMyDay_InputTypes");
        }
    }
}
