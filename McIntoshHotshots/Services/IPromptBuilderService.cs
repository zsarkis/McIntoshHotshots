namespace McIntoshHotshots.Services;

public interface IPromptBuilderService
{
    string BuildCoachingSystemPrompt(UserPerformanceData performanceData);
} 