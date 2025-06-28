using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using McIntoshHotshots.Services;
using System.Security.Claims;

namespace McIntoshHotshots.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class CoachingDebugController : ControllerBase
{
    private readonly CoachingDebugService _debugService;
    private readonly ICoachingService _coachingService;
    private readonly ILogger<CoachingDebugController> _logger;

    public CoachingDebugController(
        CoachingDebugService debugService,
        ICoachingService coachingService,
        ILogger<CoachingDebugController> logger)
    {
        _debugService = debugService;
        _coachingService = coachingService;
        _logger = logger;
    }

    [HttpGet("test-function/{functionName}")]
    public async Task<IActionResult> TestFunction(string functionName, [FromQuery] string? arguments = null)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID not found");
            }

            var result = await _debugService.TestFunctionCallAsync(functionName, userId, arguments);
            return Ok(new { result, timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing function {FunctionName}", functionName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("test-full-flow")]
    public async Task<IActionResult> TestFullFlow([FromBody] TestFlowRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID not found");
            }

            _logger.LogInformation("Testing full coaching flow for message: {Message}", request.Message);

            var startTime = DateTime.UtcNow;
            var response = await _coachingService.GetCoachingResponseAsync(request.Message, userId);
            var endTime = DateTime.UtcNow;

            return Ok(new 
            { 
                response, 
                duration = (endTime - startTime).TotalMilliseconds,
                timestamp = DateTime.UtcNow 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing full coaching flow");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("available-functions")]
    public IActionResult GetAvailableFunctions()
    {
        return Ok(new
        {
            functions = new[]
            {
                "get_head_to_head_stats",
                "get_player_performance", 
                "get_opponent_list",
                "find_opponent"
            },
            test_messages = new[]
            {
                "what is my overall leg count against Chris?",
                "show me my performance stats",
                "who have I played against?",
                "find opponent Chris"
            }
        });
    }

    public class TestFlowRequest
    {
        public string Message { get; set; } = "";
    }
} 