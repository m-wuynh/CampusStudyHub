using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AllowDevelopmentAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ExternalLogins_Provider",
                table: "ExternalLogins");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExternalLogins_Provider",
                table: "ExternalLogins",
                sql: "[Provider] IN ('Google', 'Development')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ExternalLogins_Provider",
                table: "ExternalLogins");

            migrationBuilder.Sql(
                "DELETE FROM [ExternalLogins] WHERE [Provider] = 'Development';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExternalLogins_Provider",
                table: "ExternalLogins",
                sql: "[Provider] = 'Google'");
        }
    }
}
