using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneGov.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "application_reference_seq");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "application_reference_seq");
        }
    }
}
