using Microsoft.AspNetCore.Mvc;
using OnboardingBuddy.Models;
using OnboardingBuddy.Services;

namespace OnboardingBuddy.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IAIService _aiService;
    private readonly IFileUploadService _fileUploadService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IAIService aiService, IFileUploadService fileUploadService, ILogger<ChatController> logger)
    {
        _aiService = aiService;
        _fileUploadService = fileUploadService;
        _logger = logger;
    }

    [HttpPost("send")]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest("Message cannot be empty");
            }

            _logger.LogInformation("Received chat message: {Message}", request.Message);

            var response = await _aiService.ProcessMessageAsync(request.Message);

            return Ok(new ChatResponse
            {
                Response = response,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, "An error occurred while processing your message");
        }
    }

    [HttpPost("send-with-files")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
    public async Task<ActionResult<ChatResponse>> SendMessageWithFiles([FromForm] string message, [FromForm] List<IFormFile>? files = null, [FromForm] string? sessionId = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return BadRequest("Message cannot be empty");
            }

            _logger.LogInformation("Received chat message with {FileCount} files: {Message}", files?.Count ?? 0, message);

            string response;
            
            if (files != null && files.Any())
            {
                var uploadResult = await _fileUploadService.ProcessFilesAsync(files, sessionId);
                
                if (!uploadResult.Success)
                {
                    return BadRequest($"File upload failed: {string.Join(", ", uploadResult.Errors)}");
                }

                var uploadedFiles = await _fileUploadService.GetSessionFilesAsync(sessionId ?? "default-session");
                var recentFiles = uploadedFiles.Where(f => uploadResult.UploadedFiles.Contains(f.OriginalFileName)).ToList();

                response = await _aiService.ProcessMessageWithFilesAsync(message, sessionId ?? "default-session", recentFiles);
            }
            else
            {
                response = await _aiService.ProcessSessionMessageAsync(message, sessionId ?? "default-session");
            }

            return Ok(new ChatResponse
            {
                Response = response,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message with files");
            return StatusCode(500, "An error occurred while processing your message");
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    [HttpPost("welcome/{sessionId}")]
    public async Task<ActionResult<ChatResponse>> GenerateWelcomeMessage(string sessionId)
    {
        try
        {
            var response = await _aiService.GenerateWelcomeMessageAsync(sessionId);

            return Ok(new ChatResponse
            {
                Response = response,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating welcome message for session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while generating welcome message");
        }
    }
}