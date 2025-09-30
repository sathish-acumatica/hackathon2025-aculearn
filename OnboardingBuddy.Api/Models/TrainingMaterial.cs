using System.ComponentModel.DataAnnotations;

namespace OnboardingBuddy.Api.Models;

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