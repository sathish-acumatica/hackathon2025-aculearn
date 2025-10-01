using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OnboardingBuddy.Models
{
    // Junction table for many-to-many relationship between TrainingMaterial and FileUpload
    public class TrainingMaterialAttachment
    {
        public int Id { get; set; }
        
        public int TrainingMaterialId { get; set; }
        [JsonIgnore]
        public TrainingMaterial TrainingMaterial { get; set; } = null!;
        
        public int FileUploadId { get; set; }
        [JsonIgnore]
        public FileUpload FileUpload { get; set; } = null!;
        
        public DateTime AttachedAt { get; set; } = DateTime.UtcNow;
        
        public string? Description { get; set; } // Optional description for the attachment
    }
}