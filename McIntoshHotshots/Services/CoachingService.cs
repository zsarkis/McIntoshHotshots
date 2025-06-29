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
                
                case "get_detailed_leg_analysis":
                    return await ExecuteDetailedLegAnalysisFunction(argumentsJson, userId, cancellationToken);
                
                case "get_first_nine_analysis":
                    return await ExecuteFirstNineAnalysisFunction(argumentsJson, userId, cancellationToken);
                
                case "get_score_down_to_value_analysis":
                    return await ExecuteScoreDownToValueFunction(argumentsJson, userId, cancellationToken);
                
                case "get_any_player_first_nine_analysis":
                    return await ExecuteAnyPlayerFirstNineAnalysisFunction(argumentsJson, cancellationToken);
                
                case "get_any_player_score_down_to_value_analysis":
                    return await ExecuteAnyPlayerScoreDownToValueFunction(argumentsJson, cancellationToken);
                
                case "get_any_player_performance":
                    return await ExecuteAnyPlayerPerformanceFunction(argumentsJson, cancellationToken);
                
                case "get_all_player_names":
                    return await ExecuteGetAllPlayerNamesFunction(cancellationToken);
                
                case "get_average_score_per_turn_down_to_value":
                    return await ExecuteAverageScorePerTurnDownToValueFunction(argumentsJson, userId, cancellationToken);
                
                case "get_any_player_average_score_per_turn_down_to_value":
                    return await ExecuteAnyPlayerAverageScorePerTurnDownToValueFunction(argumentsJson, cancellationToken);
                
                case "get_darts_to_win_from_value":
                    return await ExecuteDartsToWinFromValueFunction(argumentsJson, userId, cancellationToken);
                
                case "get_any_player_darts_to_win_from_value":
                    return await ExecuteAnyPlayerDartsToWinFromValueFunction(argumentsJson, cancellationToken);
                
                case "get_finishing_attempts_from_value":
                    return await ExecuteFinishingAttemptsFromValueFunction(argumentsJson, userId, cancellationToken);
                
                case "get_any_player_finishing_attempts_from_value":
                    return await ExecuteAnyPlayerFinishingAttemptsFromValueFunction(argumentsJson, cancellationToken);
                
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

    private async Task<string> ExecuteDetailedLegAnalysisFunction(string argumentsJson, string userId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing detailed leg analysis with args: {Args}", argumentsJson);
            
            using var argsDoc = JsonDocument.Parse(argumentsJson);
            var opponentName = argsDoc.RootElement.GetProperty("opponent_name").GetString();

            if (string.IsNullOrEmpty(opponentName))
            {
                _logger.LogWarning("No opponent name provided in detailed leg analysis request");
                return "No opponent name provided";
            }

            _logger.LogInformation("Getting detailed leg analysis for user {UserId} vs {OpponentName}", userId, opponentName);
            var analysis = await _performanceService.GetDetailedLegAnalysisAsync(userId, opponentName, cancellationToken);
            
            _logger.LogInformation("Detailed leg analysis retrieved: TotalLegs={TotalLegs}, CheckoutRate={CheckoutRate}", 
                analysis.TotalLegs, analysis.CheckoutSuccessRate);
            
            if (analysis.TotalLegs == 0)
            {
                var message = $"No detailed leg data found against {opponentName}. They may not exist in the database or you haven't played them yet.";
                _logger.LogInformation("No legs found: {Message}", message);
                return message;
            }

            // Create a comprehensive response with detailed insights
            var result = JsonSerializer.Serialize(new
            {
                status = "success",
                opponent_name = analysis.OpponentName,
                total_legs = analysis.TotalLegs,
                checkout_success_rate = Math.Round(analysis.CheckoutSuccessRate, 1),
                average_turns_per_leg = Math.Round(analysis.AverageTurnsPerLeg, 1),
                highest_finish = analysis.HighestFinish,
                successful_checkouts = analysis.SuccessfulCheckouts,
                missed_checkouts = analysis.MissedCheckouts,
                common_scores_left = analysis.CommonScoresLeft,
                scoring_patterns = analysis.ScoringPatterns,
                strengths = analysis.Strengths,
                weak_areas = analysis.WeakAreas,
                specific_insights = analysis.SpecificInsights,
                summary = $"Detailed analysis vs {analysis.OpponentName}: {analysis.TotalLegs} legs, {analysis.CheckoutSuccessRate:F1}% checkout rate, avg {analysis.AverageTurnsPerLeg:F1} turns per leg",
                human_readable = $"Analyzed {analysis.TotalLegs} legs against {analysis.OpponentName} with specific throw-by-throw insights"
            });
            
            _logger.LogInformation("Detailed leg analysis completed successfully with result length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteDetailedLegAnalysisFunction for user {UserId}", userId);
            return $"Error retrieving detailed leg analysis: {ex.Message}";
        }
    }

    private async Task<string> ExecuteFirstNineAnalysisFunction(string argumentsJson, string userId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing first nine analysis with args: {Args}", argumentsJson);
            
            string? opponentName = null;
            
            // Parse arguments if provided
            if (!string.IsNullOrEmpty(argumentsJson) && argumentsJson.Trim() != "{}")
            {
                using var argsDoc = JsonDocument.Parse(argumentsJson);
                if (argsDoc.RootElement.TryGetProperty("opponent_name", out var opponentNameElement))
                {
                    opponentName = opponentNameElement.GetString();
                }
            }

            _logger.LogInformation("Getting first nine analysis for user {UserId} vs {OpponentName}", userId, opponentName ?? "All Opponents");
            var analysis = await _performanceService.GetFirstNineAnalysisAsync(userId, opponentName, cancellationToken);
            
            _logger.LogInformation("First nine analysis retrieved: TotalLegs={TotalLegs}, PlayerAverage={PlayerAverage}, OpponentAverage={OpponentAverage}", 
                analysis.TotalLegsAnalyzed, analysis.PlayerFirstNineAverage, analysis.OpponentFirstNineAverage);
            
            if (analysis.TotalLegsAnalyzed == 0)
            {
                var message = string.IsNullOrEmpty(opponentName) 
                    ? "No leg data found for first 9 analysis. You may not have any completed legs in the database."
                    : $"No leg data found against {opponentName} for first 9 analysis. They may not exist in the database or you haven't played them yet.";
                _logger.LogInformation("No legs found: {Message}", message);
                return message;
            }

            // Create comprehensive response with first 9 analysis
            var result = JsonSerializer.Serialize(new
            {
                status = "success",
                opponent_name = analysis.OpponentName,
                is_opponent_comparison = analysis.IsOpponentComparison,
                player_first_nine_average = Math.Round(analysis.PlayerFirstNineAverage, 1),
                opponent_first_nine_average = analysis.IsOpponentComparison ? Math.Round(analysis.OpponentFirstNineAverage, 1) : (double?)null,
                total_legs_analyzed = analysis.TotalLegsAnalyzed,
                winner = analysis.IsOpponentComparison ? analysis.Winner : null,
                difference = analysis.IsOpponentComparison ? Math.Round(analysis.Difference, 1) : (double?)null,
                player_first_nine_averages = analysis.PlayerFirstNineAverages,
                opponent_first_nine_averages = analysis.IsOpponentComparison ? analysis.OpponentFirstNineAverages : null,
                insights = analysis.Insights,
                summary = analysis.IsOpponentComparison 
                    ? $"First 9 vs {analysis.OpponentName}: You average {analysis.PlayerFirstNineAverage:F1}, they average {analysis.OpponentFirstNineAverage:F1} (difference: {(analysis.Difference > 0 ? "+" : "")}{analysis.Difference:F1})"
                    : $"Overall first 9 average: {analysis.PlayerFirstNineAverage:F1} across {analysis.TotalLegsAnalyzed} legs",
                human_readable = analysis.IsOpponentComparison 
                    ? $"First 9 darts comparison vs {analysis.OpponentName}: Your {analysis.PlayerFirstNineAverage:F1} vs their {analysis.OpponentFirstNineAverage:F1}"
                    : $"Your overall first 9 average: {analysis.PlayerFirstNineAverage:F1} points"
            });
            
            _logger.LogInformation("First nine analysis completed successfully with result length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteFirstNineAnalysisFunction for user {UserId}", userId);
            return $"Error retrieving first nine analysis: {ex.Message}";
        }
    }

    private async Task<string> ExecuteScoreDownToValueFunction(string argumentsJson, string userId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing score down to value analysis with args: {Args}", argumentsJson);
            
            int targetValue = 170; // Default to 170 (double-out range)
            string? opponentName = null;
            
            // Parse arguments
            if (!string.IsNullOrEmpty(argumentsJson) && argumentsJson.Trim() != "{}")
            {
                using var argsDoc = JsonDocument.Parse(argumentsJson);
                
                if (argsDoc.RootElement.TryGetProperty("target_value", out var targetElement))
                {
                    targetValue = targetElement.GetInt32();
                }
                
                if (argsDoc.RootElement.TryGetProperty("opponent_name", out var opponentElement))
                {
                    opponentName = opponentElement.GetString();
                }
            }

            _logger.LogInformation("Getting score down to value analysis for user {UserId} vs {OpponentName} target: {TargetValue}", userId, opponentName ?? "All Opponents", targetValue);
            var analysis = await _performanceService.GetScoreDownToValueAnalysisAsync(userId, targetValue, opponentName, cancellationToken);
            
            _logger.LogInformation("Score down to value analysis retrieved: TotalLegs={TotalLegs}, PlayerAverage={PlayerAverage}, OpponentAverage={OpponentAverage}", 
                analysis.TotalLegsAnalyzed, analysis.PlayerAverageDarts, analysis.OpponentAverageDarts);
            
            if (analysis.TotalLegsAnalyzed == 0)
            {
                var message = string.IsNullOrEmpty(opponentName) 
                    ? $"No leg data found for score down to {targetValue} analysis. You may not have any completed legs in the database."
                    : $"No leg data found against {opponentName} for score down to {targetValue} analysis. They may not exist in the database or you haven't played them yet.";
                _logger.LogInformation("No legs found: {Message}", message);
                return message;
            }

            // Create comprehensive response with score down to value analysis
            var result = JsonSerializer.Serialize(new
            {
                status = "success",
                target_value = analysis.TargetValue,
                opponent_name = analysis.OpponentName,
                is_opponent_comparison = analysis.IsOpponentComparison,
                player_average_darts = Math.Round(analysis.PlayerAverageDarts, 1),
                opponent_average_darts = analysis.IsOpponentComparison ? Math.Round(analysis.OpponentAverageDarts, 1) : (double?)null,
                total_legs_analyzed = analysis.TotalLegsAnalyzed,
                winner = analysis.IsOpponentComparison ? analysis.Winner : null,
                difference = analysis.IsOpponentComparison ? Math.Round(analysis.Difference, 1) : (double?)null,
                player_fastest_darts = analysis.PlayerFastestDarts,
                player_slowest_darts = analysis.PlayerSlowestDarts,
                opponent_fastest_darts = analysis.IsOpponentComparison ? analysis.OpponentFastestDarts : (double?)null,
                opponent_slowest_darts = analysis.IsOpponentComparison ? analysis.OpponentSlowestDarts : (double?)null,
                insights = analysis.Insights,
                summary = analysis.IsOpponentComparison 
                    ? $"Darts to {targetValue}: You average {analysis.PlayerAverageDarts:F1}, they average {analysis.OpponentAverageDarts:F1} (difference: {(analysis.Difference > 0 ? "+" : "")}{analysis.Difference:F1})"
                    : $"Average darts to reach {targetValue}: {analysis.PlayerAverageDarts:F1} across {analysis.TotalLegsAnalyzed} legs",
                human_readable = analysis.IsOpponentComparison 
                    ? $"Darts to reach {targetValue} vs {analysis.OpponentName}: Your {analysis.PlayerAverageDarts:F1} vs their {analysis.OpponentAverageDarts:F1}"
                    : $"Your average darts to reach {targetValue}: {analysis.PlayerAverageDarts:F1}"
            });
            
            _logger.LogInformation("Score down to value analysis completed successfully with result length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteScoreDownToValueFunction for user {UserId}", userId);
            return $"Error retrieving score down to value analysis: {ex.Message}";
        }
    }

    private async Task<string> ExecuteAnyPlayerFirstNineAnalysisFunction(string argumentsJson, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing any player first nine analysis with args: {Args}", argumentsJson);
            
            using var argsDoc = JsonDocument.Parse(argumentsJson);
            var playerName = argsDoc.RootElement.GetProperty("player_name").GetString();

            if (string.IsNullOrEmpty(playerName))
            {
                _logger.LogWarning("No player name provided in any player first nine analysis request");
                return "No player name provided";
            }

            _logger.LogInformation("Getting first nine analysis for player: {PlayerName}", playerName);
            var analysis = await _performanceService.GetAnyPlayerFirstNineAnalysisAsync(playerName, cancellationToken);
            
            _logger.LogInformation("Any player first nine analysis retrieved: TotalLegs={TotalLegs}, PlayerAverage={PlayerAverage}", 
                analysis.TotalLegsAnalyzed, analysis.PlayerFirstNineAverage);
            
            if (analysis.TotalLegsAnalyzed == 0)
            {
                var message = $"No leg data found for {playerName} for first 9 analysis. They may not exist in the database or have no completed legs.";
                _logger.LogInformation("No legs found: {Message}", message);
                return message;
            }

            // Create comprehensive response
            var result = JsonSerializer.Serialize(new
            {
                status = "success",
                player_name = playerName,
                first_nine_average = Math.Round(analysis.PlayerFirstNineAverage, 1),
                total_legs_analyzed = analysis.TotalLegsAnalyzed,
                insights = analysis.Insights,
                summary = $"{playerName}'s overall first 9 average: {analysis.PlayerFirstNineAverage:F1} across {analysis.TotalLegsAnalyzed} legs",
                human_readable = $"{playerName} averages {analysis.PlayerFirstNineAverage:F1} per turn in their first 9 darts"
            });
            
            _logger.LogInformation("Any player first nine analysis completed successfully with result length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteAnyPlayerFirstNineAnalysisFunction");
            return $"Error retrieving first nine analysis: {ex.Message}";
        }
    }

    private async Task<string> ExecuteAnyPlayerScoreDownToValueFunction(string argumentsJson, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing any player score down to value analysis with args: {Args}", argumentsJson);
            
            using var argsDoc = JsonDocument.Parse(argumentsJson);
            var playerName = argsDoc.RootElement.GetProperty("player_name").GetString();
            var targetValue = argsDoc.RootElement.GetProperty("target_value").GetInt32();

            if (string.IsNullOrEmpty(playerName))
            {
                _logger.LogWarning("No player name provided in any player score down to value analysis request");
                return "No player name provided";
            }

            _logger.LogInformation("Getting score down to value analysis for player: {PlayerName} target: {TargetValue}", playerName, targetValue);
            var analysis = await _performanceService.GetAnyPlayerScoreDownToValueAnalysisAsync(playerName, targetValue, cancellationToken);
            
            _logger.LogInformation("Any player score down to value analysis retrieved: TotalLegs={TotalLegs}, PlayerAverage={PlayerAverage}", 
                analysis.TotalLegsAnalyzed, analysis.PlayerAverageDarts);
            
            if (analysis.TotalLegsAnalyzed == 0)
            {
                var message = $"No leg data found for {playerName} for score down to {targetValue} analysis. They may not exist in the database or have no completed legs.";
                _logger.LogInformation("No legs found: {Message}", message);
                return message;
            }

            // Create comprehensive response
            var result = JsonSerializer.Serialize(new
            {
                status = "success",
                player_name = playerName,
                target_value = analysis.TargetValue,
                average_darts = Math.Round(analysis.PlayerAverageDarts, 1),
                total_legs_analyzed = analysis.TotalLegsAnalyzed,
                fastest_darts = analysis.PlayerFastestDarts,
                slowest_darts = analysis.PlayerSlowestDarts,
                insights = analysis.Insights,
                summary = $"{playerName} averages {analysis.PlayerAverageDarts:F1} darts to reach {targetValue} across {analysis.TotalLegsAnalyzed} legs",
                human_readable = $"{playerName} takes an average of {analysis.PlayerAverageDarts:F1} darts to get down to {targetValue}"
            });
            
            _logger.LogInformation("Any player score down to value analysis completed successfully with result length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteAnyPlayerScoreDownToValueFunction");
            return $"Error retrieving score down to value analysis: {ex.Message}";
        }
    }

    private async Task<string> ExecuteAnyPlayerPerformanceFunction(string argumentsJson, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing any player performance with args: {Args}", argumentsJson);
            
            using var argsDoc = JsonDocument.Parse(argumentsJson);
            var playerName = argsDoc.RootElement.GetProperty("player_name").GetString();

            if (string.IsNullOrEmpty(playerName))
            {
                _logger.LogWarning("No player name provided in any player performance request");
                return "No player name provided";
            }

            _logger.LogInformation("Getting performance data for player: {PlayerName}", playerName);
            var performanceData = await _performanceService.GetAnyPlayerPerformanceDataAsync(playerName, cancellationToken);
            
            if (performanceData.Player == null)
            {
                var message = $"No player found with name: {playerName}";
                _logger.LogInformation("No player found: {Message}", message);
                return message;
            }

            var stats = performanceData.Stats;
            var result = JsonSerializer.Serialize(new
            {
                status = "success",
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
                performance_trends = stats.PerformanceTrends,
                summary = $"{performanceData.Player.Name}: {stats.WinPercentage:F1}% win rate, {stats.AverageScore:F1} average, {stats.TotalMatches} matches",
                human_readable = $"{performanceData.Player.Name} has played {stats.TotalMatches} matches with a {stats.WinPercentage:F1}% win rate and {stats.AverageScore:F1} scoring average"
            });
            
            _logger.LogInformation("Any player performance completed successfully with result length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteAnyPlayerPerformanceFunction");
            return $"Error retrieving player performance: {ex.Message}";
        }
    }

    private async Task<string> ExecuteGetAllPlayerNamesFunction(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing get all player names");
            
            var playerNames = await _performanceService.GetAllPlayerNamesAsync(cancellationToken);
            
            _logger.LogInformation("Retrieved {Count} player names", playerNames.Count);
            
            if (!playerNames.Any())
            {
                return "No players found in the system.";
            }

            var result = JsonSerializer.Serialize(new
            {
                status = "success",
                total_players = playerNames.Count,
                player_names = playerNames,
                summary = $"Found {playerNames.Count} players in the system",
                human_readable = $"Players in the system: {string.Join(", ", playerNames)}"
            });
            
            _logger.LogInformation("Get all player names completed successfully with {Count} players", playerNames.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteGetAllPlayerNamesFunction");
            return $"Error retrieving player names: {ex.Message}";
        }
    }

    private async Task<string> ExecuteAverageScorePerTurnDownToValueFunction(string argumentsJson, string userId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing average score per turn down to value with args: {Args}", argumentsJson);
            
            int targetValue = 40; // Default to 40 (checkout range)
            string? opponentName = null;
            
            // Parse arguments
            if (!string.IsNullOrEmpty(argumentsJson) && argumentsJson.Trim() != "{}")
            {
                using var argsDoc = JsonDocument.Parse(argumentsJson);
                
                if (argsDoc.RootElement.TryGetProperty("target_value", out var targetElement))
                {
                    targetValue = targetElement.GetInt32();
                }
                
                if (argsDoc.RootElement.TryGetProperty("opponent_name", out var opponentElement))
                {
                    opponentName = opponentElement.GetString();
                }
            }

            _logger.LogInformation("Getting average score per turn analysis for user {UserId} vs {OpponentName} target: {TargetValue}", userId, opponentName ?? "All Opponents", targetValue);
            var analysis = await _performanceService.GetAverageScorePerTurnDownToValueAsync(userId, targetValue, opponentName, cancellationToken);
            
            _logger.LogInformation("Average score per turn analysis retrieved: TotalLegs={TotalLegs}, PlayerAverage={PlayerAverage}, OpponentAverage={OpponentAverage}", 
                analysis.TotalLegsAnalyzed, analysis.PlayerAverageScorePerTurn, analysis.OpponentAverageScorePerTurn);
            
            if (analysis.TotalLegsAnalyzed == 0)
            {
                var message = string.IsNullOrEmpty(opponentName) 
                    ? $"No leg data found for average score per turn to {targetValue} analysis. You may not have any completed legs in the database."
                    : $"No leg data found against {opponentName} for average score per turn to {targetValue} analysis. They may not exist in the database or you haven't played them yet.";
                _logger.LogInformation("No legs found: {Message}", message);
                return message;
            }

            // Create comprehensive response
            var result = JsonSerializer.Serialize(new
            {
                status = "success",
                target_value = analysis.TargetValue,
                opponent_name = analysis.OpponentName,
                is_opponent_comparison = analysis.IsOpponentComparison,
                player_average_score_per_turn = Math.Round(analysis.PlayerAverageScorePerTurn, 1),
                opponent_average_score_per_turn = analysis.IsOpponentComparison ? Math.Round(analysis.OpponentAverageScorePerTurn, 1) : (double?)null,
                total_legs_analyzed = analysis.TotalLegsAnalyzed,
                winner = analysis.IsOpponentComparison ? analysis.Winner : null,
                difference = analysis.IsOpponentComparison ? Math.Round(analysis.Difference, 1) : (double?)null,
                player_best_leg_average = analysis.PlayerBestLegAverage,
                player_worst_leg_average = analysis.PlayerWorstLegAverage,
                opponent_best_leg_average = analysis.IsOpponentComparison ? analysis.OpponentBestLegAverage : (double?)null,
                opponent_worst_leg_average = analysis.IsOpponentComparison ? analysis.OpponentWorstLegAverage : (double?)null,
                insights = analysis.Insights,
                summary = analysis.IsOpponentComparison 
                    ? $"Score per turn to {targetValue}: You average {analysis.PlayerAverageScorePerTurn:F1}, they average {analysis.OpponentAverageScorePerTurn:F1} (difference: {(analysis.Difference > 0 ? "+" : "")}{analysis.Difference:F1})"
                    : $"Average score per turn to reach {targetValue}: {analysis.PlayerAverageScorePerTurn:F1} across {analysis.TotalLegsAnalyzed} legs",
                human_readable = analysis.IsOpponentComparison 
                    ? $"Score per turn to {targetValue} vs {analysis.OpponentName}: Your {analysis.PlayerAverageScorePerTurn:F1} vs their {analysis.OpponentAverageScorePerTurn:F1}"
                    : $"Your average score per turn while scoring down to {targetValue}: {analysis.PlayerAverageScorePerTurn:F1} points"
            });
            
            _logger.LogInformation("Average score per turn analysis completed successfully with result length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteAverageScorePerTurnDownToValueFunction for user {UserId}", userId);
            return $"Error retrieving average score per turn analysis: {ex.Message}";
        }
    }

    private async Task<string> ExecuteAnyPlayerAverageScorePerTurnDownToValueFunction(string argumentsJson, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing any player average score per turn down to value with args: {Args}", argumentsJson);
            
            using var argsDoc = JsonDocument.Parse(argumentsJson);
            var playerName = argsDoc.RootElement.GetProperty("player_name").GetString();
            var targetValue = argsDoc.RootElement.GetProperty("target_value").GetInt32();

            if (string.IsNullOrEmpty(playerName))
            {
                _logger.LogWarning("No player name provided in any player average score per turn analysis request");
                return "No player name provided";
            }

            _logger.LogInformation("Getting average score per turn analysis for player: {PlayerName} target: {TargetValue}", playerName, targetValue);
            var analysis = await _performanceService.GetAnyPlayerAverageScorePerTurnDownToValueAsync(playerName, targetValue, cancellationToken);
            
            _logger.LogInformation("Any player average score per turn analysis retrieved: TotalLegs={TotalLegs}, PlayerAverage={PlayerAverage}", 
                analysis.TotalLegsAnalyzed, analysis.PlayerAverageScorePerTurn);
            
            if (analysis.TotalLegsAnalyzed == 0)
            {
                var message = $"No leg data found for {playerName} for average score per turn to {targetValue} analysis. They may not exist in the database or have no completed legs.";
                _logger.LogInformation("No legs found: {Message}", message);
                return message;
            }

            // Create comprehensive response
            var result = JsonSerializer.Serialize(new
            {
                status = "success",
                player_name = playerName,
                target_value = analysis.TargetValue,
                average_score_per_turn = Math.Round(analysis.PlayerAverageScorePerTurn, 1),
                total_legs_analyzed = analysis.TotalLegsAnalyzed,
                best_leg_average = analysis.PlayerBestLegAverage,
                worst_leg_average = analysis.PlayerWorstLegAverage,
                insights = analysis.Insights,
                summary = $"{playerName} averages {analysis.PlayerAverageScorePerTurn:F1} points per turn while scoring down to {targetValue} across {analysis.TotalLegsAnalyzed} legs",
                human_readable = $"{playerName} averages {analysis.PlayerAverageScorePerTurn:F1} points per turn while scoring down to {targetValue}"
            });
            
            _logger.LogInformation("Any player average score per turn analysis completed successfully with result length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteAnyPlayerAverageScorePerTurnDownToValueFunction");
            return $"Error retrieving average score per turn analysis: {ex.Message}";
        }
    }

    private async Task<string> ExecuteDartsToWinFromValueFunction(string argumentsJson, string userId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing darts to win from value with args: {Args}", argumentsJson);
            
            using var argsDoc = JsonDocument.Parse(argumentsJson);
            var startingValue = argsDoc.RootElement.GetProperty("starting_value").GetInt32();
            
            string? opponentName = null;
            if (argsDoc.RootElement.TryGetProperty("opponent_name", out var opponentElement))
            {
                opponentName = opponentElement.GetString();
            }

            _logger.LogInformation("Getting darts to win from value analysis for user {UserId} vs {OpponentName} starting: {StartingValue}", userId, opponentName ?? "All Opponents", startingValue);
            var analysis = await _performanceService.GetDartsToWinFromValueAnalysisAsync(userId, startingValue, opponentName, cancellationToken);
            
            _logger.LogInformation("Darts to win from value analysis retrieved: TotalLegs={TotalLegs}, PlayerAverage={PlayerAverage}, OpponentAverage={OpponentAverage}", 
                analysis.TotalLegsAnalyzed, analysis.PlayerAverageDarts, analysis.OpponentAverageDarts);
            
            if (analysis.TotalLegsAnalyzed == 0)
            {
                var message = string.IsNullOrEmpty(opponentName) 
                    ? $"No legs found where you reached {startingValue} and won. You may not have won any legs from that position yet."
                    : $"No legs found against {opponentName} where you reached {startingValue} and won. They may not exist in the database or you haven't won legs from that position against them.";
                _logger.LogInformation("No legs found: {Message}", message);
                return message;
            }

            // Create comprehensive response
            var result = JsonSerializer.Serialize(new
            {
                status = "success",
                starting_value = analysis.TargetValue,
                opponent_name = analysis.OpponentName,
                is_opponent_comparison = analysis.IsOpponentComparison,
                player_average_darts = Math.Round(analysis.PlayerAverageDarts, 1),
                player_fastest_darts = analysis.PlayerFastestDarts,
                player_slowest_darts = analysis.PlayerSlowestDarts,
                opponent_average_darts = analysis.IsOpponentComparison ? Math.Round(analysis.OpponentAverageDarts, 1) : (double?)null,
                opponent_fastest_darts = analysis.IsOpponentComparison ? analysis.OpponentFastestDarts : (double?)null,
                opponent_slowest_darts = analysis.IsOpponentComparison ? analysis.OpponentSlowestDarts : (double?)null,
                total_legs_analyzed = analysis.TotalLegsAnalyzed,
                winner = analysis.IsOpponentComparison ? analysis.Winner : null,
                difference = analysis.IsOpponentComparison ? Math.Round(analysis.Difference, 1) : (double?)null,
                insights = analysis.Insights,
                summary = analysis.IsOpponentComparison 
                    ? $"Darts to win from {startingValue}: You average {analysis.PlayerAverageDarts:F1}, they average {analysis.OpponentAverageDarts:F1} (difference: {(analysis.Difference < 0 ? "" : "+")}{analysis.Difference:F1})"
                    : $"Darts to win from {startingValue}: Average {analysis.PlayerAverageDarts:F1}, fastest {analysis.PlayerFastestDarts}, slowest {analysis.PlayerSlowestDarts}",
                human_readable = analysis.IsOpponentComparison 
                    ? $"Finishing from {startingValue} vs {analysis.OpponentName}: Your {analysis.PlayerAverageDarts:F1} darts vs their {analysis.OpponentAverageDarts:F1} darts"
                    : $"Your finishing from {startingValue}: {analysis.PlayerAverageDarts:F1} darts average (fastest: {analysis.PlayerFastestDarts}, slowest: {analysis.PlayerSlowestDarts})"
            });
            
            _logger.LogInformation("Darts to win from value analysis completed successfully with result length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteDartsToWinFromValueFunction for user {UserId}", userId);
            return $"Error retrieving darts to win from value analysis: {ex.Message}";
        }
    }

    private async Task<string> ExecuteAnyPlayerDartsToWinFromValueFunction(string argumentsJson, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing any player darts to win from value with args: {Args}", argumentsJson);
            
            using var argsDoc = JsonDocument.Parse(argumentsJson);
            var playerName = argsDoc.RootElement.GetProperty("player_name").GetString();
            var startingValue = argsDoc.RootElement.GetProperty("starting_value").GetInt32();

            if (string.IsNullOrEmpty(playerName))
            {
                _logger.LogWarning("No player name provided in any player darts to win from value request");
                return "No player name provided";
            }

            _logger.LogInformation("Getting darts to win from value analysis for player: {PlayerName} starting: {StartingValue}", playerName, startingValue);
            var analysis = await _performanceService.GetAnyPlayerDartsToWinFromValueAnalysisAsync(playerName, startingValue, cancellationToken);
            
            _logger.LogInformation("Any player darts to win from value analysis retrieved: TotalLegs={TotalLegs}, PlayerAverage={PlayerAverage}", 
                analysis.TotalLegsAnalyzed, analysis.PlayerAverageDarts);
            
            if (analysis.TotalLegsAnalyzed == 0)
            {
                var message = $"No legs found where {playerName} reached {startingValue} and won. They may not exist in the database or haven't won legs from that position.";
                _logger.LogInformation("No legs found: {Message}", message);
                return message;
            }

            // Create comprehensive response
            var result = JsonSerializer.Serialize(new
            {
                status = "success",
                player_name = playerName,
                starting_value = analysis.TargetValue,
                average_darts = Math.Round(analysis.PlayerAverageDarts, 1),
                fastest_darts = analysis.PlayerFastestDarts,
                slowest_darts = analysis.PlayerSlowestDarts,
                total_legs_analyzed = analysis.TotalLegsAnalyzed,
                insights = analysis.Insights,
                summary = $"{playerName} averages {analysis.PlayerAverageDarts:F1} darts to win from {startingValue} (fastest: {analysis.PlayerFastestDarts}, slowest: {analysis.PlayerSlowestDarts})",
                human_readable = $"{playerName} takes {analysis.PlayerAverageDarts:F1} darts on average to win from {startingValue}"
            });
            
            _logger.LogInformation("Any player darts to win from value analysis completed successfully with result length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteAnyPlayerDartsToWinFromValueFunction");
            return $"Error retrieving darts to win from value analysis: {ex.Message}";
        }
    }

    private async Task<string> ExecuteFinishingAttemptsFromValueFunction(string argumentsJson, string userId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing finishing attempts from value with args: {Args}", argumentsJson);
            
            using var argsDoc = JsonDocument.Parse(argumentsJson);
            var startingValue = argsDoc.RootElement.GetProperty("starting_value").GetInt32();
            
            string? opponentName = null;
            if (argsDoc.RootElement.TryGetProperty("opponent_name", out var opponentElement))
            {
                opponentName = opponentElement.GetString();
            }

            _logger.LogInformation("Getting finishing attempts analysis for user {UserId} vs {OpponentName} starting: {StartingValue}", userId, opponentName ?? "All Opponents", startingValue);
            var analysis = await _performanceService.GetFinishingAttemptsFromValueAnalysisAsync(userId, startingValue, opponentName, cancellationToken);
            
            _logger.LogInformation("Finishing attempts analysis retrieved: TotalAttempts={TotalAttempts}, SuccessRate={SuccessRate}%", 
                analysis.TotalAttempts, analysis.SuccessRate);
            
            if (analysis.TotalAttempts == 0)
            {
                var message = string.IsNullOrEmpty(opponentName) 
                    ? $"No attempts found where you reached {startingValue}. You may not have reached that score in any legs yet."
                    : $"No attempts found against {opponentName} where you reached {startingValue}. They may not exist in the database or you haven't reached that score against them.";
                _logger.LogInformation("No attempts found: {Message}", message);
                return message;
            }

            // Create comprehensive response
            var result = JsonSerializer.Serialize(new
            {
                status = "success",
                starting_value = analysis.StartingValue,
                opponent_name = analysis.OpponentName,
                is_opponent_comparison = analysis.IsOpponentComparison,
                total_attempts = analysis.TotalAttempts,
                successful_finishes = analysis.SuccessfulFinishes,
                failed_attempts = analysis.FailedAttempts,
                success_rate = Math.Round(analysis.SuccessRate, 1),
                average_darts_in_wins = analysis.AverageDartsInWins > 0 ? Math.Round(analysis.AverageDartsInWins, 1) : (double?)null,
                average_darts_in_losses = analysis.AverageDartsInLosses > 0 ? Math.Round(analysis.AverageDartsInLosses, 1) : (double?)null,
                fastest_successful_finish = analysis.FastestSuccessfulFinish > 0 ? analysis.FastestSuccessfulFinish : (double?)null,
                slowest_successful_finish = analysis.SlowestSuccessfulFinish > 0 ? analysis.SlowestSuccessfulFinish : (double?)null,
                opponent_total_attempts = analysis.IsOpponentComparison ? analysis.OpponentTotalAttempts : (int?)null,
                opponent_success_rate = analysis.IsOpponentComparison ? Math.Round(analysis.OpponentSuccessRate, 1) : (double?)null,
                insights = analysis.Insights,
                summary = $"From {startingValue}: {analysis.SuccessfulFinishes}/{analysis.TotalAttempts} successful ({analysis.SuccessRate:F1}%)",
                human_readable = analysis.IsOpponentComparison 
                    ? $"Finishing attempts from {startingValue} vs {analysis.OpponentName}: {analysis.SuccessRate:F1}% success rate vs their {analysis.OpponentSuccessRate:F1}%"
                    : $"Your finishing attempts from {startingValue}: {analysis.SuccessfulFinishes} wins, {analysis.FailedAttempts} losses ({analysis.SuccessRate:F1}% success rate)"
            });
            
            _logger.LogInformation("Finishing attempts analysis completed successfully with result length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteFinishingAttemptsFromValueFunction for user {UserId}", userId);
            return $"Error retrieving finishing attempts analysis: {ex.Message}";
        }
    }

    private async Task<string> ExecuteAnyPlayerFinishingAttemptsFromValueFunction(string argumentsJson, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing any player finishing attempts from value with args: {Args}", argumentsJson);
            
            using var argsDoc = JsonDocument.Parse(argumentsJson);
            var playerName = argsDoc.RootElement.GetProperty("player_name").GetString();
            var startingValue = argsDoc.RootElement.GetProperty("starting_value").GetInt32();

            if (string.IsNullOrEmpty(playerName))
            {
                _logger.LogWarning("No player name provided in any player finishing attempts request");
                return "No player name provided";
            }

            _logger.LogInformation("Getting finishing attempts analysis for player: {PlayerName} starting: {StartingValue}", playerName, startingValue);
            var analysis = await _performanceService.GetAnyPlayerFinishingAttemptsFromValueAnalysisAsync(playerName, startingValue, cancellationToken);
            
            _logger.LogInformation("Any player finishing attempts analysis retrieved: TotalAttempts={TotalAttempts}, SuccessRate={SuccessRate}%", 
                analysis.TotalAttempts, analysis.SuccessRate);
            
            if (analysis.TotalAttempts == 0)
            {
                var message = $"No attempts found where {playerName} reached {startingValue}. They may not exist in the database or haven't reached that score in any legs.";
                _logger.LogInformation("No attempts found: {Message}", message);
                return message;
            }

            // Create comprehensive response
            var result = JsonSerializer.Serialize(new
            {
                status = "success",
                player_name = playerName,
                starting_value = analysis.StartingValue,
                total_attempts = analysis.TotalAttempts,
                successful_finishes = analysis.SuccessfulFinishes,
                failed_attempts = analysis.FailedAttempts,
                success_rate = Math.Round(analysis.SuccessRate, 1),
                average_darts_in_wins = analysis.AverageDartsInWins > 0 ? Math.Round(analysis.AverageDartsInWins, 1) : (double?)null,
                average_darts_in_losses = analysis.AverageDartsInLosses > 0 ? Math.Round(analysis.AverageDartsInLosses, 1) : (double?)null,
                fastest_successful_finish = analysis.FastestSuccessfulFinish > 0 ? analysis.FastestSuccessfulFinish : (double?)null,
                slowest_successful_finish = analysis.SlowestSuccessfulFinish > 0 ? analysis.SlowestSuccessfulFinish : (double?)null,
                insights = analysis.Insights,
                summary = $"{playerName} from {startingValue}: {analysis.SuccessfulFinishes}/{analysis.TotalAttempts} successful ({analysis.SuccessRate:F1}%)",
                human_readable = $"{playerName}'s finishing attempts from {startingValue}: {analysis.SuccessRate:F1}% success rate ({analysis.SuccessfulFinishes} wins, {analysis.FailedAttempts} losses)"
            });
            
            _logger.LogInformation("Any player finishing attempts analysis completed successfully with result length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteAnyPlayerFinishingAttemptsFromValueFunction");
            return $"Error retrieving finishing attempts analysis: {ex.Message}";
        }
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
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "get_detailed_leg_analysis",
                    description = "Get detailed throw-by-throw analysis against a specific opponent, including checkout patterns, common scores left, missed opportunities, scoring habits, and specific improvement recommendations based on actual leg detail data. Use this for deep insights beyond basic statistics.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            opponent_name = new
                            {
                                type = "string",
                                description = "The exact name of the opponent to analyze (e.g., 'Chris Eldert', 'Jon Strang')"
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
                    name = "get_first_nine_analysis",
                    description = "Get first 9 darts (first 3 turns) average analysis from actual leg detail data. This is a key darts metric measuring opening performance and consistency. Use this for 'first 9' questions.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            opponent_name = new
                            {
                                type = "string",
                                description = "Optional: The exact name of the opponent to compare first 9 averages against. If not provided, returns overall first 9 analysis."
                            }
                        },
                        required = new string[] { }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "get_score_down_to_value_analysis",
                    description = "Analyze how many darts it takes to get down to a specific score value from 501. Common targets: 170 (double-out range), 40 (checkout range), 100, etc. Use this for questions like 'how many darts to get to 170?' or 'how fast do I get to 220 points?'",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            target_value = new
                            {
                                type = "integer",
                                description = "The score value to analyze darts required to reach (e.g., 170, 40, 220, 100)"
                            },
                            opponent_name = new
                            {
                                type = "string",
                                description = "Optional: The exact name of the opponent to compare pace against. If not provided, returns overall analysis."
                            }
                        },
                        required = new[] { "target_value" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "get_any_player_first_nine_analysis",
                    description = "Get first 9 darts average analysis for ANY player in the system (not just opponents you've faced). Use this when users ask about any player's first 9 performance.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            player_name = new
                            {
                                type = "string",
                                description = "The exact name of any player to analyze (e.g., 'JR Edwards', 'Chris Eldert')"
                            }
                        },
                        required = new[] { "player_name" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "get_any_player_score_down_to_value_analysis",
                    description = "Analyze how many darts it takes for ANY player in the system to get down to a specific score value. Use this when users ask about any player's pace (not just opponents you've faced).",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            player_name = new
                            {
                                type = "string",
                                description = "The exact name of any player to analyze (e.g., 'JR Edwards', 'Chris Eldert')"
                            },
                            target_value = new
                            {
                                type = "integer",
                                description = "The score value to analyze darts required to reach (e.g., 170, 40, 220, 100)"
                            }
                        },
                        required = new[] { "player_name", "target_value" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "get_any_player_performance",
                    description = "Get overall performance statistics for ANY player in the system (not just opponents you've faced). Returns win rates, averages, ELO, etc.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            player_name = new
                            {
                                type = "string",
                                description = "The exact name of any player to analyze (e.g., 'JR Edwards', 'Chris Eldert')"
                            }
                        },
                        required = new[] { "player_name" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "get_all_player_names",
                    description = "Get a list of all players in the system. Use this when users want to know what players are available to query, or when they ask about 'all players' or want to see who's in the database."
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "get_average_score_per_turn_down_to_value",
                    description = "Get average SCORE per 3-dart turn while scoring down to a target value (excludes finishing attempts). This shows scoring efficiency during the scoring phase before throwing at doubles. Use this when users want scoring averages while getting to checkout range.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            target_value = new
                            {
                                type = "integer",
                                description = "The score value to analyze scoring efficiency to (e.g., 40 for checkout range, 170 for double-out range)"
                            },
                            opponent_name = new
                            {
                                type = "string",
                                description = "Optional: The exact name of the opponent to compare scoring efficiency against. If not provided, returns overall analysis."
                            }
                        },
                        required = new[] { "target_value" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "get_any_player_average_score_per_turn_down_to_value",
                    description = "Get average SCORE per 3-dart turn for ANY player while scoring down to a target value. Shows their scoring efficiency during the scoring phase before throwing at doubles.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            player_name = new
                            {
                                type = "string",
                                description = "The exact name of any player to analyze (e.g., 'JR Edwards', 'Chris Eldert')"
                            },
                            target_value = new
                            {
                                type = "integer",
                                description = "The score value to analyze scoring efficiency to (e.g., 40 for checkout range, 170 for double-out range)"
                            }
                        },
                        required = new[] { "player_name", "target_value" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "get_darts_to_win_from_value",
                    description = "Get the number of darts it takes to WIN from a specific score value (finish the leg). This tracks darts from when you reach a certain score until you finish at 0. Use this when users ask about finishing/winning from a specific score.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            starting_value = new
                            {
                                type = "integer",
                                description = "The score value to analyze finishing from (e.g., 170, 100, 50)"
                            },
                            opponent_name = new
                            {
                                type = "string",
                                description = "Optional: The exact name of the opponent to compare finishing speed against. If not provided, returns overall analysis."
                            }
                        },
                        required = new[] { "starting_value" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "get_any_player_darts_to_win_from_value",
                    description = "Get the number of darts it takes for ANY player to WIN from a specific score value (finish the leg). Shows their finishing ability from a certain score down to 0.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            player_name = new
                            {
                                type = "string",
                                description = "The exact name of any player to analyze (e.g., 'JR Edwards', 'Chris Eldert')"
                            },
                            starting_value = new
                            {
                                type = "integer",
                                description = "The score value to analyze finishing from (e.g., 170, 100, 50)"
                            }
                        },
                        required = new[] { "player_name", "starting_value" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "get_finishing_attempts_from_value",
                    description = "Get finishing attempts (both successful and failed) from a specific score value. This tracks ALL attempts from a certain score, including when you reach that score but lose the leg. Shows success rate and pressure performance. Use this when users ask about finishing attempts in wins AND losses.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            starting_value = new
                            {
                                type = "integer",
                                description = "The score value to analyze finishing attempts from (e.g., 170, 100, 40)"
                            },
                            opponent_name = new
                            {
                                type = "string",
                                description = "Optional: The exact name of the opponent to compare finishing attempts against. If not provided, returns overall analysis."
                            }
                        },
                        required = new[] { "starting_value" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "get_any_player_finishing_attempts_from_value",
                    description = "Get finishing attempts (both successful and failed) for ANY player from a specific score value. Shows their overall finishing success rate and performance under pressure from that position.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            player_name = new
                            {
                                type = "string",
                                description = "The exact name of any player to analyze (e.g., 'JR Edwards', 'Chris Eldert')"
                            },
                            starting_value = new
                            {
                                type = "integer",
                                description = "The score value to analyze finishing attempts from (e.g., 170, 100, 40)"
                            }
                        },
                        required = new[] { "player_name", "starting_value" }
                    }
                }
            }
        };
    }

    private string BuildSystemPrompt(UserPerformanceData performanceData)
    {
        var prompt = @"You are an expert darts coach that gives realistic, supportive, and data-informed advice. Give BRIEF, CONCISE responses (1-3 sentences max in most cases). 
Be direct and specific. No lengthy explanations unless specifically requested.

Your expertise: technique, mental game, finishing, practice routines, tactics.

REALISTIC PERFORMANCE EXPECTATIONS:
Use these benchmarks when setting goals or evaluating performance:
- 30-40 average: 2-5% checkout rate
- 41-50 average: 5-13% checkout rate  
- 51-60 average: 13-20% checkout rate
- 61-70 average: 20-24% checkout rate
- 71-80 average: 24-35% checkout rate
- 81-90 average: 35-40% checkout rate
- 91-100 average: 40-45% checkout rate

Most amateur players should prioritize:
- Improving scoring consistency (fewer low-scoring turns)
- Learning to leave good finish numbers (170, 132, 96, etc.)
- Developing basic checkout routines from common finishes
Always provide next-step goals that match their current level. Avoid giving pro-level advice unless stats show elite performance.

CRITICAL FUNCTION CALLING RULES:
1. NEVER respond with promises like ""Let me get..."", ""Retrieving..."", ""I'll look up..."", etc.
2. If you need data, call the appropriate functions IMMEDIATELY
3. When a user asks about opponents with partial names, you must complete BOTH steps:
   - First: call find_opponent() to get the exact name
   - Second: call get_head_to_head_stats() with the exact name you found
4. Only respond with TEXT after you have ALL the data the user requested
5. Answer the user's original question directly using the retrieved data

WORKFLOW EXAMPLES:
- User: ""What is my leg count against Chris?""
  → Step 1: Call find_opponent(""Chris"") → Get ""Chris Eldert""
  → Step 2: Call get_head_to_head_stats(""Chris Eldert"") → Get leg data  
  → Step 3: Present the actual leg count numbers to the user

- User: ""What should I work on against Jon?"" or ""Why do I struggle against Chris?""
  → Step 1: Call find_opponent if needed
  → Step 2: Call get_detailed_leg_analysis(""Jon Strang"") → Get throw-by-throw insights
  → Step 3: Present specific recommendations based on actual checkout patterns and scoring data

- User: ""How many darts to get to 170?"" or ""How fast do I get down to 220 points?""
  → Step 1: Call get_score_down_to_value_analysis(target_value: 170) → Get pace analysis
  → Step 2: Present actual dart count data and insights

- User: ""Who gets down to 170 faster out of me and Jon?""
  → Step 1: Call find_opponent if needed → Get ""Jon Strang""
  → Step 2: Call get_score_down_to_value_analysis(target_value: 170, opponent_name: ""Jon Strang"")
  → Step 3: Present comparison data

- User: ""Show me my performance""
  → Step 1: Call get_player_performance() → Get performance data
  → Step 2: Present the performance statistics to the user

IMPORTANT: 
- Use get_detailed_leg_analysis for improvement areas, specific weaknesses, checkout problems
- Use get_first_nine_analysis for ""first 9"" questions (shows average score per 3-dart turn, not total)
- Use get_score_down_to_value_analysis for pace questions (e.g., ""how many darts to get to 170?"")
- Use get_average_score_per_turn_down_to_value for scoring efficiency questions (e.g., ""what's my average score per turn down to 40?"")
- Use get_darts_to_win_from_value for finishing questions (e.g., ""how many darts to win from 170?"") - ONLY SUCCESSFUL FINISHES
- Use get_finishing_attempts_from_value for ALL finishing attempts including losses (e.g., ""how many darts in my losses from 170?"")
- Use get_any_player_* functions when asked about ANY player (not just opponents you've faced)
- Use get_all_player_names when users want to know what players are available

EXAMPLES OF ANY PLAYER QUERIES:
- ""What is JR's overall first 9?"" → Use get_any_player_first_nine_analysis(""JR Edwards"")
- ""How fast does Chris get to 170?"" → Use get_any_player_score_down_to_value_analysis(""Chris Eldert"", 170)
- ""What's my average score per turn down to 40?"" → Use get_average_score_per_turn_down_to_value(40)
- ""What's Jon's scoring average while getting to checkout range?"" → Use get_any_player_average_score_per_turn_down_to_value(""Jon Strang"", 40)
- ""How many darts to win from 170?"" → Use get_darts_to_win_from_value(170) [ONLY successful finishes]
- ""How fast does Chris finish from 100?"" → Use get_any_player_darts_to_win_from_value(""Chris Eldert"", 100) [ONLY successful finishes]
- ""How many darts do I throw from 170 in my losses?"" → Use get_finishing_attempts_from_value(170) [ALL attempts, wins and losses]
- ""What's Chris's success rate from 40?"" → Use get_any_player_finishing_attempts_from_value(""Chris Eldert"", 40) [ALL attempts, wins and losses]
- ""What are all the players?"" → Use get_all_player_names()
- ""Show me Jon's stats"" → Use get_any_player_performance(""Jon Strang"")

DO NOT NARRATE YOUR ACTIONS - JUST EXECUTE THE FUNCTIONS AND PRESENT RESULTS.";

        // Add basic player context if available
        if (performanceData.Player != null)
        {
            prompt += $"\n\nYou are coaching {performanceData.Player.Name} (ELO: {performanceData.Player.EloNumber}).";
            
            if (performanceData.Stats.TotalMatches > 0)
            {
                prompt += $" They have a {performanceData.Stats.WinPercentage:F0}% win rate with {performanceData.Stats.AverageScore:F0} average.";
                
                // Add realistic expectations based on their actual average
                var avg = performanceData.Stats.AverageScore;
                string expectedCheckout = "";
                string focus = "";
                
                if (avg <= 40)
                {
                    expectedCheckout = "2-5%";
                    focus = "Focus on scoring consistency and hitting big numbers before worrying about checkout percentage.";
                }
                else if (avg <= 50)
                {
                    expectedCheckout = "5-13%";
                    focus = "Work on leaving good setup shots and basic finishes like 32, 40, 60.";
                }
                else if (avg <= 60)
                {
                    expectedCheckout = "13-20%";
                    focus = "Develop checkout routines and practice common finishes.";
                }
                else if (avg <= 70)
                {
                    expectedCheckout = "20-24%";
                    focus = "Work on more advanced finishes and pressure situations.";
                }
                else if (avg <= 80)
                {
                    expectedCheckout = "24-35%";
                    focus = "Refine finishing technique and mental game.";
                }
                else if (avg <= 90)
                {
                    expectedCheckout = "35-40%";
                    focus = "Advanced finishing and consistency under pressure.";
                }
                else
                {
                    expectedCheckout = "40-45%";
                    focus = "Elite-level finishing and tactical play.";
                }
                
                prompt += $" At their level, a realistic checkout rate would be {expectedCheckout}. {focus}";
            }
        }

        return prompt;
    }
}