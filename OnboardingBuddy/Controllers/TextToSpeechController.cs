using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Speech.Synthesis;
using System.IO;

namespace AcumaticaOnboardingAssistant.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TextToSpeechController : ControllerBase
{
    private readonly ILogger<TextToSpeechController> _logger;

    public TextToSpeechController(ILogger<TextToSpeechController> logger)
    {
        _logger = logger;
    }

    private string GenerateUniqueFileName()
    {
        return $"speech_{DateTime.UtcNow:yyyyMMddHHmmssfff}.wav";
    }

    [HttpPost("speak")]
    public async Task<IActionResult> Speak([FromBody] TextToSpeechRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("Text cannot be empty.");
        }

        try
        {
            using var synth = new SpeechSynthesizer();
            using var ms = new MemoryStream();
            VoiceInfo softVoice = GetVoiceToUse(synth);

            synth.SelectVoice(softVoice.Name);
            synth.Volume = 80; // Slightly softer volume
            synth.Rate = -1;   // Slightly slower for a more pleasant tone

            synth.SetOutputToWaveStream(ms);
            synth.Speak(request.Text);
            ms.Position = 0;

            var fileName = GenerateUniqueFileName();
            return File(ms.ToArray(), "audio/wav", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating speech audio.");
            return StatusCode(500, "Error generating speech audio.");
        }
    }

    private static VoiceInfo GetVoiceToUse(SpeechSynthesizer synth)
    {

        // Set a softer, more appealing voice if available
        return synth.GetInstalledVoices()
            .Select(v => v.VoiceInfo)
            .FirstOrDefault(v => v.Gender == VoiceGender.Female && v.Age == VoiceAge.Adult)
            ?? synth.GetInstalledVoices().First().VoiceInfo;
    }
}

public class TextToSpeechRequest
{
    public string Text { get; set; } = string.Empty;
}