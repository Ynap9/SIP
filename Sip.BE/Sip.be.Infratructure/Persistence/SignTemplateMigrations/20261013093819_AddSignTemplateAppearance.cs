using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sip.be.Infratructure.Persistence.SignTemplateMigrations
{
    /// <inheritdoc />
    public partial class AddSignTemplateAppearance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CoChuKySo",
                table: "SignTemplate",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FontChuKySo",
                table: "SignTemplate",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MauChuKySo",
                table: "SignTemplate",
                type: "TEXT",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoChuKySo",
                table: "SignTemplate");

            migrationBuilder.DropColumn(
                name: "FontChuKySo",
                table: "SignTemplate");

            migrationBuilder.DropColumn(
                name: "MauChuKySo",
                table: "SignTemplate");
        }
    }
}
