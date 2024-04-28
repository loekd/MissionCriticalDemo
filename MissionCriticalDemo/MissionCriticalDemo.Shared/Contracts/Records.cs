using MissionCriticalDemo.Shared.Enums;

namespace MissionCriticalDemo.Shared.Contracts;

/// <summary>
/// Request to inject or withdraw gas
/// </summary>
public record Request(Guid RequestId,
    FlowDirection Direction,
    int AmountInGWh,
    DateTimeOffset Timestamp);

/// <summary>
/// Request to inject or withdraw gas for a customer
/// </summary>
public record CustomerRequest(Guid CustomerId, 
    Guid RequestId,
    FlowDirection Direction,
    int AmountInGWh,
    int CurrentFillLevel,
    int MaxFillLevel,
    bool Success,
    DateTimeOffset Timestamp) : Request(RequestId, Direction, AmountInGWh, Timestamp);

/// <summary>
/// Result of requested injection or withdrawal
/// </summary>
public record Response(Guid ResponseId,
    Guid RequestId,
    FlowDirection Direction,
    int AmountInGWh,
    bool Success,
    DateTimeOffset Timestamp,
    int TotalAmountInGWh,
    int CurrentFillLevel
    );
