using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sip.be.Infratructure.Persistence.SignTemplateMigrations
{
    /// <inheritdoc />
    public partial class InitSignTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SignTemplate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenTemplate = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Thumbprint = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TenChungThu = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LyDoKy = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    NoiKy = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    AnhDauDoPath = table.Column<string>(type: "TEXT", nullable: true),
                    AnhDauDoNguonPath = table.Column<string>(type: "TEXT", nullable: true),
                    AnhChuKyTuoiPath = table.Column<string>(type: "TEXT", nullable: true),
                    AnhChuKyTuoiNguonPath = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignTemplate", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignTemplatePosition",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SignTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    PageNumber = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    XRatio = table.Column<double>(type: "REAL", nullable: false),
                    YRatio = table.Column<double>(type: "REAL", nullable: false),
                    WidthRatio = table.Column<double>(type: "REAL", nullable: false),
                    HeightRatio = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignTemplatePosition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignTemplatePosition_SignTemplate_SignTemplateId",
                        column: x => x.SignTemplateId,
                        principalTable: "SignTemplate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SignTemplatePosition_SignTemplateId_Kind",
                table: "SignTemplatePosition",
                columns: new[] { "SignTemplateId", "Kind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignTemplatePosition");

            migrationBuilder.DropTable(
                name: "SignTemplate");
        }
    }
}
