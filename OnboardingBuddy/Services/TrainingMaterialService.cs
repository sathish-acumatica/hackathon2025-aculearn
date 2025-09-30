using OnboardingBuddy.Models;
using OnboardingBuddy.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace OnboardingBuddy.Services;

public interface ITrainingMaterialService
{
    Task<IEnumerable<TrainingMaterialResponse>> GetAllAsync();
    Task<TrainingMaterialResponse?> GetByIdAsync(int id);
    Task<TrainingMaterial> CreateAsync(TrainingMaterialRequest request);
    Task<TrainingMaterial> CreateWithAttachmentsAsync(TrainingMaterialWithAttachmentsRequest request, IFileUploadService fileUploadService);
    Task<TrainingMaterial?> UpdateAsync(int id, TrainingMaterialRequest request);
    Task<TrainingMaterial?> UpdateWithAttachmentsAsync(int id, TrainingMaterialWithAttachmentsRequest request, IFileUploadService fileUploadService);
    Task<bool> DeleteAsync(int id);
    Task<bool> RemoveAttachmentAsync(int materialId, int attachmentId);
    Task<IEnumerable<TrainingMaterial>> SearchAsync(string query);
    Task<IEnumerable<TrainingMaterial>> GetByCategoryAsync(string category);
    Task<string> GetTrainingContextForAI(string userQuery);
    Task<(List<TrainingMaterial> materials, string context)> GetSessionContextAsync(string sessionId, string userQuery, ISessionService sessionService);
}

public class TrainingMaterialService : ITrainingMaterialService
{
    private readonly OnboardingDbContext _context;
    private readonly ILogger<TrainingMaterialService> _logger;

    public TrainingMaterialService(OnboardingDbContext context, ILogger<TrainingMaterialService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<TrainingMaterialResponse>> GetAllAsync()
    {
        var materials = await _context.TrainingMaterials
            .Include(m => m.Attachments)
                .ThenInclude(a => a.FileUpload)
            .OrderBy(m => m.Title)
            .ToListAsync();

        return materials.Select(m => new TrainingMaterialResponse
        {
            Id = m.Id,
            Title = m.Title,
            Category = m.Category,
            Content = m.Content,
            InternalNotes = m.InternalNotes,
            IsActive = m.IsActive,
            CreatedAt = m.CreatedAt,
            UpdatedAt = m.UpdatedAt,
            Attachments = m.Attachments.Select(a => new AttachmentInfo
            {
                Id = a.FileUpload.Id,
                FileName = a.FileUpload.FileName,
                OriginalFileName = a.FileUpload.OriginalFileName,
                FileSizeBytes = a.FileUpload.FileSizeBytes,
                ContentType = a.FileUpload.ContentType,
                AttachedAt = a.AttachedAt,
                Description = a.Description,
                IsProcessed = a.FileUpload.IsProcessed
            }).ToList()
        });
    }

    public async Task<TrainingMaterialResponse?> GetByIdAsync(int id)
    {
        var material = await _context.TrainingMaterials
            .Include(m => m.Attachments)
                .ThenInclude(a => a.FileUpload)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (material == null) return null;

        return new TrainingMaterialResponse
        {
            Id = material.Id,
            Title = material.Title,
            Category = material.Category,
            Content = material.Content,
            InternalNotes = material.InternalNotes,
            IsActive = material.IsActive,
            CreatedAt = material.CreatedAt,
            UpdatedAt = material.UpdatedAt,
            Attachments = material.Attachments.Select(a => new AttachmentInfo
            {
                Id = a.FileUpload.Id,
                FileName = a.FileUpload.FileName,
                OriginalFileName = a.FileUpload.OriginalFileName,
                FileSizeBytes = a.FileUpload.FileSizeBytes,
                ContentType = a.FileUpload.ContentType,
                AttachedAt = a.AttachedAt,
                Description = a.Description,
                IsProcessed = a.FileUpload.IsProcessed
            }).ToList()
        };
    }

    public async Task<TrainingMaterial> CreateAsync(TrainingMaterialRequest request)
    {
        var material = new TrainingMaterial
        {
            Title = request.Title,
            Category = request.Category,
            Content = request.Content,
            InternalNotes = request.InternalNotes,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.TrainingMaterials.Add(material);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Created training material: {Title}", material.Title);
        
        return material;
    }

    public async Task<TrainingMaterial?> UpdateAsync(int id, TrainingMaterialRequest request)
    {
        var material = await _context.TrainingMaterials.FindAsync(id);
        if (material == null) return null;

        material.Title = request.Title;
        material.Category = request.Category;
        material.Content = request.Content;
        material.InternalNotes = request.InternalNotes;
        material.IsActive = request.IsActive;
        material.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Updated training material: {Title}", material.Title);
        
        return material;
    }

    public async Task<TrainingMaterial> CreateWithAttachmentsAsync(TrainingMaterialWithAttachmentsRequest request, IFileUploadService fileUploadService)
    {
        var material = new TrainingMaterial
        {
            Title = request.Title,
            Category = request.Category,
            Content = request.Content,
            InternalNotes = request.InternalNotes,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.TrainingMaterials.Add(material);
        await _context.SaveChangesAsync();

        // Process file attachments
        if (request.Files?.Any() == true)
        {
            var sessionId = $"training_material_{material.Id}_{DateTime.UtcNow.Ticks}";
            var uploadResult = await fileUploadService.ProcessFilesAsync(request.Files, sessionId);

            if (uploadResult.Success && uploadResult.UploadedFiles.Any())
            {
                // Get the uploaded file records
                var uploadedFiles = await _context.FileUploads
                    .Where(f => f.SessionId == sessionId)
                    .ToListAsync();

                // Create attachments
                for (int i = 0; i < uploadedFiles.Count; i++)
                {
                    var attachment = new TrainingMaterialAttachment
                    {
                        TrainingMaterialId = material.Id,
                        FileUploadId = uploadedFiles[i].Id,
                        AttachedAt = DateTime.UtcNow,
                        Description = request.AttachmentDescriptions?.ElementAtOrDefault(i)
                    };

                    _context.TrainingMaterialAttachments.Add(attachment);
                }

                await _context.SaveChangesAsync();
            }
        }

        _logger.LogInformation("Created training material with attachments: {Title}", material.Title);
        return material;
    }

    public async Task<TrainingMaterial?> UpdateWithAttachmentsAsync(int id, TrainingMaterialWithAttachmentsRequest request, IFileUploadService fileUploadService)
    {
        var material = await _context.TrainingMaterials.FindAsync(id);
        if (material == null) return null;

        material.Title = request.Title;
        material.Category = request.Category;
        material.Content = request.Content;
        material.InternalNotes = request.InternalNotes;
        material.IsActive = request.IsActive;
        material.UpdatedAt = DateTime.UtcNow;

        // Process new file attachments
        if (request.Files?.Any() == true)
        {
            var sessionId = $"training_material_{material.Id}_{DateTime.UtcNow.Ticks}";
            var uploadResult = await fileUploadService.ProcessFilesAsync(request.Files, sessionId);

            if (uploadResult.Success && uploadResult.UploadedFiles.Any())
            {
                // Get the uploaded file records
                var uploadedFiles = await _context.FileUploads
                    .Where(f => f.SessionId == sessionId)
                    .ToListAsync();

                // Create new attachments
                for (int i = 0; i < uploadedFiles.Count; i++)
                {
                    var attachment = new TrainingMaterialAttachment
                    {
                        TrainingMaterialId = material.Id,
                        FileUploadId = uploadedFiles[i].Id,
                        AttachedAt = DateTime.UtcNow,
                        Description = request.AttachmentDescriptions?.ElementAtOrDefault(i)
                    };

                    _context.TrainingMaterialAttachments.Add(attachment);
                }
            }
        }

        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Updated training material with attachments: {Title}", material.Title);
        return material;
    }

    public async Task<bool> RemoveAttachmentAsync(int materialId, int attachmentId)
    {
        var attachment = await _context.TrainingMaterialAttachments
            .Include(a => a.FileUpload)
            .FirstOrDefaultAsync(a => a.TrainingMaterialId == materialId && a.FileUploadId == attachmentId);

        if (attachment == null) return false;

        _context.TrainingMaterialAttachments.Remove(attachment);
        // Note: The FileUpload record will remain for potential reuse
        await _context.SaveChangesAsync();

        _logger.LogInformation("Removed attachment {AttachmentId} from training material {MaterialId}", attachmentId, materialId);
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var material = await _context.TrainingMaterials.FindAsync(id);
        if (material == null) return false;

        _context.TrainingMaterials.Remove(material);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Deleted training material: {Title}", material.Title);
        
        return true;
    }

    public async Task<IEnumerable<TrainingMaterial>> SearchAsync(string query)
    {
        var lowerQuery = query.ToLowerInvariant();
        
        return await _context.TrainingMaterials
            .Where(m => m.IsActive && 
                       (m.Title.ToLower().Contains(lowerQuery) ||
                        m.Category.ToLower().Contains(lowerQuery) ||
                        m.Content.ToLower().Contains(lowerQuery)))
            .OrderBy(m => m.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<TrainingMaterial>> GetByCategoryAsync(string category)
    {
        return await _context.TrainingMaterials
            .Where(m => m.IsActive && m.Category.ToLower() == category.ToLowerInvariant())
            .OrderBy(m => m.Title)
            .ToListAsync();
    }

    public async Task<string> GetTrainingContextForAI(string userQuery)
    {
        var materials = await _context.TrainingMaterials
            .Where(m => m.IsActive)
            .OrderBy(m => m.Category)
            .ThenBy(m => m.Title)
            .ToListAsync();

        var context = string.Join("\n\n", materials.Select(m => 
            $"**{m.Title}** (Category: {m.Category})\n{m.Content}"));

        return context;
    }

    public async Task<(List<TrainingMaterial> materials, string context)> GetSessionContextAsync(
        string sessionId, string userQuery, ISessionService sessionService)
    {
        try
        {
            var materials = await _context.TrainingMaterials
                .Include(m => m.Attachments)
                    .ThenInclude(a => a.FileUpload)
                .Where(m => m.IsActive)
                .OrderBy(m => m.Category)
                .ThenBy(m => m.Title)
                .ToListAsync();

            var relevantMaterials = FilterRelevantMaterials(materials, userQuery);
            var context = BuildContextStringWithAttachments(relevantMaterials);

            _logger.LogInformation("Retrieved {Count} relevant materials for session {SessionId}", 
                relevantMaterials.Count, sessionId);

            return (relevantMaterials, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session context for session {SessionId}", sessionId);
            return (new List<TrainingMaterial>(), string.Empty);
        }
    }

    private List<TrainingMaterial> FilterRelevantMaterials(List<TrainingMaterial> materials, string userQuery)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
            return materials.Take(5).ToList(); // Return first 5 if no query

        var queryLower = userQuery.ToLowerInvariant();
        var keywords = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var scored = materials.Select(m => new
        {
            Material = m,
            Score = CalculateRelevanceScore(m, keywords)
        })
        .Where(x => x.Score > 0)
        .OrderByDescending(x => x.Score)
        .Take(5)
        .Select(x => x.Material)
        .ToList();

        return scored.Any() ? scored : materials.Take(3).ToList();
    }

    private int CalculateRelevanceScore(TrainingMaterial material, string[] keywords)
    {
        var score = 0;
        var content = $"{material.Title} {material.Category} {material.Content}".ToLowerInvariant();

        foreach (var keyword in keywords)
        {
            if (keyword.Length < 3) continue; // Skip short words
            
            var titleMatches = material.Title.ToLowerInvariant().Contains(keyword) ? 3 : 0;
            var categoryMatches = material.Category.ToLowerInvariant().Contains(keyword) ? 2 : 0;
            var contentMatches = material.Content.ToLowerInvariant().Contains(keyword) ? 1 : 0;
            
            score += titleMatches + categoryMatches + contentMatches;
        }

        return score;
    }

    private string BuildContextString(List<TrainingMaterial> materials)
    {
        if (!materials.Any()) return string.Empty;

        var context = string.Join("\n\n", materials.Select(m => 
            $"**{m.Title}** (Category: {m.Category})\n{m.Content}"));

        return context;
    }

    private string BuildContextStringWithAttachments(List<TrainingMaterial> materials)
    {
        if (!materials.Any()) return string.Empty;

        var contextParts = new List<string>();

        foreach (var material in materials)
        {
            var materialContext = $"**{material.Title}** (Category: {material.Category})\n{material.Content}";

            // Add attachment content if available
            if (material.Attachments?.Any() == true)
            {
                var attachmentContents = new List<string>();
                
                foreach (var attachment in material.Attachments)
                {
                    if (attachment.FileUpload.IsProcessed && !string.IsNullOrEmpty(attachment.FileUpload.ProcessedContent))
                    {
                        var attachmentInfo = !string.IsNullOrEmpty(attachment.Description) 
                            ? $"Attachment ({attachment.Description}): {attachment.FileUpload.OriginalFileName}"
                            : $"Attachment: {attachment.FileUpload.OriginalFileName}";
                        
                        attachmentContents.Add($"\n\n{attachmentInfo}\n{attachment.FileUpload.ProcessedContent}");
                    }
                }

                if (attachmentContents.Any())
                {
                    materialContext += string.Join("", attachmentContents);
                }
            }

            contextParts.Add(materialContext);
        }

        return string.Join("\n\n", contextParts);
    }
}