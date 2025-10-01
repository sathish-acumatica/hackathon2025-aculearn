using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace OnboardingBuddy.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigTestController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ConfigTestController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("openapi")]
        public IActionResult GetOpenApiConfig()
        {
            var openApiConfig = _configuration.GetSection("OpenAPI");
            if (!openApiConfig.Exists())
            {
                return NotFound("OpenAPI configuration not found");
            }

            return Ok(new
            {
                Title = openApiConfig["Title"],
                Version = openApiConfig["Version"],
                Description = openApiConfig["Description"],
                ContactName = openApiConfig["ContactName"],
                ContactEmail = openApiConfig["ContactEmail"]
            });
        }

        [HttpGet("acumatica")]
        public IActionResult GetAcumaticaConfig()
        {
            var acumaticaConfig = _configuration.GetSection("Acumatica");
            if (!acumaticaConfig.Exists())
            {
                return NotFound("Acumatica configuration not found");
            }

            return Ok(new
            {
                BaseUrl = acumaticaConfig["BaseUrl"],
                Version = acumaticaConfig["Version"],
                Company = acumaticaConfig["Authentication:Company"],
                Branch = acumaticaConfig["Authentication:Branch"],
                EnableSync = acumaticaConfig.GetValue<bool>("Integration:EnableSync"),
                SyncInterval = acumaticaConfig.GetValue<int>("Integration:SyncIntervalMinutes")
            });
        }

        [HttpGet("ai-config")]
        public IActionResult GetAIConfig()
        {
            var aiConfig = _configuration.GetSection("AIConfiguration");
            if (!aiConfig.Exists())
            {
                return NotFound("AI configuration not found");
            }

            return Ok(new
            {
                AIService = aiConfig["AIService"],
                ApiUrl = aiConfig["ApiUrl"],
                Model = aiConfig["Model"],
                MaxTokens = aiConfig.GetValue<int>("MaxTokens"),
                Temperature = aiConfig.GetValue<double>("Temperature"),
                HasApiKey = !string.IsNullOrEmpty(aiConfig["ApiKey"]),
                ApiKeyPrefix = aiConfig["ApiKey"]?.Substring(0, Math.Min(10, aiConfig["ApiKey"]?.Length ?? 0)) + "..."
            });
        }

        [HttpGet("all-config-sources")]
        public IActionResult GetAllConfigSources()
        {
            var configRoot = _configuration as IConfigurationRoot;
            var sources = configRoot?.Providers
                .Select(p => p.GetType().Name)
                .ToList();

            return Ok(new { ConfigurationSources = sources });
        }
    }
}