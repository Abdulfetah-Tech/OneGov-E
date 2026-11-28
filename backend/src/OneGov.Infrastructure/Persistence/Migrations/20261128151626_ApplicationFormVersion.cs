using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneGov.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationFormVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "form_version",
                table: "applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "form_version",
                table: "applications");
        }
    }
}
