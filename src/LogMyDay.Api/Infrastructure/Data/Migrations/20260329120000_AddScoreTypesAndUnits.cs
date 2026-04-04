using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMyDay.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScoreTypesAndUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Input Types: Rename existing ratings to 0-based ---
            migrationBuilder.UpdateData(
                table: "LogMyDay_InputTypes",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Name", "Description" },
                values: new object[] { "Star Rating 0-5", "Star rating scale from 0 to 5 (higher is better)" });

            migrationBuilder.UpdateData(
                table: "LogMyDay_InputTypes",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "Name", "Description" },
                values: new object[] { "Star Rating 0-10", "Star rating scale from 0 to 10 (higher is better)" });

            // --- Input Types: Add Score types (0 = best) ---
            migrationBuilder.InsertData(
                table: "LogMyDay_InputTypes",
                columns: new[] { "Id", "Name", "IsRangeEditable", "IsMinimumEditable", "IsMaximumEditable", "IsStepEditable", "IsRepeatableEditable", "Description" },
                values: new object[] { 10, "Score 0-5", true, false, false, false, false, "Score scale from 0 to 5 (lower is better, e.g. pain, severity)" });

            migrationBuilder.InsertData(
                table: "LogMyDay_InputTypes",
                columns: new[] { "Id", "Name", "IsRangeEditable", "IsMinimumEditable", "IsMaximumEditable", "IsStepEditable", "IsRepeatableEditable", "Description" },
                values: new object[] { 11, "Score 0-10", true, false, false, false, false, "Score scale from 0 to 10 (lower is better, e.g. pain, severity)" });

            // --- Quantities ---
            migrationBuilder.Sql(@"
                INSERT INTO LogMyDay_Quantities (Key) SELECT 'volume' WHERE NOT EXISTS (SELECT 1 FROM LogMyDay_Quantities WHERE Key = 'volume');
                INSERT INTO LogMyDay_Quantities (Key) SELECT 'energy' WHERE NOT EXISTS (SELECT 1 FROM LogMyDay_Quantities WHERE Key = 'energy');
                INSERT INTO LogMyDay_Quantities (Key) SELECT 'length' WHERE NOT EXISTS (SELECT 1 FROM LogMyDay_Quantities WHERE Key = 'length');
                INSERT INTO LogMyDay_Quantities (Key) SELECT 'temperature' WHERE NOT EXISTS (SELECT 1 FROM LogMyDay_Quantities WHERE Key = 'temperature');
                INSERT INTO LogMyDay_Quantities (Key) SELECT 'dosage' WHERE NOT EXISTS (SELECT 1 FROM LogMyDay_Quantities WHERE Key = 'dosage');
            ");

            // --- Mass units (milligram, microgram) ---
            migrationBuilder.Sql(@"
                INSERT INTO LogMyDay_Units (Key, Symbol, QuantityId, AToBase, BToBase, Decimals)
                SELECT 'milligram', 'mg', q.Id, 0.000001, 0, 0
                FROM LogMyDay_Quantities q WHERE q.Key = 'mass'
                AND NOT EXISTS (SELECT 1 FROM LogMyDay_Units WHERE Key = 'milligram');

                INSERT INTO LogMyDay_Units (Key, Symbol, QuantityId, AToBase, BToBase, Decimals)
                SELECT 'microgram', 'µg', q.Id, 0.000000001, 0, 0
                FROM LogMyDay_Quantities q WHERE q.Key = 'mass'
                AND NOT EXISTS (SELECT 1 FROM LogMyDay_Units WHERE Key = 'microgram');
            ");

            // --- Volume units ---
            migrationBuilder.Sql(@"
                INSERT INTO LogMyDay_Units (Key, Symbol, QuantityId, AToBase, BToBase, Decimals)
                SELECT 'liter', 'L', q.Id, 1, 0, 2
                FROM LogMyDay_Quantities q WHERE q.Key = 'volume'
                AND NOT EXISTS (SELECT 1 FROM LogMyDay_Units WHERE Key = 'liter');

                INSERT INTO LogMyDay_Units (Key, Symbol, QuantityId, AToBase, BToBase, Decimals)
                SELECT 'centiliter', 'cL', q.Id, 0.01, 0, 0
                FROM LogMyDay_Quantities q WHERE q.Key = 'volume'
                AND NOT EXISTS (SELECT 1 FROM LogMyDay_Units WHERE Key = 'centiliter');

                INSERT INTO LogMyDay_Units (Key, Symbol, QuantityId, AToBase, BToBase, Decimals)
                SELECT 'milliliter', 'mL', q.Id, 0.001, 0, 0
                FROM LogMyDay_Quantities q WHERE q.Key = 'volume'
                AND NOT EXISTS (SELECT 1 FROM LogMyDay_Units WHERE Key = 'milliliter');

                UPDATE LogMyDay_Quantities SET BaseUnitId = (SELECT Id FROM LogMyDay_Units WHERE Key = 'liter') WHERE Key = 'volume' AND BaseUnitId IS NULL;
            ");

            // --- Energy units ---
            migrationBuilder.Sql(@"
                INSERT INTO LogMyDay_Units (Key, Symbol, QuantityId, AToBase, BToBase, Decimals)
                SELECT 'kilocalorie', 'kcal', q.Id, 1, 0, 0
                FROM LogMyDay_Quantities q WHERE q.Key = 'energy'
                AND NOT EXISTS (SELECT 1 FROM LogMyDay_Units WHERE Key = 'kilocalorie');

                INSERT INTO LogMyDay_Units (Key, Symbol, QuantityId, AToBase, BToBase, Decimals)
                SELECT 'calorie', 'cal', q.Id, 0.001, 0, 0
                FROM LogMyDay_Quantities q WHERE q.Key = 'energy'
                AND NOT EXISTS (SELECT 1 FROM LogMyDay_Units WHERE Key = 'calorie');

                INSERT INTO LogMyDay_Units (Key, Symbol, QuantityId, AToBase, BToBase, Decimals)
                SELECT 'kilojoule', 'kJ', q.Id, 0.239006, 0, 0
                FROM LogMyDay_Quantities q WHERE q.Key = 'energy'
                AND NOT EXISTS (SELECT 1 FROM LogMyDay_Units WHERE Key = 'kilojoule');

                UPDATE LogMyDay_Quantities SET BaseUnitId = (SELECT Id FROM LogMyDay_Units WHERE Key = 'kilocalorie') WHERE Key = 'energy' AND BaseUnitId IS NULL;
            ");

            // --- Length units ---
            migrationBuilder.Sql(@"
                INSERT INTO LogMyDay_Units (Key, Symbol, QuantityId, AToBase, BToBase, Decimals)
                SELECT 'meter', 'm', q.Id, 1, 0, 2
                FROM LogMyDay_Quantities q WHERE q.Key = 'length'
                AND NOT EXISTS (SELECT 1 FROM LogMyDay_Units WHERE Key = 'meter');

                INSERT INTO LogMyDay_Units (Key, Symbol, QuantityId, AToBase, BToBase, Decimals)
                SELECT 'centimeter', 'cm', q.Id, 0.01, 0, 0
                FROM LogMyDay_Quantities q WHERE q.Key = 'length'
                AND NOT EXISTS (SELECT 1 FROM LogMyDay_Units WHERE Key = 'centimeter');

                INSERT INTO LogMyDay_Units (Key, Symbol, QuantityId, AToBase, BToBase, Decimals)
                SELECT 'kilometer', 'km', q.Id, 1000, 0, 3
                FROM LogMyDay_Quantities q WHERE q.Key = 'length'
                AND NOT EXISTS (SELECT 1 FROM LogMyDay_Units WHERE Key = 'kilometer');

                UPDATE LogMyDay_Quantities SET BaseUnitId = (SELECT Id FROM LogMyDay_Units WHERE Key = 'meter') WHERE Key = 'length' AND BaseUnitId IS NULL;
            ");

            // --- Temperature units ---
            migrationBuilder.Sql(@"
                INSERT INTO LogMyDay_Units (Key, Symbol, QuantityId, AToBase, BToBase, Decimals)
                SELECT 'celsius', '°C', q.Id, 1, 0, 1
                FROM LogMyDay_Quantities q WHERE q.Key = 'temperature'
                AND NOT EXISTS (SELECT 1 FROM LogMyDay_Units WHERE Key = 'celsius');

                INSERT INTO LogMyDay_Units (Key, Symbol, QuantityId, AToBase, BToBase, Decimals)
                SELECT 'fahrenheit', '°F', q.Id, 0.555556, -17.777778, 1
                FROM LogMyDay_Quantities q WHERE q.Key = 'temperature'
                AND NOT EXISTS (SELECT 1 FROM LogMyDay_Units WHERE Key = 'fahrenheit');

                UPDATE LogMyDay_Quantities SET BaseUnitId = (SELECT Id FROM LogMyDay_Units WHERE Key = 'celsius') WHERE Key = 'temperature' AND BaseUnitId IS NULL;
            ");

            // --- Dosage units ---
            migrationBuilder.Sql(@"
                INSERT INTO LogMyDay_Units (Key, Symbol, QuantityId, AToBase, BToBase, Decimals)
                SELECT 'iu', 'IU', q.Id, 1, 0, 0
                FROM LogMyDay_Quantities q WHERE q.Key = 'dosage'
                AND NOT EXISTS (SELECT 1 FROM LogMyDay_Units WHERE Key = 'iu');

                UPDATE LogMyDay_Quantities SET BaseUnitId = (SELECT Id FROM LogMyDay_Units WHERE Key = 'iu') WHERE Key = 'dosage' AND BaseUnitId IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // --- Remove Score input types ---
            migrationBuilder.DeleteData(
                table: "LogMyDay_InputTypes",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "LogMyDay_InputTypes",
                keyColumn: "Id",
                keyValue: 10);

            // --- Restore original rating names ---
            migrationBuilder.UpdateData(
                table: "LogMyDay_InputTypes",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Name", "Description" },
                values: new object[] { "Rating 1-5", "Rating scale from 1 to 5 (commonly used for star ratings)" });

            migrationBuilder.UpdateData(
                table: "LogMyDay_InputTypes",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "Name", "Description" },
                values: new object[] { "Rating 1-10", "Rating scale from 1 to 10" });

            // --- Remove new units (clear BaseUnitId first to avoid FK issues) ---
            migrationBuilder.Sql(@"
                UPDATE LogMyDay_Quantities SET BaseUnitId = NULL WHERE Key IN ('volume', 'energy', 'length', 'temperature', 'dosage');

                DELETE FROM LogMyDay_Units WHERE Key IN ('milligram', 'microgram', 'liter', 'centiliter', 'milliliter', 'kilocalorie', 'calorie', 'kilojoule', 'meter', 'centimeter', 'kilometer', 'celsius', 'fahrenheit', 'iu');

                DELETE FROM LogMyDay_Quantities WHERE Key IN ('volume', 'energy', 'length', 'temperature', 'dosage');
            ");
        }
    }
}
