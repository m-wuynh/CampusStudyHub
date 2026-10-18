using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGroupMeetingLinkAndMakeCapacityOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactUrl",
                table: "StudyGroups");

            migrationBuilder.AlterColumn<short>(
                name: "MaxMembers",
                table: "StudyGroups",
                type: "smallint",
                nullable: true,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldDefaultValue: (short)20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE dbo.StudyGroups SET MaxMembers = 20 WHERE MaxMembers IS NULL;");

            migrationBuilder.AlterColumn<short>(
                name: "MaxMembers",
                table: "StudyGroups",
                type: "smallint",
                nullable: false,
                defaultValue: (short)20,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactUrl",
                table: "StudyGroups",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }
    }
}
