using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KulturHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeLogCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "category",
                table: "change_logs",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "category",
                table: "change_logs");
        }
    }
}
