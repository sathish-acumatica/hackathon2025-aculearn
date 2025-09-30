using System.ComponentModel.DataAnnotations;

namespace OnboardingBuddy.Models;

public class TrainingMaterial
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string Category { get; set; } = string.Empty;
    
    public string Content { get; set; } = string.Empty; // Rich text content
    
    public string InternalNotes { get; set; } = string.Empty; // Admin notes, not sent to AI
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation property for attachments
    public virtual ICollection<TrainingMaterialAttachment> Attachments { get; set; } = new List<TrainingMaterialAttachment>();
}

// Removed enum - simplified model for free-form content

public class TrainingMaterialRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string Category { get; set; } = string.Empty;
    
    public string Content { get; set; } = string.Empty;
    
    public string InternalNotes { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
}

public class TrainingMaterialWithAttachmentsRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string Category { get; set; } = string.Empty;
    
    public string Content { get; set; } = string.Empty;
    
    public string InternalNotes { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    public List<IFormFile>? Files { get; set; }
    
    public List<string>? AttachmentDescriptions { get; set; }
}

public class TrainingMaterialResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string InternalNotes { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<AttachmentInfo> Attachments { get; set; } = new();
}

public class AttachmentInfo
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTime AttachedAt { get; set; }
    public string? Description { get; set; }
    public bool IsProcessed { get; set; }
}