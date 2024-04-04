using MissionCriticalDemo.Shared.Enums;

namespace MissionCriticalDemo.Messages;

/// <summary>
/// Request to inject or withdraw gas
/// </summary>
public record Request(Guid RequestId,
    Guid CustomerId,
    FlowDirection Direction,
    int AmountInGWh,
    DateTimeOffset Timestamp);

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

