namespace McIntoshHotshots.Services;

public class PromptBuilderService : IPromptBuilderService
{
    public string BuildCoachingSystemPrompt(UserPerformanceData performanceData)
    {
        var prompt = BuildBaseCoachingPrompt();
        
        // Add basic player context if available
        if (performanceData.Player != null)
        {
            prompt += BuildPlayerContextPrompt(performanceData);
        }

        return prompt;
    }

    private static string BuildBaseCoachingPrompt()
    {
        return @"You are an expert darts coach that gives realistic, supportive, and data-informed advice. Give BRIEF, CONCISE responses (1-3 sentences max in most cases). 
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

IMPORTANT: Compare actual checkout % to expected range:
- If significantly ABOVE expected: Focus on SCORING improvement (bigger scores, consistency) - their finishing is already good
- If significantly BELOW expected: Focus on FINISHING practice (checkout routines, doubles practice)
- If within expected range: Balanced approach based on their average level

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
- Use get_best_leg_analysis for ""best leg"" questions - this finds the leg completed in FEWEST DARTS (most efficient)
- Use get_any_player_* functions when asked about ANY player (not just opponents you've faced)
- Use get_all_player_names when users want to know what players are available

CRITICAL DISTINCTION - BEST LEG vs HIGHEST FINISH:
- ""Best leg"" = the leg completed in the FEWEST DARTS (e.g., 15 darts to finish 501)
- ""Highest finish"" = the highest checkout score in any single turn (e.g., 170 points in one turn)
- These are COMPLETELY DIFFERENT metrics - do NOT confuse them!
- When users ask ""what is my best leg?"" they want the leg with fewest darts, NOT the highest finish
- When users ask ""what is my highest finish?"" they want the biggest checkout score

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
    }

    private static string BuildPlayerContextPrompt(UserPerformanceData performanceData)
    {
        var contextPrompt = $"\n\nYou are coaching {performanceData.Player!.Name} (ELO: {performanceData.Player.EloNumber}).";
        
        if (performanceData.Stats.TotalMatches > 0)
        {
            contextPrompt += $" They have a {performanceData.Stats.WinPercentage:F0}% win rate with {performanceData.Stats.AverageScore:F0} average and {performanceData.Stats.CheckoutPercentage:F1}% checkout rate.";
            
            // Calculate expected checkout percentage and add specific coaching advice
            var checkoutAnalysis = AnalyzeCheckoutPerformance(performanceData.Stats);
            contextPrompt += checkoutAnalysis;
        }

        return contextPrompt;
    }

    private static string AnalyzeCheckoutPerformance(PerformanceStats stats)
    {
        var avg = stats.AverageScore;
        var actualCheckout = stats.CheckoutPercentage;
        
        var expectations = GetPerformanceExpectations(avg);
        
        // Check if they're finishing significantly above or below expected level
        var expectedCheckoutMid = (expectations.MinCheckout + expectations.MaxCheckout) / 2;
        var checkoutDifference = actualCheckout - expectedCheckoutMid;
        
        if (actualCheckout > expectations.MaxCheckout + 3) // Significantly above expected
        {
            return $" IMPORTANT: Their {actualCheckout:F1}% checkout rate is significantly above the expected {expectations.ExpectedRange} for their average. This suggests they are good finishers but struggle with scoring consistency. Focus advice on improving their average score to match their finishing ability rather than checkout practice. They should work on: hitting bigger scores (60+, 100+), consistent scoring, and reducing low-scoring turns. Their finishing is already a strength.";
        }
        else if (actualCheckout < expectations.MinCheckout - 2) // Significantly below expected  
        {
            return $" Their {actualCheckout:F1}% checkout rate is below the expected {expectations.ExpectedRange} for their average. {expectations.BaseFocus} They need to work on finishing technique and checkout routines.";
        }
        else // Within expected range
        {
            return $" Their {actualCheckout:F1}% checkout rate is appropriate for their {avg:F0} average (expected: {expectations.ExpectedRange}). {expectations.BaseFocus}";
        }
    }

    private static PerformanceExpectation GetPerformanceExpectations(double average)
    {
        return average switch
        {
            <= 40 => new PerformanceExpectation
            {
                MinCheckout = 2,
                MaxCheckout = 5,
                ExpectedRange = "2-5%",
                BaseFocus = "Focus on scoring consistency and hitting big numbers before worrying about checkout percentage."
            },
            <= 50 => new PerformanceExpectation
            {
                MinCheckout = 5,
                MaxCheckout = 13,
                ExpectedRange = "5-13%",
                BaseFocus = "Work on leaving good setup shots and basic finishes like 32, 40, 60."
            },
            <= 60 => new PerformanceExpectation
            {
                MinCheckout = 13,
                MaxCheckout = 20,
                ExpectedRange = "13-20%",
                BaseFocus = "Develop checkout routines and practice common finishes."
            },
            <= 70 => new PerformanceExpectation
            {
                MinCheckout = 20,
                MaxCheckout = 24,
                ExpectedRange = "20-24%",
                BaseFocus = "Work on more advanced finishes and pressure situations."
            },
            <= 80 => new PerformanceExpectation
            {
                MinCheckout = 24,
                MaxCheckout = 35,
                ExpectedRange = "24-35%",
                BaseFocus = "Refine finishing technique and mental game."
            },
            <= 90 => new PerformanceExpectation
            {
                MinCheckout = 35,
                MaxCheckout = 40,
                ExpectedRange = "35-40%",
                BaseFocus = "Advanced finishing and consistency under pressure."
            },
            _ => new PerformanceExpectation
            {
                MinCheckout = 40,
                MaxCheckout = 45,
                ExpectedRange = "40-45%",
                BaseFocus = "Elite-level finishing and tactical play."
            }
        };
    }

    private record PerformanceExpectation
    {
        public double MinCheckout { get; init; }
        public double MaxCheckout { get; init; }
        public string ExpectedRange { get; init; } = "";
        public string BaseFocus { get; init; } = "";
    }
} 