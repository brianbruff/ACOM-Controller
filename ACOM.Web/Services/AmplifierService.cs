using System.IO.Ports;
using ACOM.Web.Hubs;
using ACOM.Web.Models;
using Microsoft.AspNetCore.SignalR;

namespace ACOM.Web.Services;

public interface IAmplifierService
{
    AmplifierTelemetry GetTelemetry();
    AmplifierSettings GetSettings();
    void UpdateSettings(AmplifierSettings settings);
    void SendStandby();
    void SendOperate();
    void SendOff();
    string[] GetAvailablePorts();
}

public class AmplifierService : IAmplifierService, IDisposable
{
    private readonly IHubContext<AmplifierHub> _hubContext;
    private readonly ILogger<AmplifierService> _logger;
    private SerialPort? _port;
    private readonly Timer _timer;

    private AmplifierSettings _settings = new();
    private AmplifierModelConfig _modelConfig = AmplifierModelConfig.Models["700S"];
    private readonly AmplifierTelemetry _telemetry = new();

    // Message parsing state
    private readonly byte[] _messageBytes = new byte[72];
    private int _messageIndex = 0;
    private bool _isParsing = false;
    private DateTime _lastDataReceived = DateTime.MinValue;

    // Power filtering buffers (10-sample moving average)
    private const int BufferSize = 10;
    private readonly double[] _paPower = new double[BufferSize];
    private readonly double[] _drivePower = new double[BufferSize];
    private readonly double[] _reflectedPower = new double[BufferSize];
    private readonly double[] _swrValues = new double[BufferSize];
    private readonly double[] _dcPower = new double[BufferSize];
    private int _powerIndex = 0;
    private int _swrIndex = 0;

    // Band names
    private static readonly string[] BandNames =
    {
        "6m", "10m", "12m", "15m", "17m", "20m", "30m", "40/60m",
        "80m", "160m", "--", "--", "--", "--", "--", "--"
    };

    // Command bytes
    private static readonly byte[] EnableTelemetry = { 0x55, 0x92, 0x04, 0x15 };
    private static readonly byte[] DisableTelemetry = { 0x55, 0x91, 0x04, 0x16 };
    private static readonly byte[] OperateCommand = { 0x55, 0x81, 0x08, 0x02, 0x00, 0x06, 0x00, 0x1a };
    private static readonly byte[] StandbyCommand = { 0x55, 0x81, 0x08, 0x02, 0x00, 0x05, 0x00, 0x1b };
    private static readonly byte[] OffCommand = { 0x55, 0x81, 0x08, 0x02, 0x00, 0x0A, 0x00, 0x16 };

    public AmplifierService(IHubContext<AmplifierHub> hubContext, ILogger<AmplifierService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
        _timer = new Timer(TimerCallback, null, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(200));
    }

    public AmplifierTelemetry GetTelemetry() => _telemetry;

    public AmplifierSettings GetSettings() => _settings;

    public void UpdateSettings(AmplifierSettings settings)
    {
        var needsReconnect = _settings.ComPort != settings.ComPort || _settings.AmplifierModel != settings.AmplifierModel;
        _settings = settings;

        if (AmplifierModelConfig.Models.TryGetValue(settings.AmplifierModel, out var config))
        {
            _modelConfig = config;
        }

        if (needsReconnect)
        {
            Connect();
        }
    }

    public void SendStandby()
    {
        try
        {
            _port?.Write(StandbyCommand, 0, StandbyCommand.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send standby command");
        }
    }

    public void SendOperate()
    {
        try
        {
            _port?.Write(OperateCommand, 0, OperateCommand.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send operate command");
        }
    }

    public void SendOff()
    {
        try
        {
            _port?.Write(OffCommand, 0, OffCommand.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send off command");
        }
    }

    public string[] GetAvailablePorts()
    {
        try
        {
            return SerialPort.GetPortNames();
        }
        catch
        {
            return Enumerable.Range(1, 30).Select(i => $"COM{i}").ToArray();
        }
    }

    private void Connect()
    {
        try
        {
            Disconnect();

            _port = new SerialPort(_settings.ComPort)
            {
                BaudRate = 9600,
                DataBits = 8,
                StopBits = StopBits.One,
                Parity = Parity.None,
                Handshake = Handshake.None,
                DtrEnable = false,
                RtsEnable = false
            };

            _port.DataReceived += OnDataReceived;
            _port.Open();
            _port.Write(EnableTelemetry, 0, EnableTelemetry.Length);

            _logger.LogInformation("Connected to {Port}", _settings.ComPort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to {Port}", _settings.ComPort);
            _telemetry.IsConnected = false;
        }
    }

    private void Disconnect()
    {
        if (_port != null)
        {
            try
            {
                if (_port.IsOpen)
                {
                    _port.Write(DisableTelemetry, 0, DisableTelemetry.Length);
                    _port.Close();
                }
            }
            catch { }
            finally
            {
                _port.Dispose();
                _port = null;
            }
        }
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            while (_port != null && _port.BytesToRead > 0)
            {
                int b = _port.ReadByte();
                if (b < 0) break;

                if (!_isParsing)
                {
                    if (b == 0x55)
                    {
                        _messageBytes[0] = (byte)b;
                        _messageIndex = 1;
                        _isParsing = true;
                    }
                }
                else
                {
                    _messageBytes[_messageIndex++] = (byte)b;

                    if (_messageIndex == 2 && b != 0x2f)
                    {
                        // Not a telemetry message, reset
                        _isParsing = false;
                        _messageIndex = 0;
                    }
                    else if (_messageIndex == 72)
                    {
                        // Complete message received
                        ProcessMessage();
                        _isParsing = false;
                        _messageIndex = 0;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving data");
        }
    }

    private void ProcessMessage()
    {
        // Verify checksum
        int sum = 0;
        for (int i = 0; i < 72; i++)
        {
            sum += _messageBytes[i];
        }
        if ((sum & 0xff) != 0) return;

        _lastDataReceived = DateTime.UtcNow;
        _telemetry.IsConnected = true;
        _telemetry.LastUpdate = DateTime.UtcNow;

        // Extract status
        int statusCode = (_messageBytes[3] & 0xf0) >> 4;
        _telemetry.Status = (AmplifierStatus)statusCode;

        // Extract temperature
        int rawTemp = _messageBytes[16] + _messageBytes[17] * 256;
        _telemetry.Temperature = rawTemp - _modelConfig.TemperatureOffset;
        _telemetry.TemperatureWarning = _telemetry.Temperature > _modelConfig.WarningTemperature;

        // Extract fan status
        _telemetry.FanLevel = (_messageBytes[69] & 0xf0) >> 4;

        // Extract power values
        double drivePowerRaw = (_messageBytes[20] + _messageBytes[21] * 256) * 0.1;
        double outputPowerRaw = _messageBytes[22] + _messageBytes[23] * 256;
        double reflectedPowerRaw = _messageBytes[24] + _messageBytes[25] * 256;
        double swrRaw = (_messageBytes[26] + _messageBytes[27] * 256) / 100.0;
        double dcPowerRaw = _messageBytes[8] * 0.1 + _messageBytes[9] * 25.6;

        // Store in circular buffers
        _paPower[_powerIndex] = outputPowerRaw;
        _drivePower[_powerIndex] = drivePowerRaw;
        _reflectedPower[_powerIndex] = reflectedPowerRaw;
        _dcPower[_powerIndex] = dcPowerRaw;
        _powerIndex = (_powerIndex + 1) % BufferSize;

        _swrValues[_swrIndex] = swrRaw;
        _swrIndex = (_swrIndex + 1) % BufferSize;

        // Calculate display values (max for power, average for SWR)
        _telemetry.OutputPower = _paPower.Max();
        _telemetry.DrivePower = _drivePower.Max();
        _telemetry.ReflectedPower = _reflectedPower.Max();
        _telemetry.DcPower = _dcPower.Max();

        var nonZeroSwr = _swrValues.Where(s => s > 0).ToArray();
        _telemetry.Swr = nonZeroSwr.Length > 0 ? nonZeroSwr.Average() : 0;

        // Extract band
        int bandIndex = _messageBytes[69] & 0x0f;
        _telemetry.BandName = BandNames[bandIndex];

        // Calculate efficiency
        if (_telemetry.DcPower > 0 && _telemetry.OutputPower > 50)
        {
            double eff = 100.0 * _telemetry.OutputPower / _telemetry.DcPower;
            _telemetry.Efficiency = eff >= 20 && eff <= 80 ? eff : 0;
            _telemetry.ShowEfficiency = _settings.ShowEfficiency && _telemetry.Efficiency > 0;
        }
        else
        {
            _telemetry.Efficiency = 0;
            _telemetry.ShowEfficiency = false;
        }

        // Calculate gain
        if (_telemetry.DrivePower > 1 && _telemetry.OutputPower > 100)
        {
            _telemetry.Gain = 10.0 * Math.Log10(_telemetry.OutputPower / _telemetry.DrivePower);
            _telemetry.ShowGain = _settings.ShowGain;
        }
        else
        {
            _telemetry.Gain = 0;
            _telemetry.ShowGain = false;
        }

        // Extract error code
        byte errorCode = _messageBytes[66];
        _telemetry.ErrorMessage = GetErrorMessage(errorCode);

        // Broadcast update to all clients
        _ = _hubContext.Clients.All.SendAsync("TelemetryUpdate", _telemetry);
    }

    private static string GetErrorMessage(byte code)
    {
        return code switch
        {
            0x00 or 0x08 => "Hot switching",
            0x03 => "Drive power at wrong time",
            0x04 or 0x05 => "Reflected power warning",
            0x06 or 0x07 => "Drive power too high",
            0x0c => "RF power at wrong time",
            0x0e => "Stop transmission first",
            0x0f => "Remove drive power",
            0x24 or 0x25 or 0x39 or 0x44 or 0x45 or 0x59 => "Excessive PAM current",
            0x70 => "CAT error",
            0xff => string.Empty,
            _ => string.Empty
        };
    }

    private void TimerCallback(object? state)
    {
        // Check if we've lost connection
        if (_lastDataReceived != DateTime.MinValue &&
            DateTime.UtcNow - _lastDataReceived > TimeSpan.FromSeconds(2))
        {
            _telemetry.IsConnected = false;
            _ = _hubContext.Clients.All.SendAsync("TelemetryUpdate", _telemetry);
        }

        // Re-send enable telemetry command periodically
        try
        {
            if (_port?.IsOpen == true)
            {
                _port.Write(EnableTelemetry, 0, EnableTelemetry.Length);
            }
        }
        catch { }
    }

    public void Dispose()
    {
        _timer.Dispose();
        Disconnect();
    }
}
