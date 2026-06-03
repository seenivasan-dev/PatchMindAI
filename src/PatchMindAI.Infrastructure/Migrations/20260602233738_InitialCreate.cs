using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatchMindAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CveId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserQuery = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskScore = table.Column<double>(type: "float", nullable: false),
                    RiskJustification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ImpactSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AffectedAssetsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RemediationStepsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RawAgentOutputJson = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Hostname = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    OperatingSystem = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Criticality = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsInternetFacing = table.Column<bool>(type: "bit", nullable: false),
                    InstalledSoftware = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BusinessUnit = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastScannedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserQuery = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    JobId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CveId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cves",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    BaseScore = table.Column<double>(type: "float", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    VectorString = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Weaknesses = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AffectedProducts = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    References = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cves", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatchStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CveId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PatchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PatchVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TargetPatchDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedTo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatchStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatchStatuses_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PatchStatuses_Cves_CveId",
                        column: x => x.CveId,
                        principalTable: "Cves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisJobs_CreatedAtUtc",
                table: "AnalysisJobs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisJobs_CveId",
                table: "AnalysisJobs",
                column: "CveId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisJobs_Status",
                table: "AnalysisJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResults_GeneratedAtUtc",
                table: "AnalysisResults",
                column: "GeneratedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResults_JobId",
                table: "AnalysisResults",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Criticality",
                table: "Assets",
                column: "Criticality");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Hostname",
                table: "Assets",
                column: "Hostname");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_IsActive",
                table: "Assets",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_IsInternetFacing",
                table: "Assets",
                column: "IsInternetFacing");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EventType",
                table: "AuditLogs",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TimestampUtc",
                table: "AuditLogs",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Cves_BaseScore",
                table: "Cves",
                column: "BaseScore");

            migrationBuilder.CreateIndex(
                name: "IX_Cves_PublishedAtUtc",
                table: "Cves",
                column: "PublishedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Cves_Severity",
                table: "Cves",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_PatchStatuses_AssetId",
                table: "PatchStatuses",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_PatchStatuses_CveId",
                table: "PatchStatuses",
                column: "CveId");

            migrationBuilder.CreateIndex(
                name: "IX_PatchStatuses_CveId_AssetId",
                table: "PatchStatuses",
                columns: new[] { "CveId", "AssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatchStatuses_Priority",
                table: "PatchStatuses",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_PatchStatuses_Status",
                table: "PatchStatuses",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisJobs");

            migrationBuilder.DropTable(
                name: "AnalysisResults");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "PatchStatuses");

            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "Cves");
        }
    }
}
