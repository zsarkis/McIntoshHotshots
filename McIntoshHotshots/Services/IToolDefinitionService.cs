using McIntoshHotshots.Model;

namespace McIntoshHotshots.Services;

public interface IToolDefinitionService
{
    ToolDefinition[] GetAvailableTools();
} 