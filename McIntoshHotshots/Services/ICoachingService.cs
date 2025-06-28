namespace McIntoshHotshots.Services;

public interface ICoachingService
{
    Task<string> GetCoachingResponseAsync(string userMessage, string userId, CancellationToken cancellationToken = default);
} 