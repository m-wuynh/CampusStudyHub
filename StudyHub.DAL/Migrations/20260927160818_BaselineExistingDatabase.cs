using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations;

/// <summary>
/// Registers the existing Database First schema as the EF Core migration baseline.
/// The database must first be created with the base SQL scripts.
/// </summary>
public partial class BaselineExistingDatabase : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty: the existing schema is the baseline.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A baseline migration must never remove the existing database schema.
    }
}
