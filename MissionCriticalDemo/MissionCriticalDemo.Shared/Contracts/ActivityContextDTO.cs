using System.Diagnostics;

namespace MissionCriticalDemo.Shared.Contracts;

public class ActivityContextDTO
{
    public string TraceId { get; set; } = string.Empty;
    public string SpanId { get; set; } = string.Empty;
    public string TraceFlags { get; set; } = string.Empty;
    public string? TraceState { get; set; }

    public static ActivityContextDTO FromActivityContext(ActivityContext context)
    {
        return new ActivityContextDTO
        {
            TraceId = context.TraceId.ToHexString(),
            SpanId = context.SpanId.ToHexString(),
            TraceFlags = ((byte)context.TraceFlags).ToString(),
            TraceState = context.TraceState
        };
    }

    public ActivityContext ToActivityContext()
    {
        var flags = byte.TryParse(TraceFlags, out var b) ? 
            (ActivityTraceFlags)b : ActivityTraceFlags.None;
            
        return new ActivityContext(
            ActivityTraceId.CreateFromString(TraceId),
            ActivitySpanId.CreateFromString(SpanId),
            flags,
            TraceState);
    }
}