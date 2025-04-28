using McIntoshHotshots.Model;
using McIntoshHotshots.Repo;

namespace McIntoshHotshots.Services;

public interface IEloCalculationService
{
    Task<int> CalculateNewEloAsync(int playerId, bool isWinner, int opponentElo);
    Task UpdatePlayerEloAfterMatchAsync(int homePlayerId, int awayPlayerId, bool homePlayerWon);
    Task RecalculateAllPlayersEloAsync();
    Task<Dictionary<string, int>> GetPlayerEloRankingsAsync();
    Task ResetAllPlayersEloAsync(int startingElo = 1500);
}

public class EloCalculationService : IEloCalculationService
{
    private readonly IPlayerRepo _playerRepo;
    private readonly IMatchSummaryRepo _matchRepo;
    private readonly ILegRepo _legRepo;
    
    // Dart-specific ELO constants
    private const int DefaultK = 20; // Default K-factor, can be adjusted based on player experience
    private const int DefaultElo = 1500; // Starting ELO for new players
    private const int MinElo = 1200; // Minimum ELO rating a player can have
    
    public EloCalculationService(IPlayerRepo playerRepo, IMatchSummaryRepo matchRepo, ILegRepo legRepo)
    {
        _playerRepo = playerRepo;
        _matchRepo = matchRepo;
        _legRepo = legRepo;
    }
    
    /// <summary>
    /// Calculates a new ELO rating for a player based on match result and performance
    /// </summary>
    public async Task<int> CalculateNewEloAsync(int playerId, bool isWinner, int opponentElo)
    {
        var player = await _playerRepo.GetPlayerByIdAsync(playerId);
        if (player == null)
        {
            throw new ArgumentException($"Player with ID {playerId} not found");
        }
        
        // Step 1: Calculate expected outcome
        double expectedOutcome = 1.0 / (1.0 + Math.Pow(10, (opponentElo - player.EloNumber) / 400.0));
        
        // Step 2: Determine actual outcome (with margin factor)
        // For now, we use a simple 0 or 1 outcome, but this could be enhanced with leg differences
        double actualOutcome = isWinner ? 1.0 : 0.0;
        
        // Step 3: Determine K-factor based on player experience
        int matchesCount = await GetPlayerMatchCountAsync(playerId);
        int kFactor = DetermineKFactor(matchesCount);
        
        // Step 4: Update ELO rating
        int eloChange = (int)Math.Round(kFactor * (actualOutcome - expectedOutcome));
        int newElo = Math.Max(MinElo, player.EloNumber + eloChange);
        
        return newElo;
    }
    
    /// <summary>
    /// Determines appropriate K-factor based on player experience
    /// </summary>
    private int DetermineKFactor(int matchesPlayed)
    {
        // New players (higher volatility)
        if (matchesPlayed < 10)
            return 40;
        
        // Regular players
        if (matchesPlayed < 30)
            return 30;
        
        // Established players (more stable ratings)
        return DefaultK;
    }
    
    /// <summary>
    /// Gets the total number of matches a player has participated in
    /// </summary>
    private async Task<int> GetPlayerMatchCountAsync(int playerId)
    {
        var matches = await _matchRepo.GetMatchesByPlayerIdAsync(playerId);
        return matches.Count;
    }
    
    /// <summary>
    /// Updates both players' ELO ratings after a match, with margin considerations
    /// </summary>
    public async Task UpdatePlayerEloAfterMatchAsync(int homePlayerId, int awayPlayerId, bool homePlayerWon)
    {
        var homePlayer = await _playerRepo.GetPlayerByIdAsync(homePlayerId);
        var awayPlayer = await _playerRepo.GetPlayerByIdAsync(awayPlayerId);
        
        if (homePlayer == null || awayPlayer == null)
        {
            throw new ArgumentException("One or both players not found");
        }
        
        // Get match details to calculate margin
        var match = await GetMatchBetweenPlayersAsync(homePlayerId, awayPlayerId);
        if (match == null)
        {
            // Fallback to simple win/loss if we can't find detailed match info
            int simpleHomeElo = await CalculateNewEloAsync(homePlayerId, homePlayerWon, awayPlayer.EloNumber);
            int simpleAwayElo = await CalculateNewEloAsync(awayPlayerId, !homePlayerWon, homePlayer.EloNumber);
            
            await UpdatePlayerEloRatingAsync(homePlayer, simpleHomeElo);
            await UpdatePlayerEloRatingAsync(awayPlayer, simpleAwayElo);
            return;
        }
        
        // Calculate leg difference as a margin factor
        int legDifference = Math.Abs(match.HomeLegsWon - match.AwayLegsWon);
        double marginFactor = CalculateMarginFactor(legDifference);
        
        // Calculate enhanced outcomes with margin considerations
        double expectedHomeOutcome = 1.0 / (1.0 + Math.Pow(10, (awayPlayer.EloNumber - homePlayer.EloNumber) / 400.0));
        double expectedAwayOutcome = 1.0 - expectedHomeOutcome;
        
        // Actual outcome with margin enhancement
        double actualHomeOutcome = homePlayerWon ? 0.5 + (marginFactor / 2.0) : 0.5 - (marginFactor / 2.0);
        double actualAwayOutcome = 1.0 - actualHomeOutcome;
        
        // Determine K-factors for both players
        int homeMatchCount = await GetPlayerMatchCountAsync(homePlayerId);
        int awayMatchCount = await GetPlayerMatchCountAsync(awayPlayerId);
        int homeKFactor = DetermineKFactor(homeMatchCount);
        int awayKFactor = DetermineKFactor(awayMatchCount);
        
        // Calculate new ratings
        int homeEloChange = (int)Math.Round(homeKFactor * (actualHomeOutcome - expectedHomeOutcome));
        int awayEloChange = (int)Math.Round(awayKFactor * (actualAwayOutcome - expectedAwayOutcome));
        
        int newHomeElo = Math.Max(MinElo, homePlayer.EloNumber + homeEloChange);
        int newAwayElo = Math.Max(MinElo, awayPlayer.EloNumber + awayEloChange);
        
        // Update player ratings
        await UpdatePlayerEloRatingAsync(homePlayer, newHomeElo);
        await UpdatePlayerEloRatingAsync(awayPlayer, newAwayElo);
    }
    
    /// <summary>
    /// Calculates a margin factor based on leg difference
    /// </summary>
    private double CalculateMarginFactor(int legDifference)
    {
        // Scale the margin factor based on leg difference
        // A close match (1 leg difference) = small margin (0.1)
        // A dominant win (5+ leg difference) = large margin (0.5)
        return Math.Min(0.5, 0.1 * legDifference);
    }
    
    /// <summary>
    /// Updates a player's ELO rating while preserving other data
    /// </summary>
    private async Task UpdatePlayerEloRatingAsync(PlayerModel player, int newElo)
    {
        var playerUpdate = new PlayerModel
        {
            Id = player.Id,
            Name = player.Name,
            Earnings = player.Earnings,
            EloNumber = newElo,
            Preferences = player.Preferences,
            UserId = player.UserId
        };
        
        await _playerRepo.UpdatePlayerAsync(playerUpdate);
    }
    
    /// <summary>
    /// Gets the most recent match between two players
    /// </summary>
    private async Task<MatchSummaryModel> GetMatchBetweenPlayersAsync(int player1Id, int player2Id)
    {
        var allMatches = await _matchRepo.GetMatchSummariesAsync();
        
        return allMatches
            .Where(m => 
                (m.HomePlayerId == player1Id && m.AwayPlayerId == player2Id) || 
                (m.HomePlayerId == player2Id && m.AwayPlayerId == player1Id))
            .OrderByDescending(m => m.Id)
            .FirstOrDefault();
    }
    
    /// <summary>
    /// Recalculates ELO ratings for all players based on match history
    /// </summary>
    public async Task RecalculateAllPlayersEloAsync()
    {
        // Reset all players to the default ELO
        await ResetAllPlayersEloAsync(DefaultElo);
        
        // Get all matches ordered by date/ID
        var allMatches = (await _matchRepo.GetMatchSummariesAsync())
            .OrderBy(m => m.Id)
            .ToList();
        
        if (!allMatches.Any())
        {
            return; // No matches to process
        }
        
        // Dictionary to track each player's current ELO through the recalculation process
        var playerCurrentElo = new Dictionary<int, int>();
        
        // Initialize the dictionary with default ELO values
        var players = await _playerRepo.GetPlayersAsync();
        foreach (var player in players)
        {
            playerCurrentElo[player.Id] = DefaultElo;
        }
        
        // Process matches in chronological order
        foreach (var match in allMatches)
        {
            // Skip matches with invalid data
            if (match.HomePlayerId <= 0 || match.AwayPlayerId <= 0 || 
                match.HomeLegsWon < 0 || match.AwayLegsWon < 0)
            {
                continue;
            }
            
            try
            {
                // Get player information
                var homePlayer = await _playerRepo.GetPlayerByIdAsync(match.HomePlayerId);
                var awayPlayer = await _playerRepo.GetPlayerByIdAsync(match.AwayPlayerId);
                
                if (homePlayer == null || awayPlayer == null)
                {
                    continue; // Skip if either player is missing
                }
                
                // Get current in-memory ELO values
                if (!playerCurrentElo.ContainsKey(homePlayer.Id))
                    playerCurrentElo[homePlayer.Id] = DefaultElo;
                
                if (!playerCurrentElo.ContainsKey(awayPlayer.Id))
                    playerCurrentElo[awayPlayer.Id] = DefaultElo;
                
                int homePlayerElo = playerCurrentElo[homePlayer.Id];
                int awayPlayerElo = playerCurrentElo[awayPlayer.Id];
                
                // Determine winner
                bool homePlayerWon = match.HomeLegsWon > match.AwayLegsWon;
                
                // Calculate expected outcomes
                double expectedHomeOutcome = 1.0 / (1.0 + Math.Pow(10, (awayPlayerElo - homePlayerElo) / 400.0));
                double expectedAwayOutcome = 1.0 - expectedHomeOutcome;
                
                // Calculate leg difference for margin factor
                int legDifference = Math.Abs(match.HomeLegsWon - match.AwayLegsWon);
                double marginFactor = CalculateMarginFactor(legDifference);
                
                // Actual outcome with margin enhancement
                double actualHomeOutcome = homePlayerWon ? 0.5 + (marginFactor / 2.0) : 0.5 - (marginFactor / 2.0);
                double actualAwayOutcome = 1.0 - actualHomeOutcome;
                
                // Determine K-factors
                int homeMatchCount = await GetPlayerMatchCountAsync(match.HomePlayerId);
                int awayMatchCount = await GetPlayerMatchCountAsync(match.AwayPlayerId);
                int homeKFactor = DetermineKFactor(homeMatchCount);
                int awayKFactor = DetermineKFactor(awayMatchCount);
                
                // Calculate ELO changes
                int homeEloChange = (int)Math.Round(homeKFactor * (actualHomeOutcome - expectedHomeOutcome));
                int awayEloChange = (int)Math.Round(awayKFactor * (actualAwayOutcome - expectedAwayOutcome));
                
                // Update in-memory ELO values
                playerCurrentElo[homePlayer.Id] = Math.Max(MinElo, homePlayerElo + homeEloChange);
                playerCurrentElo[awayPlayer.Id] = Math.Max(MinElo, awayPlayerElo + awayEloChange);
            }
            catch (Exception)
            {
                // Log error and continue
                continue;
            }
        }
        
        // Update all players with their final calculated ELO
        foreach (var player in players)
        {
            if (playerCurrentElo.ContainsKey(player.Id))
            {
                var playerUpdate = new PlayerModel
                {
                    Id = player.Id,
                    Name = player.Name,
                    Earnings = player.Earnings,
                    EloNumber = playerCurrentElo[player.Id],
                    Preferences = player.Preferences,
                    UserId = player.UserId
                };
                
                await _playerRepo.UpdatePlayerAsync(playerUpdate);
            }
        }
    }
    
    /// <summary>
    /// Resets all players' ELO ratings to a specified starting value
    /// </summary>
    public async Task ResetAllPlayersEloAsync(int startingElo = DefaultElo)
    {
        var players = await _playerRepo.GetPlayersAsync();
        
        foreach (var player in players)
        {
            await UpdatePlayerEloRatingAsync(player, startingElo);
        }
    }
    
    /// <summary>
    /// Gets all players sorted by their ELO rating
    /// </summary>
    public async Task<Dictionary<string, int>> GetPlayerEloRankingsAsync()
    {
        var players = await _playerRepo.GetPlayersAsync();
        return players
            .OrderByDescending(p => p.EloNumber)
            .ToDictionary(p => p.Name, p => p.EloNumber);
    }
} 