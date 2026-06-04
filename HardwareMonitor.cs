using System;
using LibreHardwareMonitor.Hardware;

namespace GpuOverlay;

public sealed class HardwareMonitor : IDisposable
{
    private Computer? _computer;
    private bool _initialized;

    public int CpuTemperature { get; private set; }
    public bool IsAvailable { get; private set; }
    public string LastError { get; private set; } = string.Empty;

    public void Initialize()
    {
        if (_initialized) return;

        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = false,
                IsMotherboardEnabled = false,
                IsStorageEnabled = false,
                IsMemoryEnabled = false,
                IsNetworkEnabled = false
            };
            _computer.Open();
            _initialized = true;
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            IsAvailable = false;
        }
    }

    public void Update()
    {
        if (!_initialized || _computer == null) return;

        try
        {
            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                foreach (var sub in hw.SubHardware)
                    sub.Update();

                foreach (var sensor in hw.Sensors)
                {
                    if (sensor.SensorType == SensorType.Temperature &&
                        sensor.Value.HasValue &&
                        sensor.Value.Value > 0)
                    {
                        CpuTemperature = (int)sensor.Value.Value;
                        return;
                    }
                }
            }
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        _computer?.Close();
        _computer = null;
        _initialized = false;
    }
}
