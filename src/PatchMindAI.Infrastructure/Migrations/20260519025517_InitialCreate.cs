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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CveId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    UserQuery = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RiskScore = table.Column<double>(type: "REAL", nullable: false),
                    RiskJustification = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ImpactSummary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    AffectedAssetsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RemediationStepsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RawAgentOutputJson = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisResults", x => x.Id);
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisJobs");

            migrationBuilder.DropTable(
                name: "AnalysisResults");
        }
    }
}
