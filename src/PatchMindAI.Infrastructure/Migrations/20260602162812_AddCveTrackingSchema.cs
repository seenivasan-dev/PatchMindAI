using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatchMindAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCveTrackingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Hostname = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    OperatingSystem = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Criticality = table.Column<string>(type: "TEXT", nullable: false),
                    Environment = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsInternetFacing = table.Column<bool>(type: "INTEGER", nullable: false),
                    InstalledSoftware = table.Column<string>(type: "TEXT", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BusinessUnit = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastScannedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cves",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: false),
                    BaseScore = table.Column<double>(type: "REAL", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: false),
                    VectorString = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Weaknesses = table.Column<string>(type: "TEXT", nullable: false),
                    AffectedProducts = table.Column<string>(type: "TEXT", nullable: false),
                    References = table.Column<string>(type: "TEXT", nullable: false),
                    SyncedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cves", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatchStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CveId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PatchedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PatchVersion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Priority = table.Column<string>(type: "TEXT", nullable: false),
                    TargetPatchDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AssignedTo = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
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
                name: "PatchStatuses");

            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "Cves");
        }
    }
}
