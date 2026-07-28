using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KulturHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDecidedAtAndInvitedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "invited_at",
            table: "memberships",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql(
            "UPDATE memberships SET invited_at = joined_at;");

        migrationBuilder.AlterColumn<DateTime>(
            name: "invited_at",
            table: "memberships",
            type: "timestamp with time zone",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "timestamp with time zone",
            oldNullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "decided_at",
            table: "memberships",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql(
            "UPDATE memberships SET decided_at = joined_at WHERE status IN (1, 2);");

        migrationBuilder.AlterColumn<DateTime>(
            name: "joined_at",
            table: "memberships",
            type: "timestamp with time zone",
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "timestamp with time zone");

        migrationBuilder.Sql(
            "UPDATE memberships SET joined_at = NULL;");

        migrationBuilder.DropColumn(
            name: "joined_at",
            table: "memberships");
    }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "joined_at",
                table: "memberships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE memberships SET joined_at = decided_at WHERE decided_at IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "decided_at",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "invited_at",
                table: "memberships");

            migrationBuilder.AlterColumn<DateTime>(
                name: "joined_at",
                table: "memberships",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}