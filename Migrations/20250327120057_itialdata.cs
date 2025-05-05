using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Version1.Migrations
{
    /// <inheritdoc />
    public partial class itialdata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OmrResultstable",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RollNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOfBirth = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CastCategory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TypeOfSchool = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhysicallyDisabled = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TheSchoolIsLocated = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Answers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OmrResultstable", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OmrResultstable");
        }
    }
}
