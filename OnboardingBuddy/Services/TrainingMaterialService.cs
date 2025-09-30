using OnboardingBuddy.Models;
using OnboardingBuddy.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace OnboardingBuddy.Services;

public interface ITrainingMaterialService
{
    Task<IEnumerable<TrainingMaterial>> GetAllAsync();
    Task<TrainingMaterial?> GetByIdAsync(int id);
    Task<TrainingMaterial> CreateAsync(TrainingMaterialRequest request);
    Task<TrainingMaterial?> UpdateAsync(int id, TrainingMaterialRequest request);
    Task<bool> DeleteAsync(int id);
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

    public async Task<IEnumerable<TrainingMaterial>> GetAllAsync()
    {
        return await _context.TrainingMaterials
            .OrderBy(m => m.Title)
            .ToListAsync();
    }

    public async Task<TrainingMaterial?> GetByIdAsync(int id)
    {
        return await _context.TrainingMaterials.FindAsync(id);
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
                .Where(m => m.IsActive)
                .OrderBy(m => m.Category)
                .ThenBy(m => m.Title)
                .ToListAsync();

            var relevantMaterials = FilterRelevantMaterials(materials, userQuery);
            var context = BuildContextString(relevantMaterials);

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
}