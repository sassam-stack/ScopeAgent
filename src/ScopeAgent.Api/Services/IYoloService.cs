namespace ScopeAgent.Api.Services;

public interface IYoloService
{
    Task<object?> AnalyzeImageAsync(byte[] imageBytes);
}

