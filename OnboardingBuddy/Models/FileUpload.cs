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
        
        [Required]
        public string FilePath { get; set; } = string.Empty;
        
        public string? SessionId { get; set; }
        
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsProcessed { get; set; } = false;
        
        public string? ProcessedContent { get; set; }
        
        public string? ProcessingError { get; set; }
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