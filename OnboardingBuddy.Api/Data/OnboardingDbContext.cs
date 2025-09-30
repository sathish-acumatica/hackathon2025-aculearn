using Microsoft.EntityFrameworkCore;
using OnboardingBuddy.Api.Models;

namespace OnboardingBuddy.Api.Data;

public class OnboardingDbContext : DbContext
{
    public OnboardingDbContext(DbContextOptions<OnboardingDbContext> options) : base(options)
    {
    }

    public DbSet<TrainingMaterial> TrainingMaterials { get; set; }
    public DbSet<FileUpload> FileUploads { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure TrainingMaterial entity
        modelBuilder.Entity<TrainingMaterial>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);
                
            entity.Property(e => e.Category)
                .IsRequired()
                .HasMaxLength(100);
                
            entity.Property(e => e.Content)
                .HasColumnType("TEXT"); // For rich text content
                
            entity.Property(e => e.InternalNotes)
                .HasColumnType("TEXT");
                
            entity.Property(e => e.CreatedAt)
                .IsRequired();
                
            entity.HasIndex(e => e.Title);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.IsActive);
        });

        // Configure FileUpload entity
        modelBuilder.Entity<FileUpload>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.FileName)
                .IsRequired()
                .HasMaxLength(255);
                
            entity.Property(e => e.OriginalFileName)
                .IsRequired()
                .HasMaxLength(255);
                
            entity.Property(e => e.ContentType)
                .IsRequired()
                .HasMaxLength(100);
                
            entity.Property(e => e.FilePath)
                .IsRequired()
                .HasMaxLength(500);
                
            entity.Property(e => e.SessionId)
                .HasMaxLength(100);
                
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.UploadedAt);
        });

        // Seed initial data
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TrainingMaterial>().HasData(
            // Training Material - Keep one comprehensive onboarding guide
            new TrainingMaterial
            {
                Id = 1,
                Title = "Onboarding Buddy Guide",
                Category = "Training Materials",
                IsActive = true,
                CreatedAt = new DateTime(2025, 9, 30, 12, 0, 0, DateTimeKind.Utc),
                Content = @"<h2>Welcome to OnboardingBuddy</h2>
<p>OnboardingBuddy is a comprehensive AI-powered employee onboarding solution that helps businesses streamline their onboarding process.</p>

<h3>New Employee Onboarding Checklist:</h3>
<ol>
<li><strong>IT Setup:</strong> Complete system access and security training</li>
<li><strong>Company Overview:</strong> Attend introduction session</li>
<li><strong>Navigation Training:</strong> Learn the interface and basic functions</li>
<li><strong>Role-Specific Training:</strong> Complete training for your department</li>
<li><strong>Team Meetings:</strong> Meet with team lead and key stakeholders</li>
</ol>

<h3>Key Features:</h3>
<ul>
<li><strong>AI Assistant:</strong> 24/7 intelligent support for new hires</li>
<li><strong>Progress Tracking:</strong> Monitor onboarding completion status</li>
<li><strong>Document Management:</strong> Access all necessary forms and policies</li>
<li><strong>Interactive Training:</strong> Engaging learning modules and assessments</li>
</ul>

<h3>Navigation Tips:</h3>
<p>🔍 Use the <strong>Global Search</strong> to quickly find information</p>
<p>⭐ <strong>Bookmark</strong> frequently accessed resources</p>
<p>🏠 Use <strong>Dashboard</strong> to track your progress</p>

<p>For additional help, contact: <a href='mailto:support@onboardingbuddy.com'>support@onboardingbuddy.com</a></p>
<p><em>Estimated completion time: 2-3 days</em></p>",
                InternalNotes = "Comprehensive onboarding guide - update quarterly"
            },
        
            // System Prompt - AI behavior and rules
            new TrainingMaterial
            {
                Id = 2,
                Title = "AI Assistant Rules and Guidelines",
                Category = "System Prompts",
                IsActive = true,
                CreatedAt = new DateTime(2025, 9, 30, 12, 0, 0, DateTimeKind.Utc),
                Content = @"You are an AI Onboarding Assistant for OnboardingBuddy. Your role is to be a proactive, structured guide that ensures new employees complete their onboarding journey successfully.

CORE BEHAVIOR RULES:
1. STAY FOCUSED: Only discuss company-related topics and onboarding. Politely redirect off-topic conversations
2. BE PROACTIVE: Always guide users to the next step. Don't just answer - tell them what to do next
3. FOLLOW UP: Keep asking for progress updates and completion confirmations
4. BE PERSISTENT: Continue following up until tasks are completed
5. OFFER HELP: Always ask if they need clarification or have challenges

CONVERSATION FLOW:
- Welcome new employees warmly and professionally as yourself (the AI assistant)
- Assess current progress and experience level
- Give clear next steps with specific deadlines
- Ask for confirmation when tasks are completed
- Follow up regularly if no progress is reported
- Move to next phase only when current phase is complete

RESPONSE STYLE:
- Friendly but professional tone
- Clear action items with realistic deadlines
- Specific next steps and expectations
- Regular check-ins and progress tracking
- Encouraging but persistent follow-up

WELCOME MESSAGE INSTRUCTIONS:
When generating welcome messages, speak directly as the AI assistant. DO NOT create templates or samples. Generate an actual welcome message that:
1. Welcomes them directly to the team with enthusiasm
2. Introduces yourself as their dedicated onboarding assistant
3. Sets expectations for regular check-ins and guidance
4. Asks about their role/department to personalize the journey
5. Presents the first concrete action item (IT Setup and security training)
6. Requests a completion timeframe
7. Uses a conversational, direct tone - not a template format

OFF-TOPIC HANDLING:
If users ask about movies, personal topics, or general conversation, respond: 'I'm your dedicated onboarding assistant. Let's keep focused on getting you successfully onboarded! What's your progress on [current task]?'

Remember: Your goal is successful onboarding completion, not casual conversation. Always respond as yourself, not as a template generator.",
                InternalNotes = "Main AI behavior control - defines personality and conversation management"
            }
        );
    }
}