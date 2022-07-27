using Microsoft.AspNetCore.SignalR;
using MissionCriticalDemo.Shared.Contracts;

namespace MissionCriticalDemo.DispatchApi.Hubs;

public interface IDispatchHub
{
    Task SendFlowResponse(string user, Response response);
}

public class DispatchHub : Hub, IDispatchHub
{
    public async Task SendFlowResponse(string user, Response response)
    {
        await Clients.User(user).SendAsync("ReceiveMessage", response.ToJson());
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
