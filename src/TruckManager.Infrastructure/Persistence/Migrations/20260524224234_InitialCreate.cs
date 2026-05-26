using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TruckManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TruckDomainEvents",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TruckDomainEvents", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "Trucks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trucks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TruckStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TruckStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TruckStatusTransitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    FromStatusId = table.Column<int>(type: "integer", nullable: false),
                    ToStatusId = table.Column<int>(type: "integer", nullable: false),
                    IsAllowed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TruckStatusTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TruckStatusTransitions_TruckStatuses_FromStatusId",
                        column: x => x.FromStatusId,
                        principalTable: "TruckStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TruckStatusTransitions_TruckStatuses_ToStatusId",
                        column: x => x.ToStatusId,
                        principalTable: "TruckStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "TruckStatuses",
                columns: new[] { "Id", "Code", "IsActive", "IsSystem", "Name", "Sequence" },
                values: new object[,]
                {
                    { 1, "OUT_OF_SERVICE", true, true, "Out of Service", 1 },
                    { 2, "LOADING", true, true, "Loading", 2 },
                    { 3, "TO_JOB", true, true, "To Job", 3 },
                    { 4, "AT_JOB", true, true, "At Job", 4 },
                    { 5, "RETURNING", true, true, "Returning", 5 }
                });

            migrationBuilder.InsertData(
                table: "TruckStatusTransitions",
                columns: new[] { "Id", "FromStatusId", "IsAllowed", "ToStatusId" },
                values: new object[,]
                {
                    { 1, 1, true, 2 },
                    { 2, 2, true, 1 },
                    { 3, 1, true, 3 },
                    { 4, 3, true, 1 },
                    { 5, 1, true, 4 },
                    { 6, 4, true, 1 },
                    { 7, 1, true, 5 },
                    { 8, 5, true, 1 },
                    { 9, 2, true, 3 },
                    { 10, 3, true, 4 },
                    { 11, 4, true, 5 },
                    { 12, 5, true, 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TruckDomainEvents_AggregateId_AggregateVersion",
                table: "TruckDomainEvents",
                columns: new[] { "AggregateId", "AggregateVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_TruckDomainEvents_TenantId_OccurredAtUtc",
                table: "TruckDomainEvents",
                columns: new[] { "TenantId", "OccurredAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Trucks_TenantId_StatusId",
                table: "Trucks",
                columns: new[] { "TenantId", "StatusId" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_Trucks_TenantId_Code",
                table: "Trucks",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_TruckStatuses_Code",
                table: "TruckStatuses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TruckStatusTransitions_ToStatusId",
                table: "TruckStatusTransitions",
                column: "ToStatusId");

            migrationBuilder.CreateIndex(
                name: "UX_TruckStatusTransitions_From_To",
                table: "TruckStatusTransitions",
                columns: new[] { "FromStatusId", "ToStatusId" },
                unique: true);

            // Two FKs hand-added below — they are NOT declared in the EF model because EF
            // Core requires the FK property and the principal PK to share the same CLR
            // type (value converters only align column types). Adding them here keeps the
            // database constraints intact (defense-in-depth referential integrity per
            // ADR-0025 / database.md §4.3) without polluting the aggregate model.
            //   - Trucks.StatusId (int after enum conversion) → TruckStatuses.Id (int)
            //   - TruckDomainEvents.AggregateId (raw Guid)   → Trucks.Id (TruckId VO)

            migrationBuilder.AddForeignKey(
                name: "FK_Trucks_TruckStatuses_StatusId",
                table: "Trucks",
                column: "StatusId",
                principalTable: "TruckStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TruckDomainEvents_Trucks_AggregateId",
                table: "TruckDomainEvents",
                column: "AggregateId",
                principalTable: "Trucks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // [ADR-0003 / Section G fix]   The TruckDomainEvents → Trucks FK is **not**
            // declared in the EF model (CLR-type mismatch — see the comment block above),
            // so EF Core has no knowledge of the dependency and may order the per-batch
            // INSERTs arbitrarily on the dual-write Create path. Marking the FK as
            // DEFERRABLE INITIALLY DEFERRED lets Postgres validate at commit time instead
            // of per-row, which matches the atomic state-plus-events invariant we already
            // get from the same DB transaction.
            migrationBuilder.Sql(
                "ALTER TABLE \"TruckDomainEvents\" " +
                "ALTER CONSTRAINT \"FK_TruckDomainEvents_Trucks_AggregateId\" DEFERRABLE INITIALLY DEFERRED;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TruckDomainEvents");

            migrationBuilder.DropTable(
                name: "Trucks");

            migrationBuilder.DropTable(
                name: "TruckStatusTransitions");

            migrationBuilder.DropTable(
                name: "TruckStatuses");
        }
    }
}
