namespace ScopeAgent.Api.Services;

public class OuterportConfig
{
    public string ServiceUrl { get; set; } = "http://localhost:8002";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 300;
}

