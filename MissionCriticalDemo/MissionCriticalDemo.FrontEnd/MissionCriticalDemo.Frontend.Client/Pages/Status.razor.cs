using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.SignalR.Client;
using MissionCriticalDemo.Shared.Contracts;
using MissionCriticalDemo.Shared.Enums;
using MissionCriticalDemo.Shared.Services;
using MudBlazor;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MissionCriticalDemo.Frontend.Client.Pages;

[Authorize]
public partial class StatusModel : ComponentBase
{
    private HubConnection? _hubConnection;

    /// <summary>
    /// Gas in store for current customer
    /// </summary>
    public int? CustomerGasInStore { get; set; }

    /// <summary>
    /// Gas in store for all customers
    /// </summary>
    public int? OverallGasInStore { get; set; }

    public FlowDirection Direction { get; set; }

    public int Amount { get; set; }

    [Inject]
    public IDispatchService? DispatchService { get; set; }

    [Inject]
    public NavigationManager? NavigationManager { get; set; }

    [Inject]
    public ILogger<Status>? Logger { get; set; }

    [Inject]
    public ISnackbar? Snackbar { get; set; }

    [Inject]
    public IHubConnectionBuilder? HubConnectionBuilder { get; set; }

    [Inject]
    public IConfiguration? Configuration { get; set; }

    [Inject]
    public ActivitySource? ActivitySource { get; set; }

    public bool ButtonsDisabled { get; set; } = false;

    /// <summary>
    /// Runtime constructor
    /// </summary>
    public StatusModel()
    {
    }

    /// <summary>
    /// Setup a Hub connection to receive updates.
    /// </summary>
    /// <returns></returns>
    protected override async Task OnInitializedAsync()
    {
        if (!OperatingSystem.IsBrowser())
            return;

        await FetchCurrentGasInStore();
        await HandleServerCallbacks();
    }

    private async Task FetchCurrentGasInStore()
    {
        try
        {
            ButtonsDisabled = true;
            CustomerGasInStore = await DispatchService!.GetCustomerGasInStore();
            OverallGasInStore = await DispatchService!.GetOverallGasInStore();
        }
        catch (AccessTokenNotAvailableException ex)
        {
            ex.Redirect();
        }
        catch (Exception ex)
        {
            Logger!.LogError("Failed to submit request. Error: {ErrorMessage}", ex.Message);
        }
        finally
        {
            ButtonsDisabled = false;
        }
    }

    private async Task HandleServerCallbacks()
    {
        if (_hubConnection != null)
            return;

        Uri hubUrl;
        string dispatchApiUrl = Configuration!["DispatchApi:Endpoint"] ?? NavigationManager!.BaseUri;
        Console.WriteLine($"Using '{dispatchApiUrl}/dispatchhub' for signalr");
        hubUrl = new Uri(new Uri(dispatchApiUrl), "dispatchhub");

        _hubConnection = HubConnectionBuilder!
            .WithUrl(hubUrl)
            .Build();

        _hubConnection.On<string, Response, ActivityContextDTO?>("SendFlowResponse", async (user, response, activityContext) =>
        {
            await InvokeAsync(() =>
            {
                Console.WriteLine($"Received flow response for user {user}. Context traceid: {activityContext?.TraceId ?? "empty"}");

                // Create an activity with the received trace context
                Activity? activity = null;
                try
                {                    
                
                if (activityContext != null)
                {
                    activity = ActivitySource!.StartActivity(
                        "ReceiveFlowResponse", 
                        ActivityKind.Consumer,
                        activityContext.ToActivityContext());
                }

                activity?.SetTag("responseId", response.ResponseId);                
                activity?.SetTag("requestId", response.RequestId);
                activity?.SetTag("success", response.Success);

                if (response.Success)
                {
                    //update total gas in store
                    CustomerGasInStore = response.TotalAmountInGWh;
                    OverallGasInStore = response.CurrentFillLevel;
                    Logger!.LogWarning("Received a flow response message id {ResponseId}.", response.ResponseId);
                    Snackbar!.Add($"Request was processed. Success: {response.Success}", Severity.Info);
                }
                else
                {
                    Snackbar!.Add($"Request processing failed", Severity.Error);
                }
}
                finally
                {
                    activity?.Dispose();   
                }
                StateHasChanged();
            });
        });

        Console.WriteLine($"Starting SignalR connection to {hubUrl}");
        await _hubConnection.StartAsync();
    }

    protected async Task SubmitRequest()
    {
        var request = new Request(Guid.NewGuid(), Direction, Amount, DateTimeOffset.UtcNow);
        try
        {
            ButtonsDisabled = true;
            await DispatchService!.SubmitRequest(request);
        }
        catch (AccessTokenNotAvailableException ex)
        {
            ex.Redirect();
        }
        catch (Exception ex)
        {
            Snackbar!.Add($"Request processing failed: {ex.Message}", Severity.Warning);
            Logger!.LogError("Failed to submit request. Error: {ErrorMessage}", ex.Message);
        }
        finally
        {
            ButtonsDisabled = false;
        }

    }

    protected async Task FetchCustomerGasInStore()
    {
        //Test();
        CustomerGasInStore = null;
        try
        {
            CustomerGasInStore = await DispatchService!.GetCustomerGasInStore();
        }
        catch (AccessTokenNotAvailableException ex)
        {
            ex.Redirect();
        }
        catch (Exception ex)
        {
            Snackbar!.Add($"Fetch customer gas in store failed: {ex.Message}", Severity.Warning);
            Logger!.LogError("Failed to fetch customer status. Error: {ErrorMessage}", ex.Message);
        }
    }

    private void Test()
    {
        using var openTelemetry = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("blazor-otel"))
            .AddSource("BlazorUI")
            .AddZipkinExporter(o =>
            {
                o.Endpoint = new Uri("http://localhost:5054/zipkin");
                o.ExportProcessorType = ExportProcessorType.Simple;
            })
            .Build();
        using var source = new ActivitySource("BlazorUI");
        using (var activity = source.StartActivity("Click", ActivityKind.Client))
        {
            activity?.AddEvent(new ActivityEvent("The count button was clicked"));

            
            activity?.SetTag("mouse.x", 123);
            activity?.SetTag("mouse.y", 2323);
        }
    }

    protected async Task FetchOverallGasInStore()
    {
        OverallGasInStore = null;
        try
        {
            OverallGasInStore = await DispatchService!.GetOverallGasInStore();
        }
        catch (AccessTokenNotAvailableException ex)
        {
            ex.Redirect();
        }
        catch (Exception ex)
        {
            Snackbar!.Add($"Fetch overall gas in store failed: {ex.Message}", Severity.Warning);
            Logger!.LogError("Failed to fetch status. Error: {ErrorMessage}", ex.Message);
        }
    }

    private class SignalRMessageWrapper
    {
        public string Payload { get; set; } = "";
        public Dictionary<string, string>? TraceContext { get; set; }
    }
}
