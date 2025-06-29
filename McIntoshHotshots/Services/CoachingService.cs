using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace McIntoshHotshots.Services;

public class CoachingService : ICoachingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IUserPerformanceService _performanceService;
    private readonly IPromptBuilderService _promptBuilderService;
    private readonly ILogger<CoachingService> _logger;
    private readonly IToolDefinitionService _toolDefinitionService;
    private readonly Dictionary<string, Func<string?, string?, CancellationToken, Task<string>>> _functionFactory;

    public CoachingService(
        IHttpClientFactory httpClientFactory, 
        IConfiguration configuration, 
        IUserPerformanceService performanceService,
        IPromptBuilderService promptBuilderService,
        ILogger<CoachingService> logger,
        IToolDefinitionService toolDefinitionService)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _performanceService = performanceService;
        _promptBuilderService = promptBuilderService;
        _logger = logger;
        _toolDefinitionService = toolDefinitionService;
        _functionFactory = InitializeFunctionFactory();
    }

    private Dictionary<string, Func<string?, string?, CancellationToken, Task<string>>> InitializeFunctionFactory()
    {
        return new Dictionary<string, Func<string?, string?, CancellationToken, Task<string>>>
        {
            ["get_head_to_head_stats"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteHeadToHeadFunction(argumentsJson!, userId!, cancellationToken),
            
            ["get_player_performance"] = (argumentsJson, userId, cancellationToken) => 
                ExecutePlayerPerformanceFunction(userId!, cancellationToken),
            
            ["get_opponent_list"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteOpponentListFunction(userId!, cancellationToken),
            
            ["find_opponent"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteFindOpponentFunction(argumentsJson!, userId!, cancellationToken),
            
            ["get_detailed_leg_analysis"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteDetailedLegAnalysisFunction(argumentsJson!, userId!, cancellationToken),
            
            ["get_first_nine_analysis"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteFirstNineAnalysisFunction(argumentsJson!, userId!, cancellationToken),
            
            ["get_score_down_to_value_analysis"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteScoreDownToValueFunction(argumentsJson!, userId!, cancellationToken),
            
            ["get_any_player_first_nine_analysis"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteAnyPlayerFirstNineAnalysisFunction(argumentsJson!, cancellationToken),
            
            ["get_any_player_score_down_to_value_analysis"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteAnyPlayerScoreDownToValueFunction(argumentsJson!, cancellationToken),
            
            ["get_any_player_performance"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteAnyPlayerPerformanceFunction(argumentsJson!, cancellationToken),
            
            ["get_all_player_names"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteGetAllPlayerNamesFunction(cancellationToken),
            
            ["get_average_score_per_turn_down_to_value"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteAverageScorePerTurnDownToValueFunction(argumentsJson!, userId!, cancellationToken),
            
            ["get_any_player_average_score_per_turn_down_to_value"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteAnyPlayerAverageScorePerTurnDownToValueFunction(argumentsJson!, cancellationToken),
            
            ["get_darts_to_win_from_value"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteDartsToWinFromValueFunction(argumentsJson!, userId!, cancellationToken),
            
            ["get_any_player_darts_to_win_from_value"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteAnyPlayerDartsToWinFromValueFunction(argumentsJson!, cancellationToken),
            
            ["get_finishing_attempts_from_value"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteFinishingAttemptsFromValueFunction(argumentsJson!, userId!, cancellationToken),
            
            ["get_any_player_finishing_attempts_from_value"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteAnyPlayerFinishingAttemptsFromValueFunction(argumentsJson!, cancellationToken),
            
            ["get_best_leg_analysis"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteBestLegAnalysisFunction(argumentsJson!, userId!, cancellationToken),
            
            ["get_any_player_best_leg_analysis"] = (argumentsJson, userId, cancellationToken) => 
                ExecuteAnyPlayerBestLegAnalysisFunction(argumentsJson!, cancellationToken)
        };
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
            var systemPrompt = _promptBuilderService.BuildCoachingSystemPrompt(performanceData);

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
                tools = _toolDefinitionService.GetAvailableTools(),
                tool_choice = "auto",
                parallel_tool_calls = true,
                max_tokens = 1000,
                temperature = 0.7
            };

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            // Set timeout for this round
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var httpClient = _httpClientFactory.CreateClient("OpenAI");
            using var response = await httpClient.SendAsync(request, combinedCts.Token);
            
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

            if (_functionFactory.TryGetValue(functionName, out var functionHandler))
            {
                return await functionHandler(argumentsJson, userId, cancellationToken);
            }
            
            _logger.LogWarning("Unknown function called: {FunctionName}", functionName);
            return $"Unknown function: {functionName}";
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

    private async Task<string> ExecuteBestLegAnalysisFunction(string argumentsJson, string userId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing best leg analysis with args: {Args}", argumentsJson);
            
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

            _logger.LogInformation("Getting best leg analysis for user {UserId} vs {OpponentName}", userId, opponentName ?? "All Opponents");
            var analysis = await _performanceService.GetBestLegAnalysisAsync(userId, opponentName, cancellationToken);
            
            _logger.LogInformation("Best leg analysis retrieved: TotalLegs={TotalLegs}, BestLegDarts={BestLegDarts}, AverageDarts={AverageDarts}", 
                analysis.TotalLegsWon, analysis.BestLegDarts, analysis.AverageDartsPerLeg);
            
            if (analysis.TotalLegsWon == 0)
            {
                var message = string.IsNullOrEmpty(opponentName) 
                    ? "No winning legs found for best leg analysis. You may not have won any legs yet."
                    : $"No winning legs found against {opponentName} for best leg analysis. They may not exist in the database or you haven't won any legs against them.";
                _logger.LogInformation("No winning legs found: {Message}", message);
                return message;
            }

            // Create comprehensive response with best leg analysis
            var result = JsonSerializer.Serialize(new
            {
                status = "success",
                opponent_name = analysis.OpponentName,
                is_opponent_comparison = analysis.IsOpponentComparison,
                best_leg_darts = analysis.BestLegDarts,
                worst_leg_darts = analysis.WorstLegDarts,
                average_darts_per_leg = Math.Round(analysis.AverageDartsPerLeg, 1),
                total_legs_won = analysis.TotalLegsWon,
                highest_finish = analysis.HighestFinish,
                opponent_best_leg_darts = analysis.IsOpponentComparison ? analysis.OpponentBestLegDarts : (int?)null,
                opponent_worst_leg_darts = analysis.IsOpponentComparison ? analysis.OpponentWorstLegDarts : (int?)null,
                opponent_average_darts_per_leg = analysis.IsOpponentComparison ? Math.Round(analysis.OpponentAverageDartsPerLeg, 1) : (double?)null,
                opponent_total_legs_won = analysis.IsOpponentComparison ? analysis.OpponentTotalLegsWon : (int?)null,
                winner = analysis.IsOpponentComparison ? analysis.Winner : null,
                difference = analysis.IsOpponentComparison ? Math.Round(analysis.Difference, 1) : (double?)null,
                insights = analysis.Insights,
                summary = analysis.IsOpponentComparison 
                    ? $"Best leg vs {analysis.OpponentName}: You {analysis.BestLegDarts} darts, they {analysis.OpponentBestLegDarts} darts"
                    : $"Your best leg: {analysis.BestLegDarts} darts, average: {analysis.AverageDartsPerLeg:F1} darts across {analysis.TotalLegsWon} winning legs",
                human_readable = analysis.IsOpponentComparison 
                    ? $"Best leg comparison vs {analysis.OpponentName}: Your {analysis.BestLegDarts} vs their {analysis.OpponentBestLegDarts} darts"
                    : $"Your best leg performance: {analysis.BestLegDarts} darts (different from highest finish: {analysis.HighestFinish} points)"
            });
            
            _logger.LogInformation("Best leg analysis completed successfully with result length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteBestLegAnalysisFunction for user {UserId}", userId);
            return $"Error retrieving best leg analysis: {ex.Message}";
        }
    }

    private async Task<string> ExecuteAnyPlayerBestLegAnalysisFunction(string argumentsJson, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing any player best leg analysis with args: {Args}", argumentsJson);
            
            using var argsDoc = JsonDocument.Parse(argumentsJson);
            var playerName = argsDoc.RootElement.GetProperty("player_name").GetString();

            if (string.IsNullOrEmpty(playerName))
            {
                _logger.LogWarning("No player name provided in any player best leg analysis request");
                return "No player name provided";
            }

            _logger.LogInformation("Getting best leg analysis for player: {PlayerName}", playerName);
            var analysis = await _performanceService.GetAnyPlayerBestLegAnalysisAsync(playerName, cancellationToken);
            
            _logger.LogInformation("Any player best leg analysis retrieved: TotalLegs={TotalLegs}, BestLegDarts={BestLegDarts}", 
                analysis.TotalLegsWon, analysis.BestLegDarts);
            
            if (analysis.TotalLegsWon == 0)
            {
                var message = $"No winning legs found for {playerName} for best leg analysis. They may not exist in the database or have no winning legs.";
                _logger.LogInformation("No winning legs found: {Message}", message);
                return message;
            }

            // Create comprehensive response
            var result = JsonSerializer.Serialize(new
            {
                status = "success",
                player_name = playerName,
                best_leg_darts = analysis.BestLegDarts,
                worst_leg_darts = analysis.WorstLegDarts,
                average_darts_per_leg = Math.Round(analysis.AverageDartsPerLeg, 1),
                total_legs_won = analysis.TotalLegsWon,
                highest_finish = analysis.HighestFinish,
                insights = analysis.Insights,
                summary = $"{playerName} best leg: {analysis.BestLegDarts} darts, average: {analysis.AverageDartsPerLeg:F1} darts across {analysis.TotalLegsWon} winning legs",
                human_readable = $"{playerName}'s best leg performance: {analysis.BestLegDarts} darts (different from highest finish: {analysis.HighestFinish} points)"
            });
            
            _logger.LogInformation("Any player best leg analysis completed successfully with result length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteAnyPlayerBestLegAnalysisFunction");
            return $"Error retrieving best leg analysis: {ex.Message}";
        }
    }

}