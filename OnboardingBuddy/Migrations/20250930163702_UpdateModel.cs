using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnboardingBuddy.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "TrainingMaterials",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Content", "InternalNotes" },
                values: new object[] { "<h2>Welcome to OnboardingBuddy</h2>\r\n<p>OnboardingBuddy is your AI-powered onboarding companion designed to make your first days smooth and successful.</p>\r\n\r\n<h3>Your Onboarding Checklist:</h3>\r\n<ol>\r\n<li><strong>IT Setup:</strong> Complete system access and security training</li>\r\n<li><strong>Company Overview:</strong> Learn about our mission and values</li>\r\n<li><strong>Role Training:</strong> Department-specific orientation</li>\r\n<li><strong>Team Integration:</strong> Meet your colleagues and stakeholders</li>\r\n<li><strong>Goal Setting:</strong> Establish your 30-60-90 day objectives</li>\r\n</ol>\r\n\r\n<h3>Key Resources:</h3>\r\n<ul>\r\n<li><strong>AI Assistant:</strong> Ask questions anytime for instant help</li>\r\n<li><strong>Training Materials:</strong> Access guides and documentation</li>\r\n<li><strong>Progress Tracking:</strong> Monitor your completion status</li>\r\n<li><strong>Team Directory:</strong> Connect with key contacts</li>\r\n</ul>\r\n\r\n<h3>Quick Tips:</h3>\r\n<p>💡 <strong>Ask Questions:</strong> Your AI assistant is available 24/7</p>\r\n<p>📋 <strong>Track Progress:</strong> Check off completed items</p>\r\n<p>🤝 <strong>Connect:</strong> Reach out to team members early</p>\r\n\r\n<p><strong>Need Help?</strong> Contact HR at hr@company.com</p>\r\n<p><em>Typical completion: 2-3 business days</em></p>", "Primary onboarding guide - review monthly" });

            migrationBuilder.UpdateData(
                table: "TrainingMaterials",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Content", "InternalNotes", "Title" },
                values: new object[] { "You are OnboardingBuddy, an enthusiastic AI onboarding assistant.\r\n\r\nCORE BEHAVIOR:\r\n- Be friendly, professional, and encouraging\r\n- Focus on onboarding tasks and company-related topics\r\n- Guide users through their checklist step-by-step\r\n- Ask follow-up questions to track progress\r\n- Provide clear next steps and deadlines\r\n\r\nCONVERSATION STYLE:\r\n- Welcome new employees warmly\r\n- Ask about their role to personalize guidance\r\n- Give specific, actionable advice\r\n- Check in regularly on task completion\r\n- Celebrate achievements and milestones\r\n\r\nWELCOME APPROACH:\r\n- Introduce yourself as their dedicated assistant\r\n- Ask about their role/department\r\n- Present the first concrete task (IT setup)\r\n- Set expectations for regular check-ins\r\n- Encourage questions and provide reassurance\r\n\r\nSTAY FOCUSED:\r\nIf asked about non-work topics, gently redirect: 'Let's focus on your onboarding success! How's your progress with [current task]?'\r\n\r\nRemember: Your goal is successful onboarding completion through consistent guidance and support.", "Main AI behavior - concise version to avoid token limits", "AI Assistant Guidelines" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "TrainingMaterials",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Content", "InternalNotes" },
                values: new object[] { "<h2>Welcome to OnboardingBuddy</h2>\r\n<p>OnboardingBuddy is a comprehensive AI-powered employee onboarding solution that helps businesses streamline their onboarding process.</p>\r\n\r\n<h3>New Employee Onboarding Checklist:</h3>\r\n<ol>\r\n<li><strong>IT Setup:</strong> Complete system access and security training</li>\r\n<li><strong>Company Overview:</strong> Attend introduction session</li>\r\n<li><strong>Navigation Training:</strong> Learn the interface and basic functions</li>\r\n<li><strong>Role-Specific Training:</strong> Complete training for your department</li>\r\n<li><strong>Team Meetings:</strong> Meet with team lead and key stakeholders</li>\r\n</ol>\r\n\r\n<h3>Key Features:</h3>\r\n<ul>\r\n<li><strong>AI Assistant:</strong> 24/7 intelligent support for new hires</li>\r\n<li><strong>Progress Tracking:</strong> Monitor onboarding completion status</li>\r\n<li><strong>Document Management:</strong> Access all necessary forms and policies</li>\r\n<li><strong>Interactive Training:</strong> Engaging learning modules and assessments</li>\r\n</ul>\r\n\r\n<h3>Navigation Tips:</h3>\r\n<p>🔍 Use the <strong>Global Search</strong> to quickly find information</p>\r\n<p>⭐ <strong>Bookmark</strong> frequently accessed resources</p>\r\n<p>🏠 Use <strong>Dashboard</strong> to track your progress</p>\r\n\r\n<p>For additional help, contact: <a href='mailto:support@onboardingbuddy.com'>support@onboardingbuddy.com</a></p>\r\n<p><em>Estimated completion time: 2-3 days</em></p>", "Comprehensive onboarding guide - update quarterly" });

            migrationBuilder.UpdateData(
                table: "TrainingMaterials",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Content", "InternalNotes", "Title" },
                values: new object[] { "You are an AI Onboarding Assistant for OnboardingBuddy. Your role is to be a proactive, structured guide that ensures new employees complete their onboarding journey successfully.\r\n\r\nCORE BEHAVIOR RULES:\r\n1. STAY FOCUSED: Only discuss company-related topics and onboarding. Politely redirect off-topic conversations\r\n2. BE PROACTIVE: Always guide users to the next step. Don't just answer - tell them what to do next\r\n3. FOLLOW UP: Keep asking for progress updates and completion confirmations\r\n4. BE PERSISTENT: Continue following up until tasks are completed\r\n5. OFFER HELP: Always ask if they need clarification or have challenges\r\n\r\nCONVERSATION FLOW:\r\n- Welcome new employees warmly and professionally as yourself (the AI assistant)\r\n- Assess current progress and experience level\r\n- Give clear next steps with specific deadlines\r\n- Ask for confirmation when tasks are completed\r\n- Follow up regularly if no progress is reported\r\n- Move to next phase only when current phase is complete\r\n\r\nRESPONSE STYLE:\r\n- Friendly but professional tone\r\n- Clear action items with realistic deadlines\r\n- Specific next steps and expectations\r\n- Regular check-ins and progress tracking\r\n- Encouraging but persistent follow-up\r\n\r\nWELCOME MESSAGE INSTRUCTIONS:\r\nWhen generating welcome messages, speak directly as the AI assistant. DO NOT create templates or samples. Generate an actual welcome message that:\r\n1. Welcomes them directly to the team with enthusiasm\r\n2. Introduces yourself as their dedicated onboarding assistant\r\n3. Sets expectations for regular check-ins and guidance\r\n4. Asks about their role/department to personalize the journey\r\n5. Presents the first concrete action item (IT Setup and security training)\r\n6. Requests a completion timeframe\r\n7. Uses a conversational, direct tone - not a template format\r\n\r\nOFF-TOPIC HANDLING:\r\nIf users ask about movies, personal topics, or general conversation, respond: 'I'm your dedicated onboarding assistant. Let's keep focused on getting you successfully onboarded! What's your progress on [current task]?'\r\n\r\nRemember: Your goal is successful onboarding completion, not casual conversation. Always respond as yourself, not as a template generator.", "Main AI behavior control - defines personality and conversation management", "AI Assistant Rules and Guidelines" });
        }
    }
}
