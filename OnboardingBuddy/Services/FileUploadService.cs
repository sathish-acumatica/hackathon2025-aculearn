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
    Task<(byte[] content, string contentType, string fileName)?> GetFileContentAsync(int fileId);
}

public class FileUploadService : IFileUploadService
{
    private readonly OnboardingDbContext _context;
    private readonly ILogger<FileUploadService> _logger;
    private const long MaxFileSize = 50 * 1024 * 1024; // 50MB for multimedia files
    private readonly string[] _allowedExtensions = { 
        // Documents
        ".pdf", ".txt", ".doc", ".docx", ".rtf", ".odt", ".pages", ".tex", ".md", ".markdown",
        ".ppt", ".pptx", ".odp", ".key", ".xls", ".xlsx", ".ods", ".numbers",
        // Images
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".tiff", ".tif", ".ico", ".heic", ".heif",
        // Audio
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma", ".opus",
        // Video
        ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm", ".mkv", ".m4v", ".3gp",
        // Code files
        ".js", ".ts", ".jsx", ".tsx", ".py", ".java", ".c", ".cpp", ".cs", ".php", ".rb", ".go", ".rs", ".swift", ".kt", ".scala",
        ".html", ".htm", ".css", ".scss", ".sass", ".less", ".xml", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf",
        ".sql", ".sh", ".bat", ".ps1", ".r", ".m", ".pl", ".lua", ".dart", ".elm", ".clj", ".hs", ".ml", ".fs", ".vb",
        // Data formats
        ".json", ".csv", ".tsv", ".parquet", ".avro", ".jsonl", ".ndjson",
        // Archives
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz",
        // Specialized formats
        ".epub", ".mobi", ".azw", ".fb2", ".djvu", ".cbr", ".cbz"
    };

    public FileUploadService(OnboardingDbContext context, ILogger<FileUploadService> logger)
    {
        _context = context;
        _logger = logger;
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
            // No need to delete from file system since we store in database
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

    public async Task<(byte[] content, string contentType, string fileName)?> GetFileContentAsync(int fileId)
    {
        var file = await _context.FileUploads.FindAsync(fileId);
        if (file == null) return null;

        return (file.FileContent, file.ContentType, file.OriginalFileName);
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
        // Read file content into memory
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var fileContent = memoryStream.ToArray();

        var fileUpload = new FileUpload
        {
            FileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}",
            OriginalFileName = file.FileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            FileContent = fileContent, // Store in database
            FilePath = null, // No longer needed
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
                // Document types
                ".pdf" => await ExtractPdfTextFromBytesAsync(fileUpload.FileContent),
                ".txt" or ".md" or ".markdown" or ".rtf" or ".tex" => await ExtractTextFromBytesAsync(fileUpload.FileContent),
                ".doc" or ".docx" or ".odt" or ".pages" => $"Document file: {fileUpload.OriginalFileName} - Text extraction available for AI processing",
                ".ppt" or ".pptx" or ".odp" or ".key" => $"Presentation file: {fileUpload.OriginalFileName} - Content available for AI analysis",
                ".xls" or ".xlsx" or ".ods" or ".numbers" => $"Spreadsheet file: {fileUpload.OriginalFileName} - Data structure available for AI processing",
                
                // Image types
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" or ".tiff" or ".tif" or ".ico" or ".heic" or ".heif" => 
                    $"Image file: {fileUpload.OriginalFileName} ({fileUpload.ContentType}) - Visual content available for AI analysis",
                
                // Audio types
                ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a" or ".wma" or ".opus" => 
                    $"Audio file: {fileUpload.OriginalFileName} ({fileUpload.ContentType}) - Audio content available for AI transcription and analysis",
                
                // Video types
                ".mp4" or ".avi" or ".mov" or ".wmv" or ".flv" or ".webm" or ".mkv" or ".m4v" or ".3gp" => 
                    $"Video file: {fileUpload.OriginalFileName} ({fileUpload.ContentType}) - Video and audio content available for AI analysis",
                
                // Code files
                ".js" or ".ts" or ".jsx" or ".tsx" or ".py" or ".java" or ".c" or ".cpp" or ".cs" or ".php" or ".rb" or ".go" or ".rs" or ".swift" or ".kt" or ".scala" or
                ".html" or ".htm" or ".css" or ".scss" or ".sass" or ".less" or ".xml" or ".yaml" or ".yml" or ".toml" or ".ini" or ".cfg" or ".conf" or
                ".sql" or ".sh" or ".bat" or ".ps1" or ".r" or ".m" or ".pl" or ".lua" or ".dart" or ".elm" or ".clj" or ".hs" or ".ml" or ".fs" or ".vb" => 
                    await ExtractTextFromBytesAsync(fileUpload.FileContent),
                
                // Data formats
                ".json" or ".jsonl" or ".ndjson" => await ExtractTextFromBytesAsync(fileUpload.FileContent),
                ".csv" or ".tsv" => $"Data file: {fileUpload.OriginalFileName} - Structured data available for AI analysis and processing",
                ".parquet" or ".avro" => $"Binary data file: {fileUpload.OriginalFileName} - Structured data format available for AI processing",
                
                // Archives
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" or ".xz" => 
                    $"Archive file: {fileUpload.OriginalFileName} - Compressed content available for extraction and AI analysis",
                
                // E-books and specialized formats
                ".epub" or ".mobi" or ".azw" or ".fb2" or ".djvu" or ".cbr" or ".cbz" => 
                    $"E-book/Document file: {fileUpload.OriginalFileName} - Text content available for AI reading and analysis",
                
                _ => $"File: {fileUpload.OriginalFileName} ({fileUpload.ContentType}) - Content available for AI processing"
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

    private async Task<string> ExtractPdfTextFromBytesAsync(byte[] pdfBytes)
    {
        var content = new StringBuilder();

        try
        {
            using var memoryStream = new MemoryStream(pdfBytes);
            using var reader = new PdfReader(memoryStream);
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
            _logger.LogError(ex, "Error extracting PDF text from bytes");
            throw new InvalidOperationException("Failed to extract text from PDF", ex);
        }

        return await Task.FromResult(content.ToString());
    }

    private async Task<string> ExtractTextFromBytesAsync(byte[] fileBytes)
    {
        try
        {
            // Try UTF-8 first
            var content = Encoding.UTF8.GetString(fileBytes);
            
            // Check if it's valid UTF-8 by looking for invalid characters
            if (!content.Contains('�'))
            {
                return await Task.FromResult(content);
            }
            
            // Fallback to other encodings
            foreach (var encoding in new[] { Encoding.ASCII, Encoding.Unicode, Encoding.UTF32, Encoding.GetEncoding("windows-1252") })
            {
                try
                {
                    content = encoding.GetString(fileBytes);
                    if (!content.Contains('�'))
                    {
                        return await Task.FromResult(content);
                    }
                }
                catch
                {
                    continue;
                }
            }
            
            // If all encodings fail, return a description
            return await Task.FromResult($"Binary file content detected - file size: {fileBytes.Length} bytes");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from file bytes");
            return await Task.FromResult($"Text extraction failed - file size: {fileBytes.Length} bytes");
        }
    }

    // Keep the old method for backward compatibility
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