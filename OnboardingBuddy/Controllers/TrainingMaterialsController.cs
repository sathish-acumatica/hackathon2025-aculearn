using Microsoft.AspNetCore.Mvc;
using OnboardingBuddy.Models;
using OnboardingBuddy.Services;

namespace OnboardingBuddy.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrainingMaterialsController : ControllerBase
{
    private readonly ITrainingMaterialService _trainingService;
    private readonly IFileUploadService _fileUploadService;
    private readonly ILogger<TrainingMaterialsController> _logger;

    public TrainingMaterialsController(
        ITrainingMaterialService trainingService, 
        IFileUploadService fileUploadService,
        ILogger<TrainingMaterialsController> logger)
    {
        _trainingService = trainingService;
        _fileUploadService = fileUploadService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TrainingMaterialResponse>>> GetAll()
    {
        try
        {
            var materials = await _trainingService.GetAllAsync();
            return Ok(materials);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving training materials");
            return StatusCode(500, "An error occurred while retrieving training materials");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TrainingMaterialResponse>> GetById(int id)
    {
        try
        {
            var material = await _trainingService.GetByIdAsync(id);
            if (material == null)
            {
                return NotFound();
            }

            return Ok(material);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving training material {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the training material");
        }
    }

    [HttpPost]
    public async Task<ActionResult<TrainingMaterialResponse>> Create([FromBody] TrainingMaterialRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var material = await _trainingService.CreateAsync(request);
            
            // Return the response DTO to avoid circular references
            var response = await _trainingService.GetByIdAsync(material.Id);
            return CreatedAtAction(nameof(GetById), new { id = material.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating training material");
            return StatusCode(500, "An error occurred while creating the training material");
        }
    }

    [HttpPost("with-attachments")]
    public async Task<ActionResult<TrainingMaterialResponse>> CreateWithAttachments([FromForm] TrainingMaterialWithAttachmentsRequest request)
    {
        try
        {
            _logger.LogInformation("Received request with {FileCount} files and {DescriptionCount} descriptions", 
                request.Files?.Count ?? 0, request.AttachmentDescriptions?.Count ?? 0);
            
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state is invalid: {Errors}", 
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return BadRequest(ModelState);
            }

            var material = await _trainingService.CreateWithAttachmentsAsync(request, _fileUploadService);
            
            // Return the response DTO to avoid circular references
            var response = await _trainingService.GetByIdAsync(material.Id);
            return CreatedAtAction(nameof(GetById), new { id = material.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating training material with attachments");
            return StatusCode(500, "An error occurred while creating the training material");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TrainingMaterialResponse>> Update(int id, [FromBody] TrainingMaterialRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var material = await _trainingService.UpdateAsync(id, request);
            if (material == null)
            {
                return NotFound();
            }

            // Return the response DTO to avoid circular references
            var response = await _trainingService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating training material {Id}", id);
            return StatusCode(500, "An error occurred while updating the training material");
        }
    }

    [HttpPut("{id}/with-attachments")]
    public async Task<ActionResult<TrainingMaterialResponse>> UpdateWithAttachments(int id, [FromForm] TrainingMaterialWithAttachmentsRequest request)
    {
        try
        {
            _logger.LogInformation("=== DEBUGGING: Received update request for material {Id} ===", id);
            _logger.LogInformation("Files count: {FileCount}", request.Files?.Count ?? 0);
            _logger.LogInformation("AttachmentDescriptions count: {DescriptionCount}", request.AttachmentDescriptions?.Count ?? 0);
            _logger.LogInformation("Title: '{Title}'", request.Title ?? "NULL");
            _logger.LogInformation("Category: '{Category}'", request.Category ?? "NULL");
            _logger.LogInformation("Content length: {ContentLength}", request.Content?.Length ?? 0);
            _logger.LogInformation("IsActive: {IsActive}", request.IsActive);
            _logger.LogInformation("InternalNotes: '{InternalNotes}'", request.InternalNotes ?? "NULL");
            
            // Log FormData keys for debugging
            _logger.LogInformation("Request ContentType: {ContentType}", Request.ContentType);
            if (Request.HasFormContentType)
            {
                _logger.LogInformation("Form keys: {Keys}", string.Join(", ", Request.Form.Keys));
                foreach (var key in Request.Form.Keys)
                {
                    _logger.LogInformation("Form[{Key}] = '{Value}'", key, Request.Form[key]);
                }
            }
                
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("=== MODEL STATE INVALID for material {Id} ===", id);
                foreach (var error in ModelState)
                {
                    _logger.LogWarning("Field: '{Field}', Errors: [{Errors}]", error.Key, 
                        string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage)));
                }
                return BadRequest(ModelState);
            }

            var material = await _trainingService.UpdateWithAttachmentsAsync(id, request, _fileUploadService);
            if (material == null)
            {
                return NotFound();
            }

            // Return the response DTO to avoid circular references
            var response = await _trainingService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating training material {Id} with attachments", id);
            return StatusCode(500, "An error occurred while updating the training material");
        }
    }

    [HttpDelete("{id}/attachments/{attachmentId}")]
    public async Task<IActionResult> RemoveAttachment(int id, int attachmentId)
    {
        try
        {
            var success = await _trainingService.RemoveAttachmentAsync(id, attachmentId);
            if (!success)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing attachment {AttachmentId} from training material {Id}", attachmentId, id);
            return StatusCode(500, "An error occurred while removing the attachment");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var success = await _trainingService.DeleteAsync(id);
            if (!success)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting training material {Id}", id);
            return StatusCode(500, "An error occurred while deleting the training material");
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<TrainingMaterial>>> Search([FromQuery] string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Search query cannot be empty");
            }

            var materials = await _trainingService.SearchAsync(query);
            return Ok(materials);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching training materials with query: {Query}", query);
            return StatusCode(500, "An error occurred while searching training materials");
        }
    }

    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<TrainingMaterial>>> GetByCategory(string category)
    {
        try
        {
            var materials = await _trainingService.GetByCategoryAsync(category);
            return Ok(materials);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving training materials for category: {Category}", category);
            return StatusCode(500, "An error occurred while retrieving training materials");
        }
    }
}