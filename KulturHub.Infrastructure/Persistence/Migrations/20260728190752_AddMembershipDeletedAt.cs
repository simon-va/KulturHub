using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KulturHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipDeletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "memberships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_memberships_organisation_id_user_id_is_deleted",
                table: "memberships",
                columns: new[] { "organisation_id", "user_id", "is_deleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memberships_organisation_id_user_id_is_deleted",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "memberships");
        }
    }
}
