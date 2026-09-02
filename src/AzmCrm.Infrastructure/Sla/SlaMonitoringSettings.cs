namespace AzmCrm.Infrastructure.Sla;

public sealed class SlaMonitoringSettings
{
    public const string SectionName = "SlaMonitoring";

    public int IntervalMinutes { get; set; } = 5;
}
