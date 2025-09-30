using Microsoft.EntityFrameworkCore;
using OnboardingBuddy.Models;

namespace OnboardingBuddy.Data;

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
<p>OnboardingBuddy is your AI-powered onboarding companion designed to make your first days smooth and successful.</p>

<h3>Your Onboarding Checklist:</h3>
<ol>
<li><strong>IT Setup:</strong> Complete system access and security training</li>
<li><strong>Company Overview:</strong> Learn about our mission and values</li>
<li><strong>Role Training:</strong> Department-specific orientation</li>
<li><strong>Team Integration:</strong> Meet your colleagues and stakeholders</li>
<li><strong>Goal Setting:</strong> Establish your 30-60-90 day objectives</li>
</ol>

<h3>Key Resources:</h3>
<ul>
<li><strong>AI Assistant:</strong> Ask questions anytime for instant help</li>
<li><strong>Training Materials:</strong> Access guides and documentation</li>
<li><strong>Progress Tracking:</strong> Monitor your completion status</li>
<li><strong>Team Directory:</strong> Connect with key contacts</li>
</ul>

<h3>Quick Tips:</h3>
<p>💡 <strong>Ask Questions:</strong> Your AI assistant is available 24/7</p>
<p>📋 <strong>Track Progress:</strong> Check off completed items</p>
<p>🤝 <strong>Connect:</strong> Reach out to team members early</p>

<p><strong>Need Help?</strong> Contact HR at hr@company.com</p>
<p><em>Typical completion: 2-3 business days</em></p>",
                InternalNotes = "Primary onboarding guide - review monthly"
            },
        
            // System Prompt - Simplified and focused
            new TrainingMaterial
            {
                Id = 2,
                Title = "AI Assistant Guidelines",
                Category = "System Prompts",
                IsActive = true,
                CreatedAt = new DateTime(2025, 9, 30, 12, 0, 0, DateTimeKind.Utc),
                Content = @"You are OnboardingBuddy, an enthusiastic AI onboarding assistant.

CORE BEHAVIOR:
- Be friendly, professional, and encouraging
- Focus on onboarding tasks and company-related topics
- Guide users through their checklist step-by-step
- Ask follow-up questions to track progress
- Provide clear next steps and deadlines

CONVERSATION STYLE:
- Welcome new employees warmly
- Ask about their role to personalize guidance
- Give specific, actionable advice
- Check in regularly on task completion
- Celebrate achievements and milestones

WELCOME APPROACH:
- Introduce yourself as their dedicated assistant
- Ask about their role/department
- Present the first concrete task (IT setup)
- Set expectations for regular check-ins
- Encourage questions and provide reassurance

STAY FOCUSED:
If asked about non-work topics, gently redirect: 'Let's focus on your onboarding success! How's your progress with [current task]?'

Remember: Your goal is successful onboarding completion through consistent guidance and support.",
                InternalNotes = "Main AI behavior - concise version to avoid token limits"
            }
        );
    }
}