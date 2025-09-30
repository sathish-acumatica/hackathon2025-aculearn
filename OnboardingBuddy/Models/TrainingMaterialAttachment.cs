using System.ComponentModel.DataAnnotations;

namespace OnboardingBuddy.Models
{
    // Junction table for many-to-many relationship between TrainingMaterial and FileUpload
    public class TrainingMaterialAttachment
    {
        public int Id { get; set; }
        
        public int TrainingMaterialId { get; set; }
        public TrainingMaterial TrainingMaterial { get; set; } = null!;
        
        public int FileUploadId { get; set; }
        public FileUpload FileUpload { get; set; } = null!;
        
        public DateTime AttachedAt { get; set; } = DateTime.UtcNow;
        
        public string? Description { get; set; } // Optional description for the attachment
    }
}