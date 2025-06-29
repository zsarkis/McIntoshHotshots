namespace McIntoshHotshots.Services;

public class ToolDefinitionService : IToolDefinitionService
{
    public object[] GetAvailableTools()
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
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "get_best_leg_analysis",
                    description = "Get best leg analysis - the leg completed in the FEWEST DARTS (most efficient leg). This is different from highest finish. Use this when users ask about 'best leg', 'fastest leg', or 'most efficient leg'.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            opponent_name = new
                            {
                                type = "string",
                                description = "Optional: The exact name of the opponent to compare best leg performance against. If not provided, returns overall best leg analysis."
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
                    name = "get_any_player_best_leg_analysis",
                    description = "Get best leg analysis for ANY player in the system - the leg they completed in the FEWEST DARTS. This is different from highest finish.",
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
            }
        };
    }
} 