using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace McIntoshHotshots.Services;

public class CoachingService : ICoachingService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IUserPerformanceService _performanceService;
    private readonly ILogger<CoachingService> _logger;

    public CoachingService(
        HttpClient httpClient, 
        IConfiguration configuration, 
        IUserPerformanceService performanceService,
        ILogger<CoachingService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _performanceService = performanceService;
        _logger = logger;
    }

    public async Task<string> GetCoachingResponseAsync(string userMessage, string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Prepare OpenAI request
            var apiKey = _configuration["OpenAI:ApiKey"];
            var endpoint = _configuration["OpenAI:Endpoint"];
            var model = _configuration["OpenAI:Model"] ?? "gpt-4o";

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("OpenAI API key is not configured");
                return "I'm sorry, the coaching service is not properly configured. Please contact support.";
            }

            if (string.IsNullOrEmpty(endpoint))
            {
                _logger.LogError("OpenAI endpoint is not configured");
                return "I'm sorry, the coaching service is not properly configured. Please contact support.";
            }

            // Get user performance data for personalized coaching
            var performanceData = await _performanceService.GetUserPerformanceDataAsync(userId, cancellationToken);
            
            // Build personalized system prompt with user data
            var systemPrompt = BuildPersonalizedSystemPrompt(performanceData);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = new
            {
                model,
                input = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                }
            };

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("OpenAI API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                return "I'm experiencing some technical difficulties right now. Please try again in a moment.";
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            // Parse Responses API output
            if (doc.RootElement.TryGetProperty("output_text", out var outputTextProp))
            {
                var result = outputTextProp.GetString();
                if (!string.IsNullOrEmpty(result))
                {
                    _logger.LogInformation("Successfully received coaching response of {Length} characters", result.Length);
                    return result;
                }
            }

            // Fallback to output array parsing
            if (doc.RootElement.TryGetProperty("output", out var outputArray) &&
                outputArray.ValueKind == JsonValueKind.Array && outputArray.GetArrayLength() > 0)
            {
                var firstOutput = outputArray[0];
                if (firstOutput.TryGetProperty("content", out var contentArray) &&
                    contentArray.ValueKind == JsonValueKind.Array && contentArray.GetArrayLength() > 0)
                {
                    var firstContent = contentArray[0];
                    if (firstContent.TryGetProperty("text", out var textProp))
                    {
                        var result = textProp.GetString();
                        if (!string.IsNullOrEmpty(result))
                        {
                            _logger.LogInformation("Successfully received coaching response from fallback parsing");
                            return result;
                        }
                    }
                }
            }

            _logger.LogWarning("Could not parse response from OpenAI API");
            return "I received a response but couldn't understand it properly. Could you rephrase your question?";
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning("Request to OpenAI API timed out");
            return "The coaching service is taking longer than expected. Please try again.";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error when calling OpenAI API");
            return "I'm having trouble connecting to the coaching service. Please check your internet connection and try again.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in coaching service");
            return "I encountered an unexpected error. Please try again in a moment.";
        }
    }

    private string BuildPersonalizedSystemPrompt(UserPerformanceData performanceData)
    {
        var prompt = @"You are an expert darts coach with years of experience training players at all levels. 
Your expertise includes:
- Throwing techniques, stance, and grip
- Mental game strategies and pressure management
- Finishing combinations and checkout strategies
- Practice routines and skill development
- Equipment recommendations
- Tournament preparation and game tactics

Provide helpful, encouraging, and specific advice tailored to darts players. 
Keep responses conversational but informative, and always relate advice back to improving their darts game.
Use bullet points and clear structure when giving multiple tips.";

        // Add personalized context if we have performance data
        if (performanceData.Player != null)
        {
            prompt += $"\n\nYou are coaching {performanceData.Player.Name} (ELO: {performanceData.Player.EloNumber}).";
            
            var stats = performanceData.Stats;
            if (stats.TotalMatches > 0)
            {
                prompt += $@"

PLAYER PERFORMANCE CONTEXT:
- Total matches played: {stats.TotalMatches}
- Win percentage: {stats.WinPercentage:F1}%
- Average match score: {stats.AverageScore:F1}
- Average three-dart score: {stats.AverageThreeDartScore:F1}
- Checkout percentage: {stats.CheckoutPercentage:F1}%
- Highest finish: {stats.HighestFinish}
- Average turns per leg: {stats.AverageTurnsPerLeg:F1}";

                if (stats.CommonWeakScores.Any())
                {
                    prompt += $"\n- Commonly leaves these scores: {string.Join(", ", stats.CommonWeakScores)}";
                }

                if (stats.PerformanceTrends.Any())
                {
                    prompt += $"\n- Performance trends: {string.Join("; ", stats.PerformanceTrends)}";
                }

                prompt += @"

Use this performance data to provide personalized coaching advice. 
Reference specific strengths to build confidence and identify improvement areas based on the statistics.
Suggest practice routines that target their weak areas.
Be encouraging while being honest about areas needing work.";
            }
            else
            {
                prompt += "\n\nThis player is new and hasn't played many recorded matches yet. Focus on fundamental techniques and building confidence.";
            }
        }

        return prompt;
    }
} 