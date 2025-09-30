using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OnboardingBuddy.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileUploads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false),
                    ProcessedContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcessingError = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileUploads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainingMaterials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    InternalNotes = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingMaterials", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "TrainingMaterials",
                columns: new[] { "Id", "Category", "Content", "CreatedAt", "InternalNotes", "IsActive", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "Training Materials", "<h2>Welcome to OnboardingBuddy</h2>\r\n<p>OnboardingBuddy is your AI-powered onboarding companion designed to make your first days smooth and successful.</p>\r\n\r\n<h3>Your Onboarding Checklist:</h3>\r\n<ol>\r\n<li><strong>IT Setup:</strong> Complete system access and security training</li>\r\n<li><strong>Company Overview:</strong> Learn about our mission and values</li>\r\n<li><strong>Role Training:</strong> Department-specific orientation</li>\r\n<li><strong>Team Integration:</strong> Meet your colleagues and stakeholders</li>\r\n<li><strong>Goal Setting:</strong> Establish your 30-60-90 day objectives</li>\r\n</ol>\r\n\r\n<h3>Key Resources:</h3>\r\n<ul>\r\n<li><strong>AI Assistant:</strong> Ask questions anytime for instant help</li>\r\n<li><strong>Training Materials:</strong> Access guides and documentation</li>\r\n<li><strong>Progress Tracking:</strong> Monitor your completion status</li>\r\n<li><strong>Team Directory:</strong> Connect with key contacts</li>\r\n</ul>\r\n\r\n<h3>Quick Tips:</h3>\r\n<p>💡 <strong>Ask Questions:</strong> Your AI assistant is available 24/7</p>\r\n<p>📋 <strong>Track Progress:</strong> Check off completed items</p>\r\n<p>🤝 <strong>Connect:</strong> Reach out to team members early</p>\r\n\r\n<p><strong>Need Help?</strong> Contact HR at hr@company.com</p>\r\n<p><em>Typical completion: 2-3 business days</em></p>", new DateTime(2025, 9, 30, 12, 0, 0, 0, DateTimeKind.Utc), "Primary onboarding guide - review monthly", true, "Onboarding Buddy Guide", null },
                    { 2, "System Prompts", "You are OnboardingBuddy, an enthusiastic AI onboarding assistant.\r\n\r\nCORE BEHAVIOR:\r\n- Be friendly, professional, and encouraging\r\n- Focus on onboarding tasks and company-related topics\r\n- Guide users through their checklist step-by-step\r\n- Ask follow-up questions to track progress\r\n- Provide clear next steps and deadlines\r\n\r\nCONVERSATION STYLE:\r\n- Welcome new employees warmly\r\n- Ask about their role to personalize guidance\r\n- Give specific, actionable advice\r\n- Check in regularly on task completion\r\n- Celebrate achievements and milestones\r\n\r\nWELCOME APPROACH:\r\n- Introduce yourself as their dedicated assistant\r\n- Ask about their role/department\r\n- Present the first concrete task (IT setup)\r\n- Set expectations for regular check-ins\r\n- Encourage questions and provide reassurance\r\n\r\nSTAY FOCUSED:\r\nIf asked about non-work topics, gently redirect: 'Let's focus on your onboarding success! How's your progress with [current task]?'\r\n\r\nRemember: Your goal is successful onboarding completion through consistent guidance and support.", new DateTime(2025, 9, 30, 12, 0, 0, 0, DateTimeKind.Utc), "Main AI behavior - concise version to avoid token limits", true, "AI Assistant Guidelines", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileUploads_SessionId",
                table: "FileUploads",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_FileUploads_UploadedAt",
                table: "FileUploads",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingMaterials_Category",
                table: "TrainingMaterials",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingMaterials_IsActive",
                table: "TrainingMaterials",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingMaterials_Title",
                table: "TrainingMaterials",
                column: "Title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileUploads");

            migrationBuilder.DropTable(
                name: "TrainingMaterials");
        }
    }
}
