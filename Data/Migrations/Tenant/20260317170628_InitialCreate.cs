using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vectrik.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MachineConnectionSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ProviderType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EndpointUrl = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    PollIntervalSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    ConfigJson = table.Column<string>(type: "TEXT", nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineConnectionSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Machines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MachineType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MachineModel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Department = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsAvailableForScheduling = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    SupportedMaterials = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CurrentMaterial = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    MaintenanceIntervalHours = table.Column<double>(type: "REAL", nullable: false),
                    HoursSinceLastMaintenance = table.Column<double>(type: "REAL", nullable: false),
                    LastMaintenanceDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextMaintenanceDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalOperatingHours = table.Column<double>(type: "REAL", nullable: false),
                    BuildLengthMm = table.Column<double>(type: "REAL", nullable: false),
                    BuildWidthMm = table.Column<double>(type: "REAL", nullable: false),
                    BuildHeightMm = table.Column<double>(type: "REAL", nullable: false),
                    MaxLaserPowerWatts = table.Column<double>(type: "REAL", nullable: false),
                    OpcUaEndpointUrl = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    OpcUaEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.Id);
                    table.UniqueConstraint("AK_Machines_MachineId", x => x.MachineId);
                });

            migrationBuilder.CreateTable(
                name: "MachineStateRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BuildProgress = table.Column<double>(type: "REAL", nullable: true),
                    CurrentLayer = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalLayers = table.Column<int>(type: "INTEGER", nullable: true),
                    BedTemperature = table.Column<double>(type: "REAL", nullable: true),
                    ChamberTemperature = table.Column<double>(type: "REAL", nullable: true),
                    LaserPower = table.Column<double>(type: "REAL", nullable: true),
                    GasFlow = table.Column<double>(type: "REAL", nullable: true),
                    OxygenLevel = table.Column<double>(type: "REAL", nullable: true),
                    HumidityPercent = table.Column<double>(type: "REAL", nullable: true),
                    IsConnected = table.Column<bool>(type: "INTEGER", nullable: false),
                    RawDataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineStateRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Density = table.Column<double>(type: "REAL", nullable: true),
                    CostPerKg = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Supplier = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CompatibleMaterials = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperatingShifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    DaysOfWeek = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingShifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Material = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ManufacturingApproach = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AllowStacking = table.Column<bool>(type: "INTEGER", nullable: false),
                    SingleStackDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    DoubleStackDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    TripleStackDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    MaxStackCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PartsPerBuildSingle = table.Column<int>(type: "INTEGER", nullable: false),
                    PartsPerBuildDouble = table.Column<int>(type: "INTEGER", nullable: true),
                    PartsPerBuildTriple = table.Column<int>(type: "INTEGER", nullable: true),
                    EnableDoubleStack = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableTripleStack = table.Column<bool>(type: "INTEGER", nullable: false),
                    StageEstimateSingle = table.Column<double>(type: "REAL", nullable: true),
                    SlsBuildDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    SlsPartsPerBuild = table.Column<int>(type: "INTEGER", nullable: true),
                    DepowderingDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    DepowderingPartsPerBatch = table.Column<int>(type: "INTEGER", nullable: true),
                    HeatTreatmentDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    HeatTreatmentPartsPerBatch = table.Column<int>(type: "INTEGER", nullable: true),
                    WireEdmDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    WireEdmPartsPerSession = table.Column<int>(type: "INTEGER", nullable: true),
                    RequiredStages = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionStages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StageSlug = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    HasBuiltInPage = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresSerialNumber = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DefaultSetupMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultHourlyRate = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    RequiresQualityCheck = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowSkip = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsOptional = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiredRole = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CustomFieldsConfig = table.Column<string>(type: "TEXT", nullable: false),
                    AssignedMachineIds = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RequiresMachineAssignment = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultMachineId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    StageColor = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    StageIcon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Department = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AllowParallelExecution = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultMaterialCost = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    DefaultDurationHours = table.Column<double>(type: "REAL", nullable: false),
                    IsBatchStage = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionStages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Department = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    AssignedStageIds = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLoginDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MachineComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PartNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CurrentHours = table.Column<double>(type: "REAL", nullable: true),
                    CurrentBuilds = table.Column<int>(type: "INTEGER", nullable: true),
                    LastReplacedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InstallDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MachineComponents_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "MachineId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartStageRequirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductionStageId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExecutionOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowParallelExecution = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsBlocking = table.Column<bool>(type: "INTEGER", nullable: false),
                    EstimatedHours = table.Column<double>(type: "REAL", nullable: true),
                    SetupTimeMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    HourlyRateOverride = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    MaterialCost = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    AssignedMachineId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RequiresSpecificMachine = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreferredMachineIds = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CustomFieldValues = table.Column<string>(type: "TEXT", nullable: false),
                    StageParameters = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredMaterials = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredTooling = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    QualityRequirements = table.Column<string>(type: "TEXT", nullable: false),
                    SpecialInstructions = table.Column<string>(type: "TEXT", nullable: false),
                    RequirementNotes = table.Column<string>(type: "TEXT", nullable: false),
                    ActualAverageDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    ActualSampleCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastActualDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    EstimateSource = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EstimateLastUpdated = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartStageRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartStageRequirements_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartStageRequirements_ProductionStages_ProductionStageId",
                        column: x => x.ProductionStageId,
                        principalTable: "ProductionStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DashboardLayout = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultView = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    NotificationsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineComponentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TriggerType = table.Column<int>(type: "INTEGER", nullable: false),
                    ThresholdValue = table.Column<double>(type: "REAL", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    EarlyWarningPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Instructions = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceRules_MachineComponents_MachineComponentId",
                        column: x => x.MachineComponentId,
                        principalTable: "MachineComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceActionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MaintenanceRuleId = table.Column<int>(type: "INTEGER", nullable: true),
                    MachineId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MachineComponentId = table.Column<int>(type: "INTEGER", nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PerformedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceActionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceActionLogs_MaintenanceRules_MaintenanceRuleId",
                        column: x => x.MaintenanceRuleId,
                        principalTable: "MaintenanceRules",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceWorkOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MachineComponentId = table.Column<int>(type: "INTEGER", nullable: true),
                    MaintenanceRuleId = table.Column<int>(type: "INTEGER", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    AssignedTechnicianUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ScheduledDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StartedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EstimatedHours = table.Column<double>(type: "REAL", nullable: true),
                    ActualHours = table.Column<double>(type: "REAL", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    ActualCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    RequiresShutdown = table.Column<bool>(type: "INTEGER", nullable: false),
                    PartsUsed = table.Column<string>(type: "TEXT", nullable: true),
                    WorkPerformed = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceWorkOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceWorkOrders_MachineComponents_MachineComponentId",
                        column: x => x.MachineComponentId,
                        principalTable: "MachineComponents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceWorkOrders_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "MachineId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceWorkOrders_MaintenanceRules_MaintenanceRuleId",
                        column: x => x.MaintenanceRuleId,
                        principalTable: "MaintenanceRules",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceWorkOrders_Users_AssignedTechnicianUserId",
                        column: x => x.AssignedTechnicianUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BuildFileInfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BuildPackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LayerCount = table.Column<int>(type: "INTEGER", nullable: true),
                    BuildHeightMm = table.Column<decimal>(type: "TEXT", nullable: true),
                    EstimatedPrintTimeHours = table.Column<decimal>(type: "TEXT", nullable: true),
                    EstimatedPowderKg = table.Column<decimal>(type: "TEXT", nullable: true),
                    PartPositionsJson = table.Column<string>(type: "TEXT", nullable: true),
                    SlicerSoftware = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SlicerVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ImportedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImportedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildFileInfos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BuildJobParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BuildJobId = table.Column<int>(type: "INTEGER", nullable: false),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    PartNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildJobParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildJobParts_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BuildJobs",
                columns: table => new
                {
                    BuildId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PrinterName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ActualStartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActualEndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ScheduledStartTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ScheduledEndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Material = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    LaserRunTime = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    GasUsedLiters = table.Column<float>(type: "REAL", nullable: true),
                    PowderUsedLiters = table.Column<float>(type: "REAL", nullable: true),
                    EndReason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    OperatorEstimatedHours = table.Column<decimal>(type: "TEXT", nullable: true),
                    OperatorActualHours = table.Column<decimal>(type: "TEXT", nullable: true),
                    TotalPartsInBuild = table.Column<int>(type: "INTEGER", nullable: false),
                    JobId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildJobs", x => x.BuildId);
                    table.ForeignKey(
                        name: "FK_BuildJobs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BuildPackageParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BuildPackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkOrderLineId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildPackageParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildPackageParts_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BuildPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    MachineId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Material = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ScheduledJobId = table.Column<int>(type: "INTEGER", nullable: true),
                    ScheduledDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EstimatedDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildPackages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DelayLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BuildJobId = table.Column<int>(type: "INTEGER", nullable: true),
                    JobId = table.Column<int>(type: "INTEGER", nullable: true),
                    StageExecutionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ReasonCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    DelayMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    LoggedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LoggedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelayLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DelayLogs_BuildJobs_BuildJobId",
                        column: x => x.BuildJobId,
                        principalTable: "BuildJobs",
                        principalColumn: "BuildId");
                });

            migrationBuilder.CreateTable(
                name: "JobNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobId = table.Column<int>(type: "INTEGER", nullable: false),
                    NoteText = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    MachineId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    WorkOrderLineId = table.Column<int>(type: "INTEGER", nullable: true),
                    ScheduledStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ScheduledEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActualStart = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualEnd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PartNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ProducedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    DefectQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedHours = table.Column<double>(type: "REAL", nullable: false),
                    SlsMaterial = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    StackLevel = table.Column<byte>(type: "INTEGER", nullable: true),
                    PartsPerBuild = table.Column<int>(type: "INTEGER", nullable: true),
                    PlannedStackDurationHours = table.Column<double>(type: "REAL", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    PredecessorJobId = table.Column<int>(type: "INTEGER", nullable: true),
                    UpstreamGapHours = table.Column<double>(type: "REAL", nullable: true),
                    OperatorUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    LastStatusChangeUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jobs_Jobs_PredecessorJobId",
                        column: x => x.PredecessorJobId,
                        principalTable: "Jobs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Jobs_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Jobs_Users_OperatorUserId",
                        column: x => x.OperatorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StageExecutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProductionStageId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EstimatedHours = table.Column<double>(type: "REAL", nullable: true),
                    ActualHours = table.Column<double>(type: "REAL", nullable: true),
                    SetupHours = table.Column<double>(type: "REAL", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    ActualCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    MaterialCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    OperatorUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    OperatorName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CustomFieldValues = table.Column<string>(type: "TEXT", nullable: false),
                    QualityCheckRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    QualityCheckPassed = table.Column<bool>(type: "INTEGER", nullable: true),
                    QualityNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    Issues = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StageExecutions_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StageExecutions_ProductionStages_ProductionStageId",
                        column: x => x.ProductionStageId,
                        principalTable: "ProductionStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StageExecutions_Users_OperatorUserId",
                        column: x => x.OperatorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PartInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SerialNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    WorkOrderLineId = table.Column<int>(type: "INTEGER", nullable: false),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentStageId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartInstances_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartInstances_ProductionStages_CurrentStageId",
                        column: x => x.CurrentStageId,
                        principalTable: "ProductionStages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PartInstanceStageLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartInstanceId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductionStageId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OperatorName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CustomFieldValues = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartInstanceStageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartInstanceStageLogs_PartInstances_PartInstanceId",
                        column: x => x.PartInstanceId,
                        principalTable: "PartInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartInstanceStageLogs_ProductionStages_ProductionStageId",
                        column: x => x.ProductionStageId,
                        principalTable: "ProductionStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QCInspections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobId = table.Column<int>(type: "INTEGER", nullable: true),
                    BuildJobId = table.Column<int>(type: "INTEGER", nullable: true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: true),
                    PartInstanceId = table.Column<int>(type: "INTEGER", nullable: true),
                    InspectorUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    InspectionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OverallPass = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CorrectiveAction = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QCInspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QCInspections_BuildJobs_BuildJobId",
                        column: x => x.BuildJobId,
                        principalTable: "BuildJobs",
                        principalColumn: "BuildId");
                    table.ForeignKey(
                        name: "FK_QCInspections_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QCInspections_PartInstances_PartInstanceId",
                        column: x => x.PartInstanceId,
                        principalTable: "PartInstances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QCInspections_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QCInspections_Users_InspectorUserId",
                        column: x => x.InspectorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QCChecklistItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QCInspectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Passed = table.Column<bool>(type: "INTEGER", nullable: false),
                    MeasuredValue = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExpectedValue = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Tolerance = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QCChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QCChecklistItems_QCInspections_QCInspectionId",
                        column: x => x.QCInspectionId,
                        principalTable: "QCInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuoteLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuoteId = table.Column<int>(type: "INTEGER", nullable: false),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedCostPerPart = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    QuotedPricePerPart = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteLines_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Quotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuoteNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CustomerEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CustomerPhone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalEstimatedCost = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    QuotedPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Markup = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    ConvertedWorkOrderId = table.Column<int>(type: "INTEGER", nullable: true),
                    ConvertedWorkOrderId2 = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CustomerPO = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CustomerEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CustomerPhone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    OrderDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    QuoteId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrders_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ProducedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ShippedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderLines_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkOrderLines_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildFileInfos_BuildPackageId",
                table: "BuildFileInfos",
                column: "BuildPackageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuildJobParts_BuildJobId",
                table: "BuildJobParts",
                column: "BuildJobId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildJobParts_PartId",
                table: "BuildJobParts",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildJobs_JobId",
                table: "BuildJobs",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildJobs_UserId",
                table: "BuildJobs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackageParts_BuildPackageId",
                table: "BuildPackageParts",
                column: "BuildPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackageParts_PartId",
                table: "BuildPackageParts",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackageParts_WorkOrderLineId",
                table: "BuildPackageParts",
                column: "WorkOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildPackages_ScheduledJobId",
                table: "BuildPackages",
                column: "ScheduledJobId");

            migrationBuilder.CreateIndex(
                name: "IX_DelayLogs_BuildJobId",
                table: "DelayLogs",
                column: "BuildJobId");

            migrationBuilder.CreateIndex(
                name: "IX_DelayLogs_JobId",
                table: "DelayLogs",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_DelayLogs_StageExecutionId",
                table: "DelayLogs",
                column: "StageExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_JobNotes_JobId",
                table: "JobNotes",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_OperatorUserId",
                table: "Jobs",
                column: "OperatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_PartId",
                table: "Jobs",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_PredecessorJobId",
                table: "Jobs",
                column: "PredecessorJobId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_WorkOrderLineId",
                table: "Jobs",
                column: "WorkOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineComponents_MachineId",
                table: "MachineComponents",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineConnectionSettings_MachineId",
                table: "MachineConnectionSettings",
                column: "MachineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Machines_MachineId",
                table: "Machines",
                column: "MachineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceActionLogs_MaintenanceRuleId",
                table: "MaintenanceActionLogs",
                column: "MaintenanceRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRules_MachineComponentId",
                table: "MaintenanceRules",
                column: "MachineComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_AssignedTechnicianUserId",
                table: "MaintenanceWorkOrders",
                column: "AssignedTechnicianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_MachineComponentId",
                table: "MaintenanceWorkOrders",
                column: "MachineComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_MachineId",
                table: "MaintenanceWorkOrders",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWorkOrders_MaintenanceRuleId",
                table: "MaintenanceWorkOrders",
                column: "MaintenanceRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_PartInstances_CurrentStageId",
                table: "PartInstances",
                column: "CurrentStageId");

            migrationBuilder.CreateIndex(
                name: "IX_PartInstances_PartId",
                table: "PartInstances",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_PartInstances_SerialNumber",
                table: "PartInstances",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartInstances_WorkOrderLineId",
                table: "PartInstances",
                column: "WorkOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PartInstanceStageLogs_PartInstanceId",
                table: "PartInstanceStageLogs",
                column: "PartInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_PartInstanceStageLogs_ProductionStageId",
                table: "PartInstanceStageLogs",
                column: "ProductionStageId");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_PartNumber",
                table: "Parts",
                column: "PartNumber");

            migrationBuilder.CreateIndex(
                name: "IX_PartStageRequirements_PartId",
                table: "PartStageRequirements",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_PartStageRequirements_ProductionStageId",
                table: "PartStageRequirements",
                column: "ProductionStageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionStages_StageSlug",
                table: "ProductionStages",
                column: "StageSlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QCChecklistItems_QCInspectionId",
                table: "QCChecklistItems",
                column: "QCInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_QCInspections_BuildJobId",
                table: "QCInspections",
                column: "BuildJobId");

            migrationBuilder.CreateIndex(
                name: "IX_QCInspections_InspectorUserId",
                table: "QCInspections",
                column: "InspectorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QCInspections_JobId",
                table: "QCInspections",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_QCInspections_PartId",
                table: "QCInspections",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_QCInspections_PartInstanceId",
                table: "QCInspections",
                column: "PartInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteLines_PartId",
                table: "QuoteLines",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteLines_QuoteId",
                table: "QuoteLines",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_ConvertedWorkOrderId2",
                table: "Quotes",
                column: "ConvertedWorkOrderId2");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_QuoteNumber",
                table: "Quotes",
                column: "QuoteNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_JobId",
                table: "StageExecutions",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_OperatorUserId",
                table: "StageExecutions",
                column: "OperatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_ProductionStageId",
                table: "StageExecutions",
                column: "ProductionStageId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_UserId",
                table: "UserSettings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderLines_PartId",
                table: "WorkOrderLines",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderLines_WorkOrderId",
                table: "WorkOrderLines",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_OrderNumber",
                table: "WorkOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_QuoteId",
                table: "WorkOrders",
                column: "QuoteId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BuildFileInfos_BuildPackages_BuildPackageId",
                table: "BuildFileInfos",
                column: "BuildPackageId",
                principalTable: "BuildPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BuildJobParts_BuildJobs_BuildJobId",
                table: "BuildJobParts",
                column: "BuildJobId",
                principalTable: "BuildJobs",
                principalColumn: "BuildId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BuildJobs_Jobs_JobId",
                table: "BuildJobs",
                column: "JobId",
                principalTable: "Jobs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BuildPackageParts_BuildPackages_BuildPackageId",
                table: "BuildPackageParts",
                column: "BuildPackageId",
                principalTable: "BuildPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BuildPackageParts_WorkOrderLines_WorkOrderLineId",
                table: "BuildPackageParts",
                column: "WorkOrderLineId",
                principalTable: "WorkOrderLines",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BuildPackages_Jobs_ScheduledJobId",
                table: "BuildPackages",
                column: "ScheduledJobId",
                principalTable: "Jobs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DelayLogs_Jobs_JobId",
                table: "DelayLogs",
                column: "JobId",
                principalTable: "Jobs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DelayLogs_StageExecutions_StageExecutionId",
                table: "DelayLogs",
                column: "StageExecutionId",
                principalTable: "StageExecutions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_JobNotes_Jobs_JobId",
                table: "JobNotes",
                column: "JobId",
                principalTable: "Jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_WorkOrderLines_WorkOrderLineId",
                table: "Jobs",
                column: "WorkOrderLineId",
                principalTable: "WorkOrderLines",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PartInstances_WorkOrderLines_WorkOrderLineId",
                table: "PartInstances",
                column: "WorkOrderLineId",
                principalTable: "WorkOrderLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteLines_Quotes_QuoteId",
                table: "QuoteLines",
                column: "QuoteId",
                principalTable: "Quotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_WorkOrders_ConvertedWorkOrderId2",
                table: "Quotes",
                column: "ConvertedWorkOrderId2",
                principalTable: "WorkOrders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Quotes_QuoteId",
                table: "WorkOrders");

            migrationBuilder.DropTable(
                name: "BuildFileInfos");

            migrationBuilder.DropTable(
                name: "BuildJobParts");

            migrationBuilder.DropTable(
                name: "BuildPackageParts");

            migrationBuilder.DropTable(
                name: "DelayLogs");

            migrationBuilder.DropTable(
                name: "JobNotes");

            migrationBuilder.DropTable(
                name: "MachineConnectionSettings");

            migrationBuilder.DropTable(
                name: "MachineStateRecords");

            migrationBuilder.DropTable(
                name: "MaintenanceActionLogs");

            migrationBuilder.DropTable(
                name: "MaintenanceWorkOrders");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "OperatingShifts");

            migrationBuilder.DropTable(
                name: "PartInstanceStageLogs");

            migrationBuilder.DropTable(
                name: "PartStageRequirements");

            migrationBuilder.DropTable(
                name: "QCChecklistItems");

            migrationBuilder.DropTable(
                name: "QuoteLines");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "BuildPackages");

            migrationBuilder.DropTable(
                name: "StageExecutions");

            migrationBuilder.DropTable(
                name: "MaintenanceRules");

            migrationBuilder.DropTable(
                name: "QCInspections");

            migrationBuilder.DropTable(
                name: "MachineComponents");

            migrationBuilder.DropTable(
                name: "BuildJobs");

            migrationBuilder.DropTable(
                name: "PartInstances");

            migrationBuilder.DropTable(
                name: "Machines");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "ProductionStages");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "WorkOrderLines");

            migrationBuilder.DropTable(
                name: "Parts");

            migrationBuilder.DropTable(
                name: "Quotes");

            migrationBuilder.DropTable(
                name: "WorkOrders");
        }
    }
}
