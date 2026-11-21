using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LogMyDay.Api.Infrastructure.Data.Migrations.UserData
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogMyDay_ColorSchemes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_ColorSchemes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_EventLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_EventLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_InputTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsRangeEditable = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsMinimumEditable = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsMaximumEditable = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsStepEditable = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRepeatableEditable = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_InputTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_Patterns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    PatternValue = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Patterns", x => x.Id);
                });

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
                name: "LogMyDay_TagGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TagGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_TagOptionLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TagOptionLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_ColorSchemeEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ColorSchemeId = table.Column<int>(type: "INTEGER", nullable: false),
                    RangeFrom = table.Column<double>(type: "REAL", nullable: true),
                    RangeTo = table.Column<double>(type: "REAL", nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_ColorSchemeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_ColorSchemeEntries_LogMyDay_ColorSchemes_ColorSchemeId",
                        column: x => x.ColorSchemeId,
                        principalTable: "LogMyDay_ColorSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_EventLogDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventLogId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_EventLogDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_EventLogDetails_LogMyDay_EventLogs_EventLogId",
                        column: x => x.EventLogId,
                        principalTable: "LogMyDay_EventLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_TagOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OptionListId = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true)
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
                name: "LogMyDay_Activities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateStarted = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateFinished = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TagId = table.Column<int>(type: "INTEGER", nullable: false),
                    RuleId = table.Column<int>(type: "INTEGER", nullable: true),
                    WindowKey = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    RecordId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Activities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_Quantities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    BaseUnitId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Quantities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_Units",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", nullable: false),
                    QuantityId = table.Column<int>(type: "INTEGER", nullable: false),
                    AToBase = table.Column<double>(type: "REAL", nullable: false),
                    BToBase = table.Column<double>(type: "REAL", nullable: false),
                    Decimals = table.Column<int>(type: "INTEGER", nullable: false)
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TagName = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    InputTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    TimeGranularity = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRepeatable = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRange = table.Column<bool>(type: "INTEGER", nullable: false),
                    PatternId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UnitId = table.Column<int>(type: "INTEGER", nullable: true),
                    MinValue = table.Column<double>(type: "REAL", nullable: true),
                    MaxValue = table.Column<double>(type: "REAL", nullable: true),
                    Step = table.Column<double>(type: "REAL", nullable: true),
                    DefaultValue = table.Column<string>(type: "TEXT", nullable: true),
                    OptionListId = table.Column<int>(type: "INTEGER", nullable: true),
                    GroupId = table.Column<int>(type: "INTEGER", nullable: true),
                    ColorSchemeId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsComputed = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRecordType = table.Column<bool>(type: "INTEGER", nullable: false),
                    RecordTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    RecordFieldOrder = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_Tags_LogMyDay_ColorSchemes_ColorSchemeId",
                        column: x => x.ColorSchemeId,
                        principalTable: "LogMyDay_ColorSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                        name: "FK_LogMyDay_Tags_LogMyDay_TagGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "LogMyDay_TagGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LogMyDay_Tags_LogMyDay_TagOptionLists_OptionListId",
                        column: x => x.OptionListId,
                        principalTable: "LogMyDay_TagOptionLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LogMyDay_Tags_LogMyDay_Tags_RecordTypeId",
                        column: x => x.RecordTypeId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LogMyDay_Tags_LogMyDay_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "LogMyDay_Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_Records",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecordTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    DateStarted = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_Records_LogMyDay_Tags_RecordTypeId",
                        column: x => x.RecordTypeId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_ScanMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CodeValue = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    CodeType = table.Column<int>(type: "INTEGER", nullable: false),
                    TagId = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DefaultDescription = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_ScanMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_ScanMappings_LogMyDay_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_TagDayLocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    SetAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SetBy = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TagDayLocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagDayLocks_LogMyDay_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_TagChecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IfTagId = table.Column<int>(type: "INTEGER", nullable: false),
                    IfOperator = table.Column<int>(type: "INTEGER", nullable: false),
                    IfOperand = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ThenTagId = table.Column<int>(type: "INTEGER", nullable: false),
                    ThenOperator = table.Column<int>(type: "INTEGER", nullable: false),
                    ThenOperand = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TagChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagChecks_LogMyDay_Tags_IfTagId",
                        column: x => x.IfTagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagChecks_LogMyDay_Tags_ThenTagId",
                        column: x => x.ThenTagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_TagRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Template = table.Column<int>(type: "INTEGER", nullable: false),
                    AggregateKind = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetTagId = table.Column<int>(type: "INTEGER", nullable: false),
                    IgnoreZero = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TagRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagRules_LogMyDay_Tags_TargetTagId",
                        column: x => x.TargetTagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_TagTriggers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IfTagId = table.Column<int>(type: "INTEGER", nullable: false),
                    IfOperator = table.Column<int>(type: "INTEGER", nullable: false),
                    IfOperand = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PromptTagId = table.Column<int>(type: "INTEGER", nullable: false),
                    Timing = table.Column<int>(type: "INTEGER", nullable: false),
                    OffsetMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    AtTime = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TagTriggers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagTriggers_LogMyDay_Tags_IfTagId",
                        column: x => x.IfTagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagTriggers_LogMyDay_Tags_PromptTagId",
                        column: x => x.PromptTagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_TodoLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ShowOnHomepage = table.Column<bool>(type: "INTEGER", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletionTagId = table.Column<int>(type: "INTEGER", nullable: true),
                    AutoLogMode = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TodoLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TodoLists_LogMyDay_Tags_CompletionTagId",
                        column: x => x.CompletionTagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

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

            migrationBuilder.CreateTable(
                name: "LogMyDay_TagRuleSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceTagId = table.Column<int>(type: "INTEGER", nullable: false),
                    Factor = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TagRuleSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagRuleSources_LogMyDay_TagRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "LogMyDay_TagRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TagRuleSources_LogMyDay_Tags_SourceTagId",
                        column: x => x.SourceTagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_Reminders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    NotifyAt = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    IsDone = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DoneAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SkippedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RecurrenceType = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoLogMode = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletionTagId = table.Column<int>(type: "INTEGER", nullable: true),
                    MonitorFromDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    MonitorToDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    AllowUnfilled = table.Column<bool>(type: "INTEGER", nullable: false),
                    TriggerId = table.Column<int>(type: "INTEGER", nullable: true),
                    TriggerDay = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_Reminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_Reminders_LogMyDay_TagTriggers_TriggerId",
                        column: x => x.TriggerId,
                        principalTable: "LogMyDay_TagTriggers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LogMyDay_Reminders_LogMyDay_Tags_CompletionTagId",
                        column: x => x.CompletionTagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_TodoItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ListId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NotifyAt = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    IsDone = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DoneAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SkippedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RecurrenceType = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoLogMode = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletionTagId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_TodoItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TodoItems_LogMyDay_Tags_CompletionTagId",
                        column: x => x.CompletionTagId,
                        principalTable: "LogMyDay_Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LogMyDay_TodoItems_LogMyDay_TodoLists_ListId",
                        column: x => x.ListId,
                        principalTable: "LogMyDay_TodoLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogMyDay_ReminderDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReminderId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsDone = table.Column<bool>(type: "INTEGER", nullable: false),
                    DoneAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsSkipped = table.Column<bool>(type: "INTEGER", nullable: false),
                    SkippedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletionValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMyDay_ReminderDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogMyDay_ReminderDays_LogMyDay_Reminders_ReminderId",
                        column: x => x.ReminderId,
                        principalTable: "LogMyDay_Reminders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "LogMyDay_InputTypes",
                columns: new[] { "Id", "Description", "IsMaximumEditable", "IsMinimumEditable", "IsRangeEditable", "IsRepeatableEditable", "IsStepEditable", "Name" },
                values: new object[,]
                {
                    { 1, "Whole number input (e.g., 1, 42, -5)", true, true, true, true, true, "Integer" },
                    { 2, "Free-form text input", false, false, true, true, false, "String" },
                    { 3, "True/false checkbox input", false, false, true, false, false, "Boolean" },
                    { 4, "Date selection input", false, false, true, false, false, "Date" },
                    { 5, "Time selection input", false, false, true, false, false, "Time" },
                    { 6, "Decimal number with 2 decimal places (e.g., 3.14, 99.99)", true, true, true, true, true, "Decimal, precision 2" },
                    { 7, "Star rating scale from 0 to 5 (higher is better)", false, false, true, false, false, "Star Rating 0-5" },
                    { 8, "Star rating scale from 0 to 10 (higher is better)", false, false, true, false, false, "Star Rating 0-10" },
                    { 9, "Percentage value from 0 to 100", false, false, true, false, false, "Percentage" },
                    { 10, "Score scale from 0 to 5 (lower is better, e.g. pain, severity)", false, false, true, false, false, "Score 0-5" },
                    { 11, "Score scale from 0 to 10 (lower is better, e.g. pain, severity)", false, false, true, false, false, "Score 0-10" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Activities_RecordId",
                table: "LogMyDay_Activities",
                column: "RecordId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Activities_RuleId_WindowKey",
                table: "LogMyDay_Activities",
                columns: new[] { "RuleId", "WindowKey" },
                unique: true,
                filter: "[RuleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Activities_TagId",
                table: "LogMyDay_Activities",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Activities_UserId_TagId_DateStarted",
                table: "LogMyDay_Activities",
                columns: new[] { "UserId", "TagId", "DateStarted" });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ColorSchemeEntries_ColorSchemeId",
                table: "LogMyDay_ColorSchemeEntries",
                column: "ColorSchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ColorSchemes_UserId",
                table: "LogMyDay_ColorSchemes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_EventLogDetails_EventLogId",
                table: "LogMyDay_EventLogDetails",
                column: "EventLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_EventLogs_Level",
                table: "LogMyDay_EventLogs",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_EventLogs_UserId_CreatedUtc",
                table: "LogMyDay_EventLogs",
                columns: new[] { "UserId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Quantities_BaseUnitId",
                table: "LogMyDay_Quantities",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Records_RecordTypeId",
                table: "LogMyDay_Records",
                column: "RecordTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Records_UserId_DateStarted",
                table: "LogMyDay_Records",
                columns: new[] { "UserId", "DateStarted" });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ReminderDays_ReminderId_Date",
                table: "LogMyDay_ReminderDays",
                columns: new[] { "ReminderId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ReminderDays_UserId_Date",
                table: "LogMyDay_ReminderDays",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Reminders_CompletionTagId",
                table: "LogMyDay_Reminders",
                column: "CompletionTagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Reminders_TriggerId_TriggerDay",
                table: "LogMyDay_Reminders",
                columns: new[] { "TriggerId", "TriggerDay" },
                unique: true,
                filter: "[TriggerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Reminders_UserId",
                table: "LogMyDay_Reminders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ScanMappings_CodeValue",
                table: "LogMyDay_ScanMappings",
                column: "CodeValue");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ScanMappings_TagId",
                table: "LogMyDay_ScanMappings",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_ScanMappings_UserId_CodeValue",
                table: "LogMyDay_ScanMappings",
                columns: new[] { "UserId", "CodeValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Settings_Key",
                table: "LogMyDay_Settings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagDayLocks_TagId",
                table: "LogMyDay_TagDayLocks",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagDayLocks_UserId_TagId_Date",
                table: "LogMyDay_TagDayLocks",
                columns: new[] { "UserId", "TagId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagGroups_UserId",
                table: "LogMyDay_TagGroups",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagChecks_IfTagId",
                table: "LogMyDay_TagChecks",
                column: "IfTagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagChecks_ThenTagId",
                table: "LogMyDay_TagChecks",
                column: "ThenTagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagChecks_UserId",
                table: "LogMyDay_TagChecks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagOptions_OptionListId",
                table: "LogMyDay_TagOptions",
                column: "OptionListId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagRuleCases_RuleId_SortOrder",
                table: "LogMyDay_TagRuleCases",
                columns: new[] { "RuleId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagRules_TargetTagId",
                table: "LogMyDay_TagRules",
                column: "TargetTagId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagRules_UserId",
                table: "LogMyDay_TagRules",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagRuleSources_RuleId_SourceTagId",
                table: "LogMyDay_TagRuleSources",
                columns: new[] { "RuleId", "SourceTagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagRuleSources_SourceTagId",
                table: "LogMyDay_TagRuleSources",
                column: "SourceTagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Tags_ColorSchemeId",
                table: "LogMyDay_Tags",
                column: "ColorSchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Tags_GroupId",
                table: "LogMyDay_Tags",
                column: "GroupId");

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
                name: "IX_LogMyDay_Tags_RecordTypeId",
                table: "LogMyDay_Tags",
                column: "RecordTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Tags_UnitId",
                table: "LogMyDay_Tags",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagTriggers_IfTagId",
                table: "LogMyDay_TagTriggers",
                column: "IfTagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagTriggers_PromptTagId",
                table: "LogMyDay_TagTriggers",
                column: "PromptTagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TagTriggers_UserId",
                table: "LogMyDay_TagTriggers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TodoItems_CompletionTagId",
                table: "LogMyDay_TodoItems",
                column: "CompletionTagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TodoItems_ListId",
                table: "LogMyDay_TodoItems",
                column: "ListId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TodoLists_CompletionTagId",
                table: "LogMyDay_TodoLists",
                column: "CompletionTagId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_TodoLists_UserId",
                table: "LogMyDay_TodoLists",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LogMyDay_Units_QuantityId",
                table: "LogMyDay_Units",
                column: "QuantityId");

            migrationBuilder.AddForeignKey(
                name: "FK_LogMyDay_Activities_LogMyDay_Records_RecordId",
                table: "LogMyDay_Activities",
                column: "RecordId",
                principalTable: "LogMyDay_Records",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LogMyDay_Activities_LogMyDay_TagRules_RuleId",
                table: "LogMyDay_Activities",
                column: "RuleId",
                principalTable: "LogMyDay_TagRules",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LogMyDay_Activities_LogMyDay_Tags_TagId",
                table: "LogMyDay_Activities",
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
                name: "LogMyDay_ColorSchemeEntries");

            migrationBuilder.DropTable(
                name: "LogMyDay_EventLogDetails");

            migrationBuilder.DropTable(
                name: "LogMyDay_ReminderDays");

            migrationBuilder.DropTable(
                name: "LogMyDay_ScanMappings");

            migrationBuilder.DropTable(
                name: "LogMyDay_Settings");

            migrationBuilder.DropTable(
                name: "LogMyDay_TagDayLocks");

            migrationBuilder.DropTable(
                name: "LogMyDay_TagChecks");

            migrationBuilder.DropTable(
                name: "LogMyDay_TagOptions");

            migrationBuilder.DropTable(
                name: "LogMyDay_TagRuleCases");

            migrationBuilder.DropTable(
                name: "LogMyDay_TagRuleSources");

            migrationBuilder.DropTable(
                name: "LogMyDay_TodoItems");

            migrationBuilder.DropTable(
                name: "LogMyDay_Records");

            migrationBuilder.DropTable(
                name: "LogMyDay_EventLogs");

            migrationBuilder.DropTable(
                name: "LogMyDay_Reminders");

            migrationBuilder.DropTable(
                name: "LogMyDay_TagRules");

            migrationBuilder.DropTable(
                name: "LogMyDay_TodoLists");

            migrationBuilder.DropTable(
                name: "LogMyDay_TagTriggers");

            migrationBuilder.DropTable(
                name: "LogMyDay_Tags");

            migrationBuilder.DropTable(
                name: "LogMyDay_ColorSchemes");

            migrationBuilder.DropTable(
                name: "LogMyDay_InputTypes");

            migrationBuilder.DropTable(
                name: "LogMyDay_Patterns");

            migrationBuilder.DropTable(
                name: "LogMyDay_TagGroups");

            migrationBuilder.DropTable(
                name: "LogMyDay_TagOptionLists");

            migrationBuilder.DropTable(
                name: "LogMyDay_Units");

            migrationBuilder.DropTable(
                name: "LogMyDay_Quantities");
        }
    }
}
