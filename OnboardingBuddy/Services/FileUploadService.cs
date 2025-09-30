using OnboardingBuddy.Models;
using OnboardingBuddy.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace OnboardingBuddy.Services;

public interface IFileUploadService
{
    Task<FileUploadResult> ProcessFilesAsync(List<IFormFile> files, string sessionId);
    Task<List<FileUpload>> GetSessionFilesAsync(string sessionId);
    Task<bool> DeleteFileAsync(int fileId);
    Task<FileUpload?> GetFileAsync(int fileId);
}

public class FileUploadService : IFileUploadService
{
    private readonly OnboardingDbContext _context;
    private readonly ILogger<FileUploadService> _logger;
    private readonly string _uploadsPath;
    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB
    private readonly string[] _allowedExtensions = { ".pdf", ".txt", ".doc", ".docx" };

    public FileUploadService(OnboardingDbContext context, ILogger<FileUploadService> logger, IWebHostEnvironment environment)
    {
        _context = context;
        _logger = logger;
        _uploadsPath = Path.Combine(environment.ContentRootPath, "uploads");
        
        Directory.CreateDirectory(_uploadsPath);
    }

    public async Task<FileUploadResult> ProcessFilesAsync(List<IFormFile> files, string sessionId)
    {
        var result = new FileUploadResult { Success = true };

        foreach (var file in files)
        {
            try
            {
                if (!IsValidFile(file, result))
                    continue;

                var fileUpload = await SaveFileAsync(file, sessionId);
                await ProcessFileContentAsync(fileUpload);

                result.UploadedFiles.Add(fileUpload.OriginalFileName);
                _logger.LogInformation("Successfully processed file {FileName} for session {SessionId}", 
                    file.FileName, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file {FileName} for session {SessionId}", 
                    file.FileName, sessionId);
                result.Errors.Add($"Failed to process {file.FileName}: {ex.Message}");
                result.Success = false;
            }
        }

        return result;
    }

    public async Task<List<FileUpload>> GetSessionFilesAsync(string sessionId)
    {
        return await _context.FileUploads
            .Where(f => f.SessionId == sessionId)
            .OrderByDescending(f => f.UploadedAt)
            .ToListAsync();
    }

    public async Task<bool> DeleteFileAsync(int fileId)
    {
        var file = await _context.FileUploads.FindAsync(fileId);
        if (file == null) return false;

        try
        {
            if (File.Exists(file.FilePath))
            {
                File.Delete(file.FilePath);
            }

            _context.FileUploads.Remove(file);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted file {FileName} (ID: {FileId})", file.OriginalFileName, fileId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {FileId}", fileId);
            return false;
        }
    }

    public async Task<FileUpload?> GetFileAsync(int fileId)
    {
        return await _context.FileUploads.FindAsync(fileId);
    }

    private bool IsValidFile(IFormFile file, FileUploadResult result)
    {
        if (file.Length == 0)
        {
            result.Errors.Add($"File {file.FileName} is empty");
            return false;
        }

        if (file.Length > MaxFileSize)
        {
            result.Errors.Add($"File {file.FileName} exceeds maximum size of {MaxFileSize / 1024 / 1024}MB");
            return false;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(extension))
        {
            result.Errors.Add($"File type {extension} is not supported");
            return false;
        }

        return true;
    }

    private async Task<FileUpload> SaveFileAsync(IFormFile file, string sessionId)
    {
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(_uploadsPath, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var fileUpload = new FileUpload
        {
            FileName = fileName,
            OriginalFileName = file.FileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            FilePath = filePath,
            SessionId = sessionId,
            UploadedAt = DateTime.UtcNow
        };

        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        return fileUpload;
    }

    private async Task ProcessFileContentAsync(FileUpload fileUpload)
    {
        try
        {
            var extension = Path.GetExtension(fileUpload.OriginalFileName).ToLowerInvariant();
            string content = extension switch
            {
                ".pdf" => await ExtractPdfTextAsync(fileUpload.FilePath),
                ".txt" => await File.ReadAllTextAsync(fileUpload.FilePath),
                _ => "File content extraction not supported for this file type"
            };

            fileUpload.ProcessedContent = content;
            fileUpload.IsProcessed = true;
        }
        catch (Exception ex)
        {
            fileUpload.ProcessingError = ex.Message;
            _logger.LogError(ex, "Error processing content for file {FileName}", fileUpload.OriginalFileName);
        }

        await _context.SaveChangesAsync();
    }

    private async Task<string> ExtractPdfTextAsync(string filePath)
    {
        var content = new StringBuilder();

        try
        {
            using var reader = new PdfReader(filePath);
            using var document = new PdfDocument(reader);

            for (int i = 1; i <= document.GetNumberOfPages(); i++)
            {
                var page = document.GetPage(i);
                var text = PdfTextExtractor.GetTextFromPage(page);
                content.AppendLine(text);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting PDF text from {FilePath}", filePath);
            throw new InvalidOperationException("Failed to extract text from PDF", ex);
        }

        return await Task.FromResult(content.ToString());
    }
}