using MissionCriticalDemo.Shared.Enums;

namespace MissionCriticalDemo.Messages;

/// <summary>
/// Request to inject or withdraw gas
/// </summary>
// public record Request(Guid RequestId,
//     Guid CustomerId,
//     FlowDirection Direction,
//     int AmountInGWh,
//     DateTimeOffset Timestamp);
public class Request
{
    public Guid RequestId { get; set; }
    public Guid CustomerId { get; set; }
    public FlowDirection Direction { get; set; }
    public int AmountInGWh { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    public Request()
    {
    }

    public Request(Guid requestId, Guid customerId, FlowDirection direction, int amountInGWh, DateTimeOffset timestamp)
    {
        RequestId = requestId;
        CustomerId = customerId;
        Direction = direction;
        AmountInGWh = amountInGWh;
        Timestamp = timestamp;
    }
}

/// <summary>
/// Result of requested injection or withdrawal
/// </summary>
public record Response(Guid ResponseId,
    Guid RequestId,
    Guid CustomerId,
    FlowDirection Direction,
    int AmountInGWh,
    bool Success,
    DateTimeOffset Timestamp,
    int CurrentFillLevel,
    int MaxFillLevel
    );

