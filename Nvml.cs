using System;
using System.Runtime.InteropServices;

namespace GpuOverlay;

public static class Nvml
{
    private const string NvmlDll = "nvml.dll";
    public const int NVML_SUCCESS = 0;

    [DllImport(NvmlDll, EntryPoint = "nvmlInit_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlInit_v2();

    [DllImport(NvmlDll, EntryPoint = "nvmlShutdown", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlShutdown();

    [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetCount_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetCount_v2(out uint deviceCount);

    [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetHandleByIndex_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

    [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetTemperature", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetTemperature(IntPtr device, int sensorType, out uint temp);

    [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetUtilizationRates", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetUtilizationRates(IntPtr device, out nvmlUtilization utilization);

    [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetMemoryInfo", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetMemoryInfo(IntPtr device, out nvmlMemory memory);

    [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetName", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetName(IntPtr device, byte[] name, uint length);

    [DllImport(NvmlDll, EntryPoint = "nvmlSystemGetNVMLVersion", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlSystemGetNVMLVersion(byte[] version, uint length);

    [DllImport(NvmlDll, EntryPoint = "nvmlSystemGetDriverVersion", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlSystemGetDriverVersion(byte[] version, uint length);

    [StructLayout(LayoutKind.Sequential)]
    public struct nvmlUtilization
    {
        public uint gpu;
        public uint memory;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct nvmlMemory
    {
        public ulong total;
        public ulong free;
        public ulong used;
    }

    private static bool _initialized;
    private static IntPtr _device = IntPtr.Zero;
    private static string? _deviceName;

    public static bool IsAvailable { get; private set; }
    public static string LastError { get; private set; } = string.Empty;
    public static string? DeviceName => _deviceName;
    public static string? NvmlVersion { get; private set; }
    public static string? DriverVersion { get; private set; }

    public static bool Initialize()
    {
        if (_initialized) return true;

        try
        {
            int ret = nvmlInit_v2();
            if (ret != NVML_SUCCESS)
            {
                LastError = $"nvmlInit failed: {ret}";
                IsAvailable = false;
                return false;
            }

            ret = nvmlDeviceGetCount_v2(out uint count);
            if (ret != NVML_SUCCESS || count == 0)
            {
                LastError = "No NVIDIA GPU found";
                nvmlShutdown();
                IsAvailable = false;
                return false;
            }

            ret = nvmlDeviceGetHandleByIndex_v2(0, out _device);
            if (ret != NVML_SUCCESS)
            {
                LastError = $"GetHandle failed: {ret}";
                nvmlShutdown();
                IsAvailable = false;
                return false;
            }

            var nameBytes = new byte[128];
            if (nvmlDeviceGetName(_device, nameBytes, (uint)nameBytes.Length) == NVML_SUCCESS)
                _deviceName = System.Text.Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');

            var verBytes = new byte[128];
            if (nvmlSystemGetNVMLVersion(verBytes, (uint)verBytes.Length) == NVML_SUCCESS)
                NvmlVersion = System.Text.Encoding.UTF8.GetString(verBytes).TrimEnd('\0');

            var drvBytes = new byte[128];
            if (nvmlSystemGetDriverVersion(drvBytes, (uint)drvBytes.Length) == NVML_SUCCESS)
                DriverVersion = System.Text.Encoding.UTF8.GetString(drvBytes).TrimEnd('\0');

            _initialized = true;
            IsAvailable = true;
            return true;
        }
        catch (DllNotFoundException)
        {
            LastError = "nvml.dll not found";
            IsAvailable = false;
            return false;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            IsAvailable = false;
            return false;
        }
    }

    public static void Shutdown()
    {
        if (_initialized)
        {
            try { nvmlShutdown(); } catch { }
            _initialized = false;
            IsAvailable = false;
        }
    }

    public static GpuStatus GetStatus()
    {
        var status = new GpuStatus();

        if (!_initialized || _device == IntPtr.Zero)
        {
            status.Available = false;
            status.Error = LastError;
            return status;
        }

        try
        {
            int ret = nvmlDeviceGetTemperature(_device, 0, out uint temp);
            if (ret == NVML_SUCCESS)
                status.Temperature = (int)temp;
            else
                status.Error = $"GetTemperature: {ret}";

            ret = nvmlDeviceGetUtilizationRates(_device, out nvmlUtilization util);
            if (ret == NVML_SUCCESS)
            {
                status.GpuUsage = util.gpu;
                status.MemoryUsage = util.memory;
            }

            ret = nvmlDeviceGetMemoryInfo(_device, out nvmlMemory mem);
            if (ret == NVML_SUCCESS)
            {
                status.MemoryTotal = mem.total;
                status.MemoryUsed = mem.used;
            }

            status.Available = true;
        }
        catch (Exception ex)
        {
            status.Error = ex.Message;
        }

        return status;
    }
}

public class GpuStatus
{
    public bool Available { get; set; }
    public int Temperature { get; set; }
    public uint GpuUsage { get; set; }
    public uint MemoryUsage { get; set; }
    public ulong MemoryTotal { get; set; }
    public ulong MemoryUsed { get; set; }
    public string? Error { get; set; }
}
