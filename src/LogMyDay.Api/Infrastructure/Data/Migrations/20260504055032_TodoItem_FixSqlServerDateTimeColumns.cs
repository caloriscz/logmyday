using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TodoItem_FixSqlServerDateTimeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.AlterColumn<DateTime>(
                    name: "DateCreated",
                    table: "LogMyDay_TodoLists",
                    type: "datetime2",
                    nullable: false,
                    oldClrType: typeof(DateTime),
                    oldType: "TEXT");

                migrationBuilder.AlterColumn<DateTime>(
                    name: "DateCreated",
                    table: "LogMyDay_TodoItems",
                    type: "datetime2",
                    nullable: false,
                    oldClrType: typeof(DateTime),
                    oldType: "TEXT");

                migrationBuilder.AlterColumn<DateTime>(
                    name: "DoneAt",
                    table: "LogMyDay_TodoItems",
                    type: "datetime2",
                    nullable: true,
                    oldClrType: typeof(DateTime),
                    oldType: "TEXT",
                    oldNullable: true);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.AlterColumn<string>(
                    name: "DateCreated",
                    table: "LogMyDay_TodoLists",
                    type: "TEXT",
                    nullable: false,
                    oldClrType: typeof(DateTime),
                    oldType: "datetime2");

                migrationBuilder.AlterColumn<string>(
                    name: "DateCreated",
                    table: "LogMyDay_TodoItems",
                    type: "TEXT",
                    nullable: false,
                    oldClrType: typeof(DateTime),
                    oldType: "datetime2");

                migrationBuilder.AlterColumn<string>(
                    name: "DoneAt",
                    table: "LogMyDay_TodoItems",
                    type: "TEXT",
                    nullable: true,
                    oldClrType: typeof(DateTime),
                    oldType: "datetime2",
                    oldNullable: true);
            }
        }
    }
}
