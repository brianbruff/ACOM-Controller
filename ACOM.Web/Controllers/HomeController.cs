using ACOM.Web.Models;
using ACOM.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ACOM.Web.Controllers;

public class HomeController : Controller
{
    private readonly IAmplifierService _amplifierService;

    public HomeController(IAmplifierService amplifierService)
    {
        _amplifierService = amplifierService;
    }

    public IActionResult Index()
    {
        var model = new HomeViewModel
        {
            Telemetry = _amplifierService.GetTelemetry(),
            Settings = _amplifierService.GetSettings(),
            ModelConfig = AmplifierModelConfig.Models.GetValueOrDefault(
                _amplifierService.GetSettings().AmplifierModel,
                AmplifierModelConfig.Models["700S"]),
            AvailablePorts = _amplifierService.GetAvailablePorts(),
            AvailableModels = AmplifierModelConfig.Models.Keys.ToArray()
        };
        return View(model);
    }

    [HttpPost]
    public IActionResult UpdateSettings([FromBody] AmplifierSettings settings)
    {
        _amplifierService.UpdateSettings(settings);
        return Ok();
    }

    [HttpPost]
    public IActionResult Standby()
    {
        _amplifierService.SendStandby();
        return Ok();
    }

    [HttpPost]
    public IActionResult Operate()
    {
        _amplifierService.SendOperate();
        return Ok();
    }

    [HttpPost]
    public IActionResult Off()
    {
        _amplifierService.SendOff();
        return Ok();
    }
}

public class HomeViewModel
{
    public AmplifierTelemetry Telemetry { get; set; } = new();
    public AmplifierSettings Settings { get; set; } = new();
    public AmplifierModelConfig ModelConfig { get; set; } = AmplifierModelConfig.Models["700S"];
    public string[] AvailablePorts { get; set; } = Array.Empty<string>();
    public string[] AvailableModels { get; set; } = Array.Empty<string>();
}
