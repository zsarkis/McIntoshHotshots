using System.Text.Json;

namespace McIntoshHotshots.Services;

public class CoachingDebugService
{
    private readonly IUserPerformanceService _performanceService;
    private readonly ILogger<CoachingDebugService> _logger;

    public CoachingDebugService(
        IUserPerformanceService performanceService,
        ILogger<CoachingDebugService> logger)
    {
        _performanceService = performanceService;
        _logger = logger;
    }

    public async Task<string> TestFunctionCallAsync(string functionName, string userId, string? argumentsJson = null)
    {
        try
        {
            _logger.LogInformation("Testing function: {FunctionName} for user: {UserId}", functionName, userId);

            switch (functionName)
            {
                case "get_head_to_head_stats":
                    if (string.IsNullOrEmpty(argumentsJson))
                        argumentsJson = """{"opponent_name": "Test Opponent"}""";
                    return await TestHeadToHeadFunction(argumentsJson, userId);
                
                case "get_player_performance":
                    return await TestPlayerPerformanceFunction(userId);
                
                case "get_opponent_list":
                    return await TestOpponentListFunction(userId);
                
                case "find_opponent":
                    if (string.IsNullOrEmpty(argumentsJson))
                        argumentsJson = """{"search_term": "Chris"}""";
                    return await TestFindOpponentFunction(argumentsJson, userId);
                
                default:
                    return $"Unknown function: {functionName}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing function {FunctionName}", functionName);
            return $"Error: {ex.Message}";
        }
    }

    private async Task<string> TestHeadToHeadFunction(string argumentsJson, string userId)
    {
        using var argsDoc = JsonDocument.Parse(argumentsJson);
        var opponentName = argsDoc.RootElement.GetProperty("opponent_name").GetString();

        _logger.LogInformation("Testing head-to-head for opponent: {OpponentName}", opponentName);
        
        var headToHeadData = await _performanceService.GetHeadToHeadDataAsync(userId, opponentName!, CancellationToken.None);
        
        var result = new
        {
            status = "test_success",
            function = "get_head_to_head_stats",
            opponent_searched = opponentName,
            opponent_found = headToHeadData.OpponentName,
            total_matches = headToHeadData.TotalMatches,
            legs_won = headToHeadData.LegsWon,
            legs_lost = headToHeadData.LegsLost,
            debug_info = $"Function executed successfully. Data available: {headToHeadData.TotalMatches > 0}"
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<string> TestPlayerPerformanceFunction(string userId)
    {
        _logger.LogInformation("Testing player performance for user: {UserId}", userId);
        
        var performanceData = await _performanceService.GetUserPerformanceDataAsync(userId, CancellationToken.None);
        
        var result = new
        {
            status = "test_success",
            function = "get_player_performance",
            player_found = performanceData.Player != null,
            player_name = performanceData.Player?.Name,
            total_matches = performanceData.Stats.TotalMatches,
            debug_info = $"Player data available: {performanceData.Player != null}"
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<string> TestOpponentListFunction(string userId)
    {
        _logger.LogInformation("Testing opponent list for user: {UserId}", userId);
        
        var opponentList = await _performanceService.GetOpponentListAsync(userId, CancellationToken.None);
        
        var result = new
        {
            status = "test_success",
            function = "get_opponent_list",
            total_opponents = opponentList.Count,
            opponent_names = opponentList.Take(5), // Just first 5 for brevity
            debug_info = $"Found {opponentList.Count} opponents"
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<string> TestFindOpponentFunction(string argumentsJson, string userId)
    {
        using var argsDoc = JsonDocument.Parse(argumentsJson);
        var searchTerm = argsDoc.RootElement.GetProperty("search_term").GetString();

        _logger.LogInformation("Testing find opponent with search term: {SearchTerm}", searchTerm);
        
        var opponentList = await _performanceService.GetOpponentListAsync(userId, CancellationToken.None);
        
        var matches = opponentList.Where(name => 
            name.Contains(searchTerm!, StringComparison.OrdinalIgnoreCase) ||
            searchTerm!.Contains(name, StringComparison.OrdinalIgnoreCase) ||
            name.Split(' ').Any(part => part.StartsWith(searchTerm!, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        var result = new
        {
            status = "test_success",
            function = "find_opponent",
            search_term = searchTerm,
            found_opponents = matches,
            total_matches = matches.Count,
            debug_info = $"Search for '{searchTerm}' found {matches.Count} matches"
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }
} 