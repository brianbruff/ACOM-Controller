using ACOM.Web.Models;
using ACOM.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace ACOM.Web.Hubs;

public class AmplifierHub : Hub
{
    private readonly IAmplifierService _amplifierService;

    public AmplifierHub(IAmplifierService amplifierService)
    {
        _amplifierService = amplifierService;
    }

    public override async Task OnConnectedAsync()
    {
        // Send current telemetry state to newly connected client
        var telemetry = _amplifierService.GetTelemetry();
        await Clients.Caller.SendAsync("TelemetryUpdate", telemetry);
        await base.OnConnectedAsync();
    }

    public async Task SendStandby()
    {
        _amplifierService.SendStandby();
        await Task.CompletedTask;
    }

    public async Task SendOperate()
    {
        _amplifierService.SendOperate();
        await Task.CompletedTask;
    }

    public async Task SendOff()
    {
        _amplifierService.SendOff();
        await Task.CompletedTask;
    }
}
