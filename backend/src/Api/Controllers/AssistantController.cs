using System.ComponentModel.DataAnnotations;
using Application.Abstractions;
using Application.Strategies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Api.Controllers;

/// AI support interface for citizens (multilingual). It gives guidance only: it cannot approve help or give medical advice.
[ApiController]
[Route("api/assistant")]
[AllowAnonymous]
[EnableRateLimiting("assistant")]
public class AssistantController : ControllerBase
{
    private static readonly string[] Emergency = { "bleeding", "unconscious", "not breathing", "trapped", "drowning", "fire", "heart attack" };
    private readonly IAiAssistantClient _ai;
    public AssistantController(IAiAssistantClient ai) => _ai = ai;
    public class AskDto { [Required, StringLength(500, MinimumLength = 2)] public string Message { get; set; } = ""; public string Language { get; set; } = "en"; }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask(AskDto dto, CancellationToken ct)
    {
        var lang = new[] { "en", "si", "ta" }.Contains(dto.Language) ? dto.Language : "en";
        var msg = TextSanitizer.Clean(dto.Message, 500);
        var urgent = Emergency.Any(e => msg.Contains(e, StringComparison.OrdinalIgnoreCase));
        var answer = await _ai.AskAsync(msg, lang, ct);
        return Ok(new
        {
            answer = answer.Text, usedFallback = answer.IsFallback,
            emergencyNotice = urgent ? "This sounds like an emergency. Contact your local emergency number or nearest medical help now, then submit a request here." : null,
            disclaimer = "Automated assistant. It cannot approve help or give medical advice. A human team reviews every request."
        });
    }
}
