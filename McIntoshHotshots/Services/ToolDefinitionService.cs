using McIntoshHotshots.Model;

namespace McIntoshHotshots.Services;

public class ToolDefinitionService : IToolDefinitionService
{
    public ToolDefinition[] GetAvailableTools()
    {
        return new ToolDefinition[]
        {
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_head_to_head_stats",
                    Description = "Get detailed head-to-head statistics between the current player and a specific opponent. REQUIRES EXACT opponent name - use find_opponent first if you only have a partial name.",
                    Parameters = new ParameterDefinition
                    {
                        Properties = new Dictionary<string, PropertyDefinition>
                        {
                            ["opponent_name"] = new PropertyDefinition
                            {
                                Type = "string",
                                Description = "The name of the opponent to get head-to-head stats against (e.g., 'Chris', 'John')"
                            }
                        },
                        Required = new[] { "opponent_name" }
                    }
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_player_performance",
                    Description = "Get the current player's overall performance statistics, including total matches, matches won, number of unique opponents faced, win rates, scoring averages, checkout percentages, and performance trends"
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_opponent_list",
                    Description = "Get a list of all opponent names the current player has faced in matches"
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "find_opponent",
                    Description = "Search for opponents by partial name (e.g., search 'Chris' to find 'Chris Eldert'). Returns matching opponent names. IMPORTANT: After finding an opponent, you should immediately call get_head_to_head_stats with the exact name found if the user asked for match statistics.",
                    Parameters = new ParameterDefinition
                    {
                        Properties = new Dictionary<string, PropertyDefinition>
                        {
                            ["search_term"] = new PropertyDefinition
                            {
                                Type = "string",
                                Description = "The partial name or search term to find matching opponents (e.g., 'Chris', 'John', 'JR')"
                            }
                        },
                        Required = new[] { "search_term" }
                    }
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_detailed_leg_analysis",
                    Description = "Get detailed throw-by-throw analysis against a specific opponent, including checkout patterns, common scores left, missed opportunities, scoring habits, and specific improvement recommendations based on actual leg detail data. Use this for deep insights beyond basic statistics.",
                    Parameters = new ParameterDefinition
                    {
                        Properties = new Dictionary<string, PropertyDefinition>
                        {
                            ["opponent_name"] = new PropertyDefinition
                            {
                                Type = "string",
                                Description = "The exact name of the opponent to analyze (e.g., 'Chris Eldert', 'Jon Strang')"
                            }
                        },
                        Required = new[] { "opponent_name" }
                    }
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_first_nine_analysis",
                    Description = "Get first 9 darts (first 3 turns) average analysis from actual leg detail data. This is a key darts metric measuring opening performance and consistency. Use this for 'first 9' questions.",
                    Parameters = new ParameterDefinition
                    {
                        Properties = new Dictionary<string, PropertyDefinition>
                        {
                            ["opponent_name"] = new PropertyDefinition
                            {
                                Type = "string",
                                Description = "Optional: The exact name of the opponent to compare first 9 averages against. If not provided, returns overall first 9 analysis."
                            }
                        },
                        Required = new string[] { }
                    }
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_score_down_to_value_analysis",
                    Description = "Analyze how many darts it takes to get down to a specific score value from 501. Common targets: 170 (double-out range), 40 (checkout range), 100, etc. Use this for questions like 'how many darts to get to 170?' or 'how fast do I get to 220 points?'",
                    Parameters = new ParameterDefinition
                    {
                        Properties = new Dictionary<string, PropertyDefinition>
                        {
                            ["target_value"] = new PropertyDefinition
                            {
                                Type = "integer",
                                Description = "The score value to analyze darts required to reach (e.g., 170, 40, 220, 100)"
                            },
                            ["opponent_name"] = new PropertyDefinition
                            {
                                Type = "string",
                                Description = "Optional: The exact name of the opponent to compare pace against. If not provided, returns overall analysis."
                            }
                        },
                        Required = new[] { "target_value" }
                    }
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_any_player_first_nine_analysis",
                    Description = "Get first 9 darts average analysis for ANY player in the system (not just opponents you've faced). Use this when users ask about any player's first 9 performance.",
                    Parameters = new ParameterDefinition
                    {
                        Properties = new Dictionary<string, PropertyDefinition>
                        {
                            ["player_name"] = new PropertyDefinition
                            {
                                Type = "string",
                                Description = "The exact name of any player to analyze (e.g., 'JR Edwards', 'Chris Eldert')"
                            }
                        },
                        Required = new[] { "player_name" }
                    }
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_any_player_score_down_to_value_analysis",
                    Description = "Analyze how many darts it takes for ANY player in the system to get down to a specific score value. Use this when users ask about any player's pace (not just opponents you've faced).",
                    Parameters = new ParameterDefinition
                    {
                        Properties = new Dictionary<string, PropertyDefinition>
                        {
                            ["player_name"] = new PropertyDefinition
                            {
                                Type = "string",
                                Description = "The exact name of any player to analyze (e.g., 'JR Edwards', 'Chris Eldert')"
                            },
                            ["target_value"] = new PropertyDefinition
                            {
                                Type = "integer",
                                Description = "The score value to analyze darts required to reach (e.g., 170, 40, 220, 100)"
                            }
                        },
                        Required = new[] { "player_name", "target_value" }
                    }
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_any_player_performance",
                    Description = "Get overall performance statistics for ANY player in the system (not just opponents you've faced). Returns win rates, averages, ELO, etc.",
                    Parameters = new ParameterDefinition
                    {
                        Properties = new Dictionary<string, PropertyDefinition>
                        {
                            ["player_name"] = new PropertyDefinition
                            {
                                Type = "string",
                                Description = "The exact name of any player to analyze (e.g., 'JR Edwards', 'Chris Eldert')"
                            }
                        },
                        Required = new[] { "player_name" }
                    }
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_all_player_names",
                    Description = "Get a list of all players in the system. Use this when users want to know what players are available to query, or when they ask about 'all players' or want to see who's in the database."
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_average_score_per_turn_down_to_value",
                    Description = "Get average SCORE per 3-dart turn while scoring down to a target value (excludes finishing attempts). This shows scoring efficiency during the scoring phase before throwing at doubles. Use this when users want scoring averages while getting to checkout range.",
                    Parameters = new ParameterDefinition
                    {
                        Properties = new Dictionary<string, PropertyDefinition>
                        {
                            ["target_value"] = new PropertyDefinition
                            {
                                Type = "integer",
                                Description = "The score value to analyze scoring efficiency to (e.g., 40 for checkout range, 170 for double-out range)"
                            },
                            ["opponent_name"] = new PropertyDefinition
                            {
                                Type = "string",
                                Description = "Optional: The exact name of the opponent to compare scoring efficiency against. If not provided, returns overall analysis."
                            }
                        },
                        Required = new[] { "target_value" }
                    }
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_any_player_average_score_per_turn_down_to_value",
                    Description = "Get average SCORE per 3-dart turn for ANY player while scoring down to a target value. Shows their scoring efficiency during the scoring phase before throwing at doubles.",
                    Parameters = new ParameterDefinition
                    {
                        Properties = new Dictionary<string, PropertyDefinition>
                        {
                            ["player_name"] = new PropertyDefinition
                            {
                                Type = "string",
                                Description = "The exact name of any player to analyze (e.g., 'JR Edwards', 'Chris Eldert')"
                            },
                            ["target_value"] = new PropertyDefinition
                            {
                                Type = "integer",
                                Description = "The score value to analyze scoring efficiency to (e.g., 40 for checkout range, 170 for double-out range)"
                            }
                        },
                        Required = new[] { "player_name", "target_value" }
                    }
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_darts_to_win_from_value",
                    Description = "Get the number of darts it takes to WIN from a specific score value (finish the leg). This tracks darts from when you reach a certain score until you finish at 0. Use this when users ask about finishing/winning from a specific score.",
                    Parameters = new ParameterDefinition
                    {
                        Properties = new Dictionary<string, PropertyDefinition>
                        {
                            ["starting_value"] = new PropertyDefinition
                            {
                                Type = "integer",
                                Description = "The score value to analyze finishing from (e.g., 170, 100, 50)"
                            },
                            ["opponent_name"] = new PropertyDefinition
                            {
                                Type = "string",
                                Description = "Optional: The exact name of the opponent to compare finishing speed against. If not provided, returns overall analysis."
                            }
                        },
                        Required = new[] { "starting_value" }
                    }
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_any_player_darts_to_win_from_value",
                    Description = "Get the number of darts it takes for ANY player to WIN from a specific score value (finish the leg). Shows their finishing ability from a certain score down to 0.",
                    Parameters = new ParameterDefinition
                    {
                        Properties = new Dictionary<string, PropertyDefinition>
                        {
                            ["player_name"] = new PropertyDefinition
                            {
                                Type = "string",
                                Description = "The exact name of any player to analyze (e.g., 'JR Edwards', 'Chris Eldert')"
                            },
                            ["starting_value"] = new PropertyDefinition
                            {
                                Type = "integer",
                                Description = "The score value to analyze finishing from (e.g., 170, 100, 50)"
                            }
                        },
                        Required = new[] { "player_name", "starting_value" }
                    }
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_finishing_attempts_from_value",
                    Description = "Get finishing attempts (both successful and failed) from a specific score value. This tracks ALL attempts from a certain score, including when you reach that score but lose the leg. Shows success rate and pressure performance. Use this when users ask about finishing attempts in wins AND losses.",
                    Parameters = new ParameterDefinition
                    {
                        Properties = new Dictionary<string, PropertyDefinition>
                        {
                            ["starting_value"] = new PropertyDefinition
                            {
                                Type = "integer",
                                Description = "The score value to analyze finishing attempts from (e.g., 170, 100, 40)"
                            },
                            ["opponent_name"] = new PropertyDefinition
                            {
                                Type = "string",
                                Description = "Optional: The exact name of the opponent to compare finishing attempts against. If not provided, returns overall analysis."
                            }
                        },
                        Required = new[] { "starting_value" }
                    }
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_any_player_finishing_attempts_from_value",
                    Description = "Get finishing attempts (both successful and failed) for ANY player from a specific score value. Shows their overall finishing success rate and performance under pressure from that position.",
                    Parameters = new ParameterDefinition
                    {
                        Properties = new Dictionary<string, PropertyDefinition>
                        {
                            ["player_name"] = new PropertyDefinition
                            {
                                Type = "string",
                                Description = "The exact name of any player to analyze (e.g., 'JR Edwards', 'Chris Eldert')"
                            },
                            ["starting_value"] = new PropertyDefinition
                            {
                                Type = "integer",
                                Description = "The score value to analyze finishing attempts from (e.g., 170, 100, 40)"
                            }
                        },
                        Required = new[] { "player_name", "starting_value" }
                    }
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_best_leg_analysis",
                    Description = "Get best leg analysis - the leg completed in the FEWEST DARTS (most efficient leg). This is different from highest finish. Use this when users ask about 'best leg', 'fastest leg', or 'most efficient leg'.",
                    Parameters = new ParameterDefinition
                    {
                        Properties = new Dictionary<string, PropertyDefinition>
                        {
                            ["opponent_name"] = new PropertyDefinition
                            {
                                Type = "string",
                                Description = "Optional: The exact name of the opponent to compare best leg performance against. If not provided, returns overall best leg analysis."
                            }
                        },
                        Required = new string[] { }
                    }
                }
            },
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "get_any_player_best_leg_analysis",
                    Description = "Get best leg analysis for ANY player in the system - the leg they completed in the FEWEST DARTS. This is different from highest finish.",
                    Parameters = new ParameterDefinition
                    {
                        Properties = new Dictionary<string, PropertyDefinition>
                        {
                            ["player_name"] = new PropertyDefinition
                            {
                                Type = "string",
                                Description = "The exact name of any player to analyze (e.g., 'JR Edwards', 'Chris Eldert')"
                            }
                        },
                        Required = new[] { "player_name" }
                    }
                }
            }
        };
    }
} 