using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.DAL.Migrations;

public partial class ConvertNotesV3PageContentToPlainText : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_NotesV3Pages_DeltaJson",
            table: "NotesV3Pages");

        migrationBuilder.AddColumn<string>(
            name: "ContentText",
            table: "NotesV3Pages",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE pageRow
            SET ContentText = COALESCE(converted.ContentText, N'')
            FROM NotesV3Pages AS pageRow
            OUTER APPLY
            (
                SELECT STRING_AGG(
                    operation.[Insert],
                    N'') WITHIN GROUP (ORDER BY TRY_CONVERT(int, jsonOperation.[key])) AS ContentText
                FROM OPENJSON(pageRow.ContentDeltaJson, '$.ops') AS jsonOperation
                CROSS APPLY OPENJSON(jsonOperation.[value])
                    WITH ([Insert] nvarchar(max) '$.insert') AS operation
                WHERE operation.[Insert] IS NOT NULL
            ) AS converted;

            UPDATE NotesV3Pages
            SET ContentText = LEFT(ContentText, LEN(ContentText) - 1)
            WHERE RIGHT(ContentText, 1) = NCHAR(10);
            """);

        migrationBuilder.AlterColumn<string>(
            name: "ContentText",
            table: "NotesV3Pages",
            type: "nvarchar(max)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "ContentDeltaJson",
            table: "NotesV3Pages");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ContentDeltaJson",
            table: "NotesV3Pages",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE NotesV3Pages
            SET ContentDeltaJson = N'{"ops":[{"insert":"' +
                STRING_ESCAPE(ContentText + NCHAR(10), 'json') +
                N'"}]}';
            """);

        migrationBuilder.AlterColumn<string>(
            name: "ContentDeltaJson",
            table: "NotesV3Pages",
            type: "nvarchar(max)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldNullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_NotesV3Pages_DeltaJson",
            table: "NotesV3Pages",
            sql: "ISJSON([ContentDeltaJson]) = 1");

        migrationBuilder.DropColumn(
            name: "ContentText",
            table: "NotesV3Pages");
    }
}
