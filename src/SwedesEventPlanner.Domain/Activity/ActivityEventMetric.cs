using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Activity;

public sealed class ActivityEventMetric
{
    public long Id { get; set; }

    public long ActivityEventId { get; set; }

    public required string MetricType { get; set; }

    public required string MetricKey { get; set; }

    public long? MetricValue { get; set; }

    public bool? MetricBool { get; set; }

    public string MetadataJson { get; set; } = JsonDefaults.Object;
}
