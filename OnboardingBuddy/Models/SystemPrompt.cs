using System.ComponentModel.DataAnnotations;

namespace OnboardingBuddy.Models;

public class SystemPrompt
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public string Type { get; set; } = string.Empty; // "welcome", "system", "instruction"
    
    public string Content { get; set; } = string.Empty; // The prompt content
    
    public string Description { get; set; } = string.Empty; // Admin description
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
}

public class SystemPromptRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public string Type { get; set; } = string.Empty;
    
    public string Content { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
}