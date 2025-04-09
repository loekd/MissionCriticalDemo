using Microsoft.AspNetCore.SignalR;
using MissionCriticalDemo.Shared.Contracts;
using System.Diagnostics;

namespace MissionCriticalDemo.DispatchApi.Hubs;

public interface IDispatchHub
{
    Task SendFlowResponse(string user, Response response, ActivityContextDTO? activityContext = null);
}

public class DispatchHub : Hub<IDispatchHub>
{
    private readonly ActivitySource _activitySource;
    private readonly ILogger<DispatchHub> _logger;

    public DispatchHub(ActivitySource activitySource, ILogger<DispatchHub> logger)
    {
        _activitySource = activitySource;
        _logger = logger;
    }

    public async Task SendFlowResponse(string user, Response response, ActivityContextDTO? activityContext = null)
    {
        using var activity = _activitySource.StartActivity(
            "SendFlowResponse", 
            ActivityKind.Producer, 
            activityContext?.ToActivityContext() ?? default);

        activity?.SetTag("responseId", response.ResponseId);
        activity?.SetTag("requestId", response.RequestId);
        activity?.SetTag("amount", response.TotalAmountInGWh);
        activity?.SetTag("success", response.Success);

        _logger.LogInformation("Sending flow response to user {User} with response ID {ResponseId}", 
            user, response.ResponseId);

        // Send the message with serializable context
        await Clients.All.SendFlowResponse(
            user, 
            response, 
            activity != null ? ActivityContextDTO.FromActivityContext(activity.Context) : null);
    }
}

public static class Extensions
{
    private static readonly System.Text.Json.JsonSerializerOptions options = new(System.Text.Json.JsonSerializerDefaults.Web);

    /// <summary>
    /// Response to JSON
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string ToJson<T>(this T input)
    {
        return System.Text.Json.JsonSerializer.Serialize(input, options: options);
    }
}
