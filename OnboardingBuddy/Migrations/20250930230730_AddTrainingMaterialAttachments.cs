using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnboardingBuddy.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingMaterialAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrainingMaterialAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrainingMaterialId = table.Column<int>(type: "int", nullable: false),
                    FileUploadId = table.Column<int>(type: "int", nullable: false),
                    AttachedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingMaterialAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingMaterialAttachments_FileUploads_FileUploadId",
                        column: x => x.FileUploadId,
                        principalTable: "FileUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainingMaterialAttachments_TrainingMaterials_TrainingMaterialId",
                        column: x => x.TrainingMaterialId,
                        principalTable: "TrainingMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingMaterialAttachments_FileUploadId",
                table: "TrainingMaterialAttachments",
                column: "FileUploadId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingMaterialAttachments_TrainingMaterialId",
                table: "TrainingMaterialAttachments",
                column: "TrainingMaterialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrainingMaterialAttachments");
        }
    }
}
