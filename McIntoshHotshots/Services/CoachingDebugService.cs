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
        string? opponentName;
        try
        {
            using var argsDoc = JsonDocument.Parse(argumentsJson);
            
            // Safely check if the property exists and get its value
            if (!argsDoc.RootElement.TryGetProperty("opponent_name", out var opponentNameElement))
            {
                _logger.LogWarning("Missing 'opponent_name' property in JSON arguments: {ArgumentsJson}", argumentsJson);
                return JsonSerializer.Serialize(new
                {
                    status = "error",
                    function = "get_head_to_head_stats",
                    error_message = "Missing required 'opponent_name' property",
                    debug_info = "The JSON input must contain a 'opponent_name' property"
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            
            opponentName = opponentNameElement.GetString();
            
            // Check if the opponent name is null or empty
            if (string.IsNullOrWhiteSpace(opponentName))
            {
                _logger.LogWarning("Empty or null 'opponent_name' value in JSON arguments: {ArgumentsJson}", argumentsJson);
                return JsonSerializer.Serialize(new
                {
                    status = "error",
                    function = "get_head_to_head_stats",
                    error_message = "The 'opponent_name' property cannot be null or empty",
                    debug_info = "Please provide a valid opponent name"
                }, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON arguments for head-to-head function: {ArgumentsJson}", argumentsJson);
            return JsonSerializer.Serialize(new
            {
                status = "error",
                function = "get_head_to_head_stats",
                error_message = "Invalid JSON format",
                debug_info = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        _logger.LogInformation("Testing head-to-head for opponent: {OpponentName}", opponentName);
        
        var headToHeadData = await _performanceService.GetHeadToHeadDataAsync(userId, opponentName, CancellationToken.None);
        
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
        string? searchTerm;
        try
        {
            using var argsDoc = JsonDocument.Parse(argumentsJson);
            
            // Safely check if the property exists and get its value
            if (!argsDoc.RootElement.TryGetProperty("search_term", out var searchTermElement))
            {
                _logger.LogWarning("Missing 'search_term' property in JSON arguments: {ArgumentsJson}", argumentsJson);
                return JsonSerializer.Serialize(new
                {
                    status = "error",
                    function = "find_opponent",
                    error_message = "Missing required 'search_term' property",
                    debug_info = "The JSON input must contain a 'search_term' property"
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            
            searchTerm = searchTermElement.GetString();
            
            // Check if the search term is null or empty
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                _logger.LogWarning("Empty or null 'search_term' value in JSON arguments: {ArgumentsJson}", argumentsJson);
                return JsonSerializer.Serialize(new
                {
                    status = "error",
                    function = "find_opponent",
                    error_message = "The 'search_term' property cannot be null or empty",
                    debug_info = "Please provide a valid search term"
                }, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON arguments for find opponent function: {ArgumentsJson}", argumentsJson);
            return JsonSerializer.Serialize(new
            {
                status = "error",
                function = "find_opponent",
                error_message = "Invalid JSON format",
                debug_info = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        _logger.LogInformation("Testing find opponent with search term: {SearchTerm}", searchTerm);
        
        var opponentList = await _performanceService.GetOpponentListAsync(userId, CancellationToken.None);
        
        var matches = opponentList.Where(name => 
            name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            searchTerm.Contains(name, StringComparison.OrdinalIgnoreCase) ||
            name.Split(' ').Any(part => part.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
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