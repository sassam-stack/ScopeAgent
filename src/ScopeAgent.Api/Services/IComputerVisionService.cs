namespace ScopeAgent.Api.Services;

public interface IComputerVisionService
{
    Task<object?> AnalyzeImageAsync(byte[] imageBytes);
    Task<object?> ReadTextAsync(byte[] imageBytes);
}

