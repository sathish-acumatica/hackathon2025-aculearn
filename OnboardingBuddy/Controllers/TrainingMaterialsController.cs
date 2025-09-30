using Microsoft.AspNetCore.Mvc;
using OnboardingBuddy.Models;
using OnboardingBuddy.Services;

namespace OnboardingBuddy.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrainingMaterialsController : ControllerBase
{
    private readonly ITrainingMaterialService _trainingService;
    private readonly ILogger<TrainingMaterialsController> _logger;

    public TrainingMaterialsController(ITrainingMaterialService trainingService, ILogger<TrainingMaterialsController> logger)
    {
        _trainingService = trainingService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TrainingMaterial>>> GetAll()
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
    public async Task<ActionResult<TrainingMaterial>> GetById(int id)
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
    public async Task<ActionResult<TrainingMaterial>> Create([FromBody] TrainingMaterialRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var material = await _trainingService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = material.Id }, material);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating training material");
            return StatusCode(500, "An error occurred while creating the training material");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TrainingMaterial>> Update(int id, [FromBody] TrainingMaterialRequest request)
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

            return Ok(material);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating training material {Id}", id);
            return StatusCode(500, "An error occurred while updating the training material");
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