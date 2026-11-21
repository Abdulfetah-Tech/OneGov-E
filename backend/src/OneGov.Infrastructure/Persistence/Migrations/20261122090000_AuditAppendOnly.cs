using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OneGov.Infrastructure.Persistence;

#nullable disable

namespace OneGov.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Makes audit_entries append-only inside PostgreSQL. Rows can be inserted but never updated, deleted or truncated,
    /// whatever code or SQL console is used. The hash chain still detects tampering by someone who bypasses this
    /// (for example the table owner disabling the trigger), so the two protections back each other up.
    /// Hand-written: it changes no EF model, so there is no designer file.
    /// </summary>
    [DbContext(typeof(OneGovDbContext))]
    [Migration("20261122090000_AuditAppendOnly")]
    public class AuditAppendOnly : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION audit_entries_forbid_change() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_entries is append-only: % is not allowed', TG_OP;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER audit_entries_append_only
                BEFORE UPDATE OR DELETE ON audit_entries
                FOR EACH ROW EXECUTE FUNCTION audit_entries_forbid_change();
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER audit_entries_no_truncate
                BEFORE TRUNCATE ON audit_entries
                FOR EACH STATEMENT EXECUTE FUNCTION audit_entries_forbid_change();
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS audit_entries_no_truncate ON audit_entries;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS audit_entries_append_only ON audit_entries;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS audit_entries_forbid_change();");
        }
    }
}
