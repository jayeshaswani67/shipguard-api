namespace ShipGuard.Api.Configuration;

public sealed class TrivyOptions
{
    public string ExecutablePath { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 300;
}