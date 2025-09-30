using Microsoft.AspNetCore.Mvc;
using OnboardingBuddy.Api.Models;
using OnboardingBuddy.Api.Services;

namespace OnboardingBuddy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileUploadController : ControllerBase
{
    private readonly IFileUploadService _fileUploadService;
    private readonly ILogger<FileUploadController> _logger;

    public FileUploadController(IFileUploadService fileUploadService, ILogger<FileUploadController> logger)
    {
        _fileUploadService = fileUploadService;
        _logger = logger;
    }

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
    public async Task<ActionResult<FileUploadResult>> UploadFiles([FromForm] List<IFormFile> files, [FromForm] string? sessionId = null)
    {
        try
        {
            if (files == null || !files.Any())
            {
                return BadRequest("No files provided");
            }

            _logger.LogInformation("Uploading {FileCount} files for session {SessionId}", files.Count, sessionId);

            var result = await _fileUploadService.ProcessFilesAsync(files, sessionId ?? "default-session");

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading files for session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while uploading files");
        }
    }

    [HttpGet("session/{sessionId}")]
    public async Task<ActionResult<List<FileUpload>>> GetSessionFiles(string sessionId)
    {
        try
        {
            var files = await _fileUploadService.GetSessionFilesAsync(sessionId);
            return Ok(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving files for session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while retrieving files");
        }
    }

    [HttpDelete("{fileId}")]
    public async Task<IActionResult> DeleteFile(int fileId)
    {
        try
        {
            var success = await _fileUploadService.DeleteFileAsync(fileId);
            if (!success)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {FileId}", fileId);
            return StatusCode(500, "An error occurred while deleting the file");
        }
    }

    [HttpGet("{fileId}")]
    public async Task<ActionResult<FileUpload>> GetFile(int fileId)
    {
        try
        {
            var file = await _fileUploadService.GetFileAsync(fileId);
            if (file == null)
            {
                return NotFound();
            }

            return Ok(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file {FileId}", fileId);
            return StatusCode(500, "An error occurred while retrieving the file");
        }
    }
}