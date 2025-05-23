using McIntoshHotshots.Model;

namespace McIntoshHotshots.Services;

public interface ILiveMatchService
{
    Task<LiveMatch> CreateMatchAsync(int homePlayerId, int awayPlayerId, int? tournamentId = null);
    Task<LiveMatch?> GetMatchAsync(int matchId);
    Task<bool> RecordThrowAsync(int matchId, int playerId, int score, int dartsUsed);
    Task<bool> UndoLastThrowAsync(int matchId, int playerId);
    Task<LiveMatch> FinishLegAsync(int matchId);
    Task<MatchSummaryModel> FinishMatchAsync(int matchId);
    Task<List<LiveMatch>> GetActiveMatchesAsync();
} 