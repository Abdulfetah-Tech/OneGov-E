using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneGov.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_am = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_om = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agencies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    occurred_at_unix_ms = table.Column<long>(type: "bigint", nullable: false),
                    actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    previous_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_entries", x => x.sequence);
                });

            migrationBuilder.CreateTable(
                name: "gov_services",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_am = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_om = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    description_am = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    description_om = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    workflow_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gov_services", x => x.id);
                    table.ForeignKey(
                        name: "fk_gov_services_agencies_agency_id",
                        column: x => x.agency_id,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    citizen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    workflow_version = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    reference_number = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications", x => x.id);
                    table.ForeignKey(
                        name: "fk_applications_agencies_agency_id",
                        column: x => x.agency_id,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_applications_gov_services_service_id",
                        column: x => x.service_id,
                        principalTable: "gov_services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_requirements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    text_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    text_am = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    text_om = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_requirements", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_requirements_gov_services_service_id",
                        column: x => x.service_id,
                        principalTable: "gov_services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transition = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    from_state = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    to_state = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_application_history_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agencies_code",
                table: "agencies",
                column: "code",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_application_history_application_id_occurred_at",
                table: "application_history",
                columns: new[] { "application_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_applications_agency_id_state_created_at",
                table: "applications",
                columns: new[] { "agency_id", "state", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_applications_citizen_id_created_at",
                table: "applications",
                columns: new[] { "citizen_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_applications_reference_number",
                table: "applications",
                column: "reference_number",
                unique: true,
                filter: "reference_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_applications_service_id",
                table: "applications",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_actor_occurred_at_unix_ms",
                table: "audit_entries",
                columns: new[] { "actor", "occurred_at_unix_ms" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_hash",
                table: "audit_entries",
                column: "hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_gov_services_agency_id",
                table: "gov_services",
                column: "agency_id");

            migrationBuilder.CreateIndex(
                name: "ix_gov_services_category_status",
                table: "gov_services",
                columns: new[] { "category", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_gov_services_code",
                table: "gov_services",
                column: "code",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_service_requirements_service_id_order",
                table: "service_requirements",
                columns: new[] { "service_id", "order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_history");

            migrationBuilder.DropTable(
                name: "audit_entries");

            migrationBuilder.DropTable(
                name: "service_requirements");

            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.DropTable(
                name: "gov_services");

            migrationBuilder.DropTable(
                name: "agencies");
        }
    }
}
