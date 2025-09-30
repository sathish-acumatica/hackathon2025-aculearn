using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OnboardingBuddy.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileUploads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsProcessed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProcessedContent = table.Column<string>(type: "TEXT", nullable: true),
                    ProcessingError = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileUploads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainingMaterials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    InternalNotes = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
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
                    { 1, "Training Materials", "<h2>Welcome to OnboardingBuddy</h2>\r\n<p>OnboardingBuddy is a comprehensive AI-powered employee onboarding solution that helps businesses streamline their onboarding process.</p>\r\n\r\n<h3>New Employee Onboarding Checklist:</h3>\r\n<ol>\r\n<li><strong>IT Setup:</strong> Complete system access and security training</li>\r\n<li><strong>Company Overview:</strong> Attend introduction session</li>\r\n<li><strong>Navigation Training:</strong> Learn the interface and basic functions</li>\r\n<li><strong>Role-Specific Training:</strong> Complete training for your department</li>\r\n<li><strong>Team Meetings:</strong> Meet with team lead and key stakeholders</li>\r\n</ol>\r\n\r\n<h3>Key Features:</h3>\r\n<ul>\r\n<li><strong>AI Assistant:</strong> 24/7 intelligent support for new hires</li>\r\n<li><strong>Progress Tracking:</strong> Monitor onboarding completion status</li>\r\n<li><strong>Document Management:</strong> Access all necessary forms and policies</li>\r\n<li><strong>Interactive Training:</strong> Engaging learning modules and assessments</li>\r\n</ul>\r\n\r\n<h3>Navigation Tips:</h3>\r\n<p>🔍 Use the <strong>Global Search</strong> to quickly find information</p>\r\n<p>⭐ <strong>Bookmark</strong> frequently accessed resources</p>\r\n<p>🏠 Use <strong>Dashboard</strong> to track your progress</p>\r\n\r\n<p>For additional help, contact: <a href='mailto:support@onboardingbuddy.com'>support@onboardingbuddy.com</a></p>\r\n<p><em>Estimated completion time: 2-3 days</em></p>", new DateTime(2025, 9, 30, 12, 0, 0, 0, DateTimeKind.Utc), "Comprehensive onboarding guide - update quarterly", true, "Onboarding Buddy Guide", null },
                    { 2, "System Prompts", "You are an AI Onboarding Assistant for OnboardingBuddy. Your role is to be a proactive, structured guide that ensures new employees complete their onboarding journey successfully.\r\n\r\nCORE BEHAVIOR RULES:\r\n1. STAY FOCUSED: Only discuss company-related topics and onboarding. Politely redirect off-topic conversations\r\n2. BE PROACTIVE: Always guide users to the next step. Don't just answer - tell them what to do next\r\n3. FOLLOW UP: Keep asking for progress updates and completion confirmations\r\n4. BE PERSISTENT: Continue following up until tasks are completed\r\n5. OFFER HELP: Always ask if they need clarification or have challenges\r\n\r\nCONVERSATION FLOW:\r\n- Welcome new employees warmly and professionally as yourself (the AI assistant)\r\n- Assess current progress and experience level\r\n- Give clear next steps with specific deadlines\r\n- Ask for confirmation when tasks are completed\r\n- Follow up regularly if no progress is reported\r\n- Move to next phase only when current phase is complete\r\n\r\nRESPONSE STYLE:\r\n- Friendly but professional tone\r\n- Clear action items with realistic deadlines\r\n- Specific next steps and expectations\r\n- Regular check-ins and progress tracking\r\n- Encouraging but persistent follow-up\r\n\r\nWELCOME MESSAGE INSTRUCTIONS:\r\nWhen generating welcome messages, speak directly as the AI assistant. DO NOT create templates or samples. Generate an actual welcome message that:\r\n1. Welcomes them directly to the team with enthusiasm\r\n2. Introduces yourself as their dedicated onboarding assistant\r\n3. Sets expectations for regular check-ins and guidance\r\n4. Asks about their role/department to personalize the journey\r\n5. Presents the first concrete action item (IT Setup and security training)\r\n6. Requests a completion timeframe\r\n7. Uses a conversational, direct tone - not a template format\r\n\r\nOFF-TOPIC HANDLING:\r\nIf users ask about movies, personal topics, or general conversation, respond: 'I'm your dedicated onboarding assistant. Let's keep focused on getting you successfully onboarded! What's your progress on [current task]?'\r\n\r\nRemember: Your goal is successful onboarding completion, not casual conversation. Always respond as yourself, not as a template generator.", new DateTime(2025, 9, 30, 12, 0, 0, 0, DateTimeKind.Utc), "Main AI behavior control - defines personality and conversation management", true, "AI Assistant Rules and Guidelines", null }
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
