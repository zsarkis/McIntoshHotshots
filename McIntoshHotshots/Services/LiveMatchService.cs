using McIntoshHotshots.Model;
using McIntoshHotshots.Repo;
using System.Collections.Concurrent;

namespace McIntoshHotshots.Services;

public class LiveMatchService : ILiveMatchService
{
    private static readonly ConcurrentDictionary<int, LiveMatch> _activeMatches = new();
    private static int _nextMatchId = 1;
    
    private readonly IPlayerRepo _playerRepo;
    private readonly IMatchSummaryRepo _matchSummaryRepo;
    private readonly ILegRepo _legRepo;
    private readonly ILegDetailRepo _legDetailRepo;
    
    public LiveMatchService(IPlayerRepo playerRepo, IMatchSummaryRepo matchSummaryRepo, 
        ILegRepo legRepo, ILegDetailRepo legDetailRepo)
    {
        _playerRepo = playerRepo;
        _matchSummaryRepo = matchSummaryRepo;
        _legRepo = legRepo;
        _legDetailRepo = legDetailRepo;
    }
    
    public async Task<LiveMatch> CreateMatchAsync(int homePlayerId, int awayPlayerId, int? tournamentId = null)
    {
        var homePlayer = await _playerRepo.GetPlayerByIdAsync(homePlayerId);
        var awayPlayer = await _playerRepo.GetPlayerByIdAsync(awayPlayerId);
        
        if (homePlayer == null || awayPlayer == null)
            throw new ArgumentException("Invalid player IDs");
            
        var match = new LiveMatch
        {
            Id = Interlocked.Increment(ref _nextMatchId),
            HomePlayerId = homePlayerId,
            AwayPlayerId = awayPlayerId,
            HomePlayerName = homePlayer.Name,
            AwayPlayerName = awayPlayer.Name,
            TournamentId = tournamentId,
            StartTime = DateTime.UtcNow,
            CurrentPlayerId = homePlayerId // Home player starts
        };
        
        _activeMatches[match.Id] = match;
        return match;
    }
    
    public async Task<LiveMatch?> GetMatchAsync(int matchId)
    {
        _activeMatches.TryGetValue(matchId, out var match);
        return await Task.FromResult(match);
    }
    
    public async Task<bool> RecordThrowAsync(int matchId, int playerId, int score, int dartsUsed)
    {
        if (!_activeMatches.TryGetValue(matchId, out var match))
            return false;
            
        if (!match.IsCurrentPlayerTurn(playerId) || !match.CanThrow || match.IsFinished)
            return false;
            
        var currentScore = match.GetPlayerScore(playerId);
        
        // Use the rule engine to validate the throw
        var ruleResult = DartsRuleEngine.ValidateThrow(currentScore, score, dartsUsed);
        
        if (!ruleResult.IsValid)
        {
            // Invalid throw - don't record it
            return false;
        }
        
        // Create the throw record
        var liveThrow = new LiveThrow
        {
            Id = match.AllThrows.Count + 1,
            PlayerId = playerId,
            LegNumber = match.CurrentLegNumber,
            TurnNumber = match.CurrentTurnNumber,
            Score = score,
            DartsUsed = dartsUsed,
            ScoreRemainingBefore = currentScore,
            ScoreRemainingAfter = ruleResult.NewScore,
            Timestamp = DateTime.UtcNow,
            IsFinishingThrow = ruleResult.IsFinish
        };
        
        match.AllThrows.Add(liveThrow);
        match.CurrentTurnScores.Add(score);
        match.DartsThrown += dartsUsed;
        
        // Update player score
        if (playerId == match.HomePlayerId)
            match.HomeCurrentScore = ruleResult.NewScore;
        else
            match.AwayCurrentScore = ruleResult.NewScore;
        
        // Handle bust
        if (ruleResult.IsBust)
        {
            // Add bust information to the throw
            liveThrow.IsBust = true;
            liveThrow.BustReason = ruleResult.BustReason;
            
            // End turn immediately on bust
            await EndTurnAsync(match);
            return true;
        }
        
        // Check for leg finish
        if (ruleResult.IsFinish)
        {
            liveThrow.FinishingDouble = ruleResult.FinishingDouble;
            await FinishLegAsync(matchId);
        }
        else if (match.DartsThrown >= 3)
        {
            await EndTurnAsync(match);
        }
        
        return true;
    }
    
    public async Task<bool> UndoLastThrowAsync(int matchId, int playerId)
    {
        if (!_activeMatches.TryGetValue(matchId, out var match))
            return false;
            
        var lastThrow = match.AllThrows.LastOrDefault(t => t.PlayerId == playerId && t.LegNumber == match.CurrentLegNumber);
        if (lastThrow == null)
            return false;
            
        // Remove the throw
        match.AllThrows.Remove(lastThrow);
        
        // Restore score
        if (playerId == match.HomePlayerId)
            match.HomeCurrentScore = lastThrow.ScoreRemainingBefore;
        else
            match.AwayCurrentScore = lastThrow.ScoreRemainingBefore;
            
        // Adjust turn state
        match.DartsThrown -= lastThrow.DartsUsed;
        if (match.CurrentTurnScores.Any())
            match.CurrentTurnScores.RemoveAt(match.CurrentTurnScores.Count - 1);
            
        return await Task.FromResult(true);
    }
    
    public async Task<LiveMatch> FinishLegAsync(int matchId)
    {
        if (!_activeMatches.TryGetValue(matchId, out var match))
            throw new ArgumentException("Match not found");
            
        var winnerId = match.HomeCurrentScore == 0 ? match.HomePlayerId : match.AwayPlayerId;
        
        // Update leg wins
        if (winnerId == match.HomePlayerId)
            match.HomeLegsWon++;
        else
            match.AwayLegsWon++;
            
        // Create leg record
        var leg = new LiveLeg
        {
            LegNumber = match.CurrentLegNumber,
            WinnerPlayerId = winnerId,
            StartTime = match.StartTime,
            EndTime = DateTime.UtcNow,
            Throws = match.AllThrows.Where(t => t.LegNumber == match.CurrentLegNumber).ToList()
        };
        
        match.Legs.Add(leg);
        
        // Check if match is finished (first to 3 legs wins)
        if (match.HomeLegsWon >= 3 || match.AwayLegsWon >= 3)
        {
            match.IsFinished = true;
        }
        else
        {
            // Start new leg
            match.CurrentLegNumber++;
            match.HomeCurrentScore = 501;
            match.AwayCurrentScore = 501;
            match.CurrentTurnNumber = 1;
            match.DartsThrown = 0;
            match.CurrentTurnScores.Clear();
            
            // Alternate starting player: home starts odd legs, away starts even legs
            match.CurrentPlayerId = match.CurrentLegNumber % 2 == 1 ? match.HomePlayerId : match.AwayPlayerId;
        }
        
        return await Task.FromResult(match);
    }
    
    public async Task<MatchSummaryModel> FinishMatchAsync(int matchId)
    {
        if (!_activeMatches.TryGetValue(matchId, out var match))
            throw new ArgumentException("Match not found");
            
        if (!match.IsFinished)
            throw new InvalidOperationException("Match is not finished");
            
        // Create match summary
        var matchSummary = new MatchSummaryModel
        {
            HomePlayerId = match.HomePlayerId,
            AwayPlayerId = match.AwayPlayerId,
            HomeSetAverage = match.HomeSetAverage,
            AwaySetAverage = match.AwaySetAverage,
            HomeLegsWon = match.HomeLegsWon,
            AwayLegsWon = match.AwayLegsWon,
            TimeElapsed = (DateTime.UtcNow - match.StartTime).ToString(@"hh\:mm\:ss"),
            TournamentId = match.TournamentId,
            UrlToRecap = "/recap/placeholder"
        };
        
        // Save to database
        var savedMatch = await _matchSummaryRepo.CreateMatchSummaryAsync(matchSummary);
        
        // Save legs and leg details
        foreach (var leg in match.Legs)
        {
            var legModel = new LegModel
            {
                MatchId = savedMatch.Id,
                LegNumber = leg.LegNumber,
                HomePlayerDartsThrown = leg.Throws.Where(t => t.PlayerId == match.HomePlayerId).Sum(t => t.DartsUsed),
                AwayPlayerDartsThrown = leg.Throws.Where(t => t.PlayerId == match.AwayPlayerId).Sum(t => t.DartsUsed),
                LoserScoreRemaining = leg.WinnerPlayerId == match.HomePlayerId ? 
                    leg.Throws.Where(t => t.PlayerId == match.AwayPlayerId).LastOrDefault()?.ScoreRemainingAfter ?? 501 :
                    leg.Throws.Where(t => t.PlayerId == match.HomePlayerId).LastOrDefault()?.ScoreRemainingAfter ?? 501,
                WinnerId = leg.WinnerPlayerId,
                TimeElapsed = leg.EndTime.HasValue ? (leg.EndTime.Value - leg.StartTime).ToString(@"mm\:ss") : "00:00"
            };
            
            var savedLeg = await _legRepo.CreateLegAsync(legModel);
            
            // Save leg details
            foreach (var throwDetail in leg.Throws)
            {
                var legDetail = new LegDetailModel
                {
                    MatchId = savedMatch.Id,
                    LegId = savedLeg.Id,
                    TurnNumber = throwDetail.TurnNumber,
                    PlayerId = throwDetail.PlayerId,
                    ScoreRemainingBeforeThrow = throwDetail.ScoreRemainingBefore,
                    Score = throwDetail.Score,
                    DartsUsed = throwDetail.DartsUsed
                };
                
                await _legDetailRepo.CreateLegDetailAsync(legDetail);
            }
        }
        
        // Remove from active matches
        _activeMatches.TryRemove(matchId, out _);
        
        return savedMatch;
    }
    
    public async Task<List<LiveMatch>> GetActiveMatchesAsync()
    {
        return await Task.FromResult(_activeMatches.Values.ToList());
    }
    
    private async Task EndTurnAsync(LiveMatch match)
    {
        match.CurrentPlayerId = match.CurrentPlayerId == match.HomePlayerId ? match.AwayPlayerId : match.HomePlayerId;
        match.CurrentTurnNumber++;
        match.DartsThrown = 0;
        match.CurrentTurnScores.Clear();
        await Task.CompletedTask;
    }
} 