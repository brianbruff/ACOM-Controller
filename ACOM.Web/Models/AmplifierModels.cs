namespace ACOM.Web.Models;

/// <summary>
/// Configuration for different ACOM amplifier models
/// </summary>
public class AmplifierModelConfig
{
    public string Name { get; set; } = string.Empty;
    public int NominalForwardPower { get; set; }
    public int MaxForwardPower { get; set; }
    public int NominalReversePower { get; set; }
    public int MaxReversePower { get; set; }
    public int TemperatureOffset { get; set; }
    public bool ShowTemperature { get; set; }
    public int WarningTemperature { get; set; }

    public static Dictionary<string, AmplifierModelConfig> Models { get; } = new()
    {
        ["500S"] = new AmplifierModelConfig
        {
            Name = "500S",
            NominalForwardPower = 500,
            MaxForwardPower = 600,
            NominalReversePower = 100,
            MaxReversePower = 200,
            TemperatureOffset = 282,
            ShowTemperature = false,
            WarningTemperature = 65
        },
        ["600S"] = new AmplifierModelConfig
        {
            Name = "600S",
            NominalForwardPower = 600,
            MaxForwardPower = 700,
            NominalReversePower = 100,
            MaxReversePower = 200,
            TemperatureOffset = 273,
            ShowTemperature = true,
            WarningTemperature = 65
        },
        ["700S"] = new AmplifierModelConfig
        {
            Name = "700S",
            NominalForwardPower = 700,
            MaxForwardPower = 800,
            NominalReversePower = 100,
            MaxReversePower = 200,
            TemperatureOffset = 282,
            ShowTemperature = true,
            WarningTemperature = 65
        },
        ["1200S"] = new AmplifierModelConfig
        {
            Name = "1200S",
            NominalForwardPower = 1200,
            MaxForwardPower = 1400,
            NominalReversePower = 200,
            MaxReversePower = 400,
            TemperatureOffset = 281,
            ShowTemperature = true,
            WarningTemperature = 65
        },
        ["2020S"] = new AmplifierModelConfig
        {
            Name = "2020S",
            NominalForwardPower = 1800,
            MaxForwardPower = 2000,
            NominalReversePower = 200,
            MaxReversePower = 400,
            TemperatureOffset = 282,
            ShowTemperature = false,
            WarningTemperature = 65
        }
    };
}

/// <summary>
/// Amplifier status codes
/// </summary>
public enum AmplifierStatus
{
    Unknown = 0,
    Reset = 1,
    Init = 2,
    Debug = 3,
    Service = 4,
    Standby = 5,
    Receive = 6,
    Transmit = 7,
    System = 9,
    Off = 10
}

/// <summary>
/// Current telemetry state of the amplifier
/// </summary>
public class AmplifierTelemetry
{
    public bool IsConnected { get; set; }
    public AmplifierStatus Status { get; set; } = AmplifierStatus.Unknown;
    public string StatusText => Status switch
    {
        AmplifierStatus.Reset => "RESET",
        AmplifierStatus.Init => "INIT",
        AmplifierStatus.Debug => "DEBUG",
        AmplifierStatus.Service => "SERVICE",
        AmplifierStatus.Standby => "STANDBY",
        AmplifierStatus.Receive => "RECEIVE",
        AmplifierStatus.Transmit => "TRANSMIT",
        AmplifierStatus.System => "SYSTEM",
        AmplifierStatus.Off => "OFF",
        _ => "--"
    };
    public string StatusColor => Status switch
    {
        AmplifierStatus.Standby => "blue",
        AmplifierStatus.Receive => "green",
        AmplifierStatus.Transmit => "red",
        AmplifierStatus.Off => "gray",
        _ => "black"
    };

    public int Temperature { get; set; }
    public bool TemperatureWarning { get; set; }
    public int FanLevel { get; set; }
    public string FanText => FanLevel switch
    {
        0 => "",
        1 => "Fan",
        2 => "Fan2",
        3 => "Fan3",
        4 => "FAN4",
        _ => ""
    };

    public double OutputPower { get; set; }
    public double DrivePower { get; set; }
    public double ReflectedPower { get; set; }
    public double DcPower { get; set; }
    public double Swr { get; set; }

    public string BandName { get; set; } = "--";
    public string ErrorMessage { get; set; } = string.Empty;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    // Calculated values
    public double Efficiency { get; set; }
    public double Gain { get; set; }
    public bool ShowEfficiency { get; set; }
    public bool ShowGain { get; set; }

    public DateTime LastUpdate { get; set; } = DateTime.MinValue;
}

/// <summary>
/// Application settings
/// </summary>
public class AmplifierSettings
{
    public string ComPort { get; set; } = "COM1";
    public string AmplifierModel { get; set; } = "700S";
    public bool ShowEfficiency { get; set; }
    public bool ShowGain { get; set; }
    public bool ShowSwr { get; set; } = true;
}
