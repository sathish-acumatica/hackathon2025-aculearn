using System.ComponentModel.DataAnnotations;

namespace OnboardingBuddy.Models
{
    public class FileUpload
    {
        public int Id { get; set; }
        
        [Required]
        public string FileName { get; set; } = string.Empty;
        
        [Required]
        public string OriginalFileName { get; set; } = string.Empty;
        
        [Required]
        public string ContentType { get; set; } = string.Empty;
        
        public long FileSizeBytes { get; set; }
        
        // Store file content directly in database instead of file path
        public byte[] FileContent { get; set; } = Array.Empty<byte>();
        
        // Keep FilePath for backward compatibility (can be removed later)
        public string? FilePath { get; set; }
        
        public string? SessionId { get; set; }
        
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsProcessed { get; set; } = false;
        
        public string? ProcessedContent { get; set; }
        
        public string? ProcessingError { get; set; }
        
        // Navigation property for training material attachments
        public virtual ICollection<TrainingMaterialAttachment> TrainingMaterialAttachments { get; set; } = new List<TrainingMaterialAttachment>();
    }

    public class ChatMessageWithFiles
    {
        [Required]
        public string Message { get; set; } = string.Empty;
        
        public List<IFormFile>? Files { get; set; }
        
        public string? SessionId { get; set; }
    }

    public class FileUploadResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<string> UploadedFiles { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}