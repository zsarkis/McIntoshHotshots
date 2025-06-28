using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            var model = _configuration["OpenAI:Model"] ?? "gpt-4o";

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("OpenAI API key is not configured");
                return "I'm sorry, the coaching service is not properly configured. Please contact support.";
            }

            // Get basic user performance data for context
            var performanceData = await _performanceService.GetUserPerformanceDataAsync(userId, cancellationToken);
            
            // Build system prompt with basic user data
            var systemPrompt = BuildSystemPrompt(performanceData);

            // Initialize conversation with user message
            var conversationMessages = new List<object>
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            };

            // Handle potentially multiple rounds of function calling
            return await ProcessConversationWithFunctionCalling(conversationMessages, userId, model, apiKey, cancellationToken);
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

    private async Task<string> ProcessConversationWithFunctionCalling(List<object> conversationMessages, string userId, string model, string apiKey, CancellationToken cancellationToken, int maxRounds = 5)
    {
        var currentRound = 0;
        
        while (currentRound < maxRounds)
        {
            _logger.LogInformation("Starting conversation round {Round}", currentRound + 1);
            
            // Make API call
            var endpoint = "https://api.openai.com/v1/chat/completions";
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = new
            {
                model,
                messages = conversationMessages.ToArray(),
                tools = GetAvailableTools(),
                tool_choice = "auto",
                parallel_tool_calls = true,
                max_tokens = 1000,
                temperature = 0.7
            };

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            // Set timeout for this round
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var response = await _httpClient.SendAsync(request, combinedCts.Token);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("OpenAI API error in round {Round}: {StatusCode} - {Content}", currentRound + 1, response.StatusCode, errorContent);
                return "I'm experiencing some technical difficulties right now. Please try again in a moment.";
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("Round {Round} API response: {Response}", currentRound + 1, json);
            
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                var message = firstChoice.GetProperty("message");

                // Check if the AI wants to call functions
                if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.GetArrayLength() > 0)
                {
                    _logger.LogInformation("Round {Round}: AI wants to call {Count} functions", currentRound + 1, toolCalls.GetArrayLength());
                    
                    // Add assistant message with tool calls to conversation
                    var toolCallsArray = new List<object>();
                    var toolResults = new List<object>();

                    foreach (var toolCall in toolCalls.EnumerateArray())
                    {
                        var toolCallId = toolCall.GetProperty("id").GetString();
                        var functionName = toolCall.GetProperty("function").GetProperty("name").GetString();
                        var functionArgs = toolCall.GetProperty("function").GetProperty("arguments").GetString();

                        toolCallsArray.Add(new
                        {
                            id = toolCallId,
                            type = "function",
                            function = new
                            {
                                name = functionName,
                                arguments = functionArgs
                            }
                        });

                        // Execute the function
                        var result = await ExecuteFunction(functionName, functionArgs, userId, cancellationToken);
                        
                        toolResults.Add(new
                        {
                            tool_call_id = toolCallId,
                            role = "tool",
                            content = result
                        });
                    }

                    // Add the assistant message with tool calls
                    conversationMessages.Add(new { role = "assistant", tool_calls = toolCallsArray.ToArray() });
                    
                    // Add all tool results
                    conversationMessages.AddRange(toolResults);
                    
                    _logger.LogInformation("Round {Round}: Added {ToolCount} function results to conversation", currentRound + 1, toolResults.Count);
                    
                    // Continue to next round
                    currentRound++;
                    continue;
                }

                // No function calls - check for final response
                if (message.TryGetProperty("content", out var content))
                {
                    var result = content.GetString();
                    _logger.LogInformation("Round {Round}: AI response content: '{Content}' (Length: {Length})", currentRound + 1, result, result?.Length ?? 0);
                    
                    if (!string.IsNullOrEmpty(result))
                    {
                        // Check if this is still a "promise" to do work
                        if (IsPromiseResponse(result))
                        {
                            _logger.LogWarning("Round {Round}: AI is still promising to do work instead of doing it: '{Content}'", currentRound + 1, result);
                            
                            // Add this response to conversation and force another round
                            conversationMessages.Add(new { role = "assistant", content = result });
                            
                            // Add a system message to push for action
                            conversationMessages.Add(new { role = "system", content = "Stop narrating. Execute the required function calls NOW to get the data the user requested. Do not respond with promises - take action." });
                            
                            currentRound++;
                            continue;
                        }
                        
                        _logger.LogInformation("Successfully received final coaching response after {Rounds} rounds", currentRound + 1);
                        return result;
                    }
                    else
                    {
                        _logger.LogWarning("Round {Round}: AI response content is null or empty", currentRound + 1);
                    }
                }
                else
                {
                    _logger.LogWarning("Round {Round}: No 'content' property found in AI message", currentRound + 1);
                }
            }
            else
            {
                _logger.LogWarning("Round {Round}: No choices found in API response", currentRound + 1);
            }

            currentRound++;
        }
        
        _logger.LogError("Maximum conversation rounds ({MaxRounds}) exceeded", maxRounds);
        return "I'm having trouble completing your request. The conversation became too complex. Please try asking a simpler question.";
    }

    private bool IsPromiseResponse(string response)
    {
        var lowerResponse = response.ToLower();
        return lowerResponse.Contains("let me get") ||
               lowerResponse.Contains("retrieving") ||
               lowerResponse.Contains("looking up") ||
               lowerResponse.Contains("i'll get") ||
               lowerResponse.Contains("getting your") ||
               lowerResponse.Contains("now i'll") ||
               (lowerResponse.Contains("now") && (lowerResponse.Contains("get") || lowerResponse.Contains("retrieve")));
    }



    private async Task<string> ExecuteFunction(string functionName, string argumentsJson, string userId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing function: {FunctionName} with args: {Args}", functionName, argumentsJson);

            switch (functionName)
            {
                case "get_head_to_head_stats":
                    return await ExecuteHeadToHeadFunction(argumentsJson, userId, cancellationToken);
                
                case "get_player_performance":
                    return await ExecutePlayerPerformanceFunction(userId, cancellationToken);
                
                case "get_opponent_list":
                    return await ExecuteOpponentListFunction(userId, cancellationToken);
                
                case "find_opponent":
                    return await ExecuteFindOpponentFunction(argumentsJson, userId, cancellationToken);
                
                default:
                    _logger.LogWarning("Unknown function called: {FunctionName}", functionName);
                    return $"Unknown function: {functionName}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return $"Error executing function: {ex.Message}";
        }
    }

    private async Task<string> ExecuteHeadToHeadFunction(string argumentsJson, string userId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing head-to-head function with args: {Args}", argumentsJson);
            
            using var argsDoc = JsonDocument.Parse(argumentsJson);
            var opponentName = argsDoc.RootElement.GetProperty("opponent_name").GetString();

            if (string.IsNullOrEmpty(opponentName))
            {
                _logger.LogWarning("No opponent name provided in head-to-head request");
                return "No opponent name provided";
            }

            _logger.LogInformation("Getting head-to-head data for user {UserId} vs {OpponentName}", userId, opponentName);
            var headToHeadData = await _performanceService.GetHeadToHeadDataAsync(userId, opponentName, cancellationToken);
            
            _logger.LogInformation("Head-to-head data retrieved: TotalMatches={TotalMatches}, LegsWon={LegsWon}, LegsLost={LegsLost}", 
                headToHeadData.TotalMatches, headToHeadData.LegsWon, headToHeadData.LegsLost);
            
            if (headToHeadData.TotalMatches == 0)
            {
                var message = $"No match data found against {opponentName}. They may not exist in the database or you haven't played them yet.";
                _logger.LogInformation("No matches found: {Message}", message);
                return message;
            }

            // Create a comprehensive response object
            var responseData = new
            {
                status = "success",
                opponent_name = headToHeadData.OpponentName,
                total_matches = headToHeadData.TotalMatches,
                matches_won = headToHeadData.MatchesWon,
                matches_lost = headToHeadData.MatchesLost,
                win_percentage = Math.Round(headToHeadData.WinPercentage, 1),
                legs_won = headToHeadData.LegsWon,
                legs_lost = headToHeadData.LegsLost,
                leg_win_percentage = Math.Round(headToHeadData.LegWinPercentage, 1),
                player_average_vs_opponent = Math.Round(headToHeadData.AverageScoreVsOpponent, 1),
                opponent_average = Math.Round(headToHeadData.OpponentAverageScore, 1),
                last_match_result = headToHeadData.LastMatchResult,
                performance_trends = headToHeadData.PerformanceTrends,
                summary = $"Against {headToHeadData.OpponentName}: Won {headToHeadData.LegsWon} legs, Lost {headToHeadData.LegsLost} legs. Win rate: {headToHeadData.LegWinPercentage:F1}%",
                human_readable = $"Head-to-head vs {headToHeadData.OpponentName}: {headToHeadData.LegsWon}-{headToHeadData.LegsLost} legs ({headToHeadData.LegWinPercentage:F1}% win rate)"
            };
            
            var result = JsonSerializer.Serialize(responseData);
            
            _logger.LogInformation("Head-to-head function completed successfully with result length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteHeadToHeadFunction for user {UserId}", userId);
            return $"Error retrieving head-to-head data: {ex.Message}";
        }
    }

    private async Task<string> ExecutePlayerPerformanceFunction(string userId, CancellationToken cancellationToken)
    {
        var performanceData = await _performanceService.GetUserPerformanceDataAsync(userId, cancellationToken);
        
        if (performanceData.Player == null)
        {
            return "No player data found for this user";
        }

        var stats = performanceData.Stats;
        return JsonSerializer.Serialize(new
        {
            player_name = performanceData.Player.Name,
            elo_rating = performanceData.Player.EloNumber,
            total_matches = stats.TotalMatches,
            matches_won = stats.MatchesWon,
            unique_opponents = stats.UniqueOpponents,
            win_percentage = Math.Round(stats.WinPercentage, 1),
            average_score = Math.Round(stats.AverageScore, 1),
            checkout_percentage = Math.Round(stats.CheckoutPercentage, 1),
            highest_finish = stats.HighestFinish,
            average_turns_per_leg = Math.Round(stats.AverageTurnsPerLeg, 1),
            common_weak_scores = stats.CommonWeakScores,
            performance_trends = stats.PerformanceTrends
        });
    }

    private async Task<string> ExecuteOpponentListFunction(string userId, CancellationToken cancellationToken)
    {
        var opponentList = await _performanceService.GetOpponentListAsync(userId, cancellationToken);
        
        if (!opponentList.Any())
        {
            return "No opponents found. You haven't played any matches yet.";
        }

        return JsonSerializer.Serialize(new
        {
            total_opponents = opponentList.Count,
            opponent_names = opponentList
        });
    }

    private async Task<string> ExecuteFindOpponentFunction(string argumentsJson, string userId, CancellationToken cancellationToken)
    {
        using var argsDoc = JsonDocument.Parse(argumentsJson);
        var searchTerm = argsDoc.RootElement.GetProperty("search_term").GetString();

        if (string.IsNullOrEmpty(searchTerm))
        {
            return "No search term provided";
        }

        var opponentList = await _performanceService.GetOpponentListAsync(userId, cancellationToken);
        
        if (!opponentList.Any())
        {
            return "No opponents found in your match history.";
        }

        // Find matching opponents (case-insensitive, partial matching)
        var matches = opponentList.Where(name => 
            name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            searchTerm.Contains(name, StringComparison.OrdinalIgnoreCase) ||
            name.Split(' ').Any(part => part.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        return JsonSerializer.Serialize(new
        {
            search_term = searchTerm,
            found_opponents = matches,
            total_matches = matches.Count,
            all_opponents = opponentList
        });
    }

    private object[] GetAvailableTools()
    {
        return new object[]
        {
            new
            {
                type = "function",
                function = new
                {
                    name = "get_head_to_head_stats",
                    description = "Get detailed head-to-head statistics between the current player and a specific opponent. REQUIRES EXACT opponent name - use find_opponent first if you only have a partial name.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            opponent_name = new
                            {
                                type = "string",
                                description = "The name of the opponent to get head-to-head stats against (e.g., 'Chris', 'John')"
                            }
                        },
                        required = new[] { "opponent_name" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "get_player_performance",
                    description = "Get the current player's overall performance statistics, including total matches, matches won, number of unique opponents faced, win rates, scoring averages, checkout percentages, and performance trends"
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "get_opponent_list",
                    description = "Get a list of all opponent names the current player has faced in matches"
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "find_opponent",
                    description = "Search for opponents by partial name (e.g., search 'Chris' to find 'Chris Eldert'). Returns matching opponent names. IMPORTANT: After finding an opponent, you should immediately call get_head_to_head_stats with the exact name found if the user asked for match statistics.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            search_term = new
                            {
                                type = "string",
                                description = "The partial name or search term to find matching opponents (e.g., 'Chris', 'John', 'JR')"
                            }
                        },
                        required = new[] { "search_term" }
                    }
                }
            }
        };
    }

    private string BuildSystemPrompt(UserPerformanceData performanceData)
    {
        var prompt = @"You are an expert darts coach. Give BRIEF, CONCISE responses (1-3 sentences max in most cases). 
Be direct and specific. No lengthy explanations unless specifically requested.

Your expertise: technique, mental game, finishing, practice routines, tactics.

CRITICAL FUNCTION CALLING RULES:
1. NEVER respond with promises like 'Let me get...', 'Retrieving...', 'I'll look up...', etc.
2. If you need data, call the appropriate functions IMMEDIATELY
3. When a user asks about opponents with partial names, you must complete BOTH steps:
   - First: call find_opponent() to get the exact name
   - Second: call get_head_to_head_stats() with the exact name you found
4. Only respond with TEXT after you have ALL the data the user requested
5. Answer the user's original question directly using the retrieved data

WORKFLOW EXAMPLES:
- User: 'What is my leg count against Chris?'
  → Step 1: Call find_opponent('Chris') → Get 'Chris Eldert'
  → Step 2: Call get_head_to_head_stats('Chris Eldert') → Get leg data  
  → Step 3: Present the actual leg count numbers to the user

- User: 'Show me my performance'
  → Step 1: Call get_player_performance() → Get performance data
  → Step 2: Present the performance statistics to the user

DO NOT NARRATE YOUR ACTIONS - JUST EXECUTE THE FUNCTIONS AND PRESENT RESULTS.";

        // Add basic player context if available
        if (performanceData.Player != null)
        {
            prompt += $"\n\nYou are coaching {performanceData.Player.Name} (ELO: {performanceData.Player.EloNumber}).";
            
            if (performanceData.Stats.TotalMatches > 0)
            {
                prompt += $" They have a {performanceData.Stats.WinPercentage:F0}% win rate with {performanceData.Stats.AverageScore:F0} average.";
            }
        }

        return prompt;
    }
}