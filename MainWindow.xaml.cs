using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace GpuOverlay;

public partial class MainWindow : Window
{
    private AppConfig _config = null!;
    private DispatcherTimer _timer = null!;
    private HardwareMonitor _hwMonitor = null!;
    private Forms.NotifyIcon? _trayIcon;
    private Forms.ToolStripMenuItem? _trayMenuUnlock;
    private Forms.ToolStripMenuItem? _trayMenuLock;
    private bool _isDragging;
    private bool _saveScheduled;

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    private static IntPtr GetExStyle(IntPtr hWnd) => IntPtr.Size == 8
        ? GetWindowLongPtr64(hWnd, GWL_EXSTYLE)
        : new IntPtr(GetWindowLong32(hWnd, GWL_EXSTYLE));

    private static IntPtr SetExStyle(IntPtr hWnd, IntPtr newStyle) => IntPtr.Size == 8
        ? SetWindowLongPtr64(hWnd, GWL_EXSTYLE, newStyle)
        : new IntPtr(SetWindowLong32(hWnd, GWL_EXSTYLE, newStyle.ToInt32()));

    public MainWindow()
    {
        InitializeComponent();

        _config = AppConfig.Load();

        Left = _config.Left;
        Top = _config.Top;
        Opacity = _config.Opacity;

        LocationChanged += (_, _) => ScheduleSaveConfig();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        var exStyle = GetExStyle(hwnd).ToInt64();
        exStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetExStyle(hwnd, new IntPtr(exStyle));

        Nvml.Initialize();
        if (!Nvml.IsAvailable)
        {
            TextGpuTemp.Text = "NA";
            TextGpuUsage.Text = Nvml.LastError;
        }
        else
        {
            TextGpuUsage.ToolTip = Nvml.DeviceName;
        }

        _hwMonitor = new HardwareMonitor();
        _hwMonitor.Initialize();
        if (!_hwMonitor.IsAvailable)
        {
            TextCpuTemp.Text = "NA";
            TextCpuInfo.Text = _hwMonitor.LastError;
        }

        CreateTrayIcon();

        ApplyLockState(_config.IsLocked);
        UpdateTrayMenuState();

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
        _timer.Start();

        UpdateStats();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        UpdateStats();
    }

    private void UpdateStats()
    {
        UpdateGpu();
        UpdateCpu();
    }

    private void UpdateGpu()
    {
        if (!Nvml.IsAvailable)
        {
            TextGpuTemp.Text = "NA";
            return;
        }

        var status = Nvml.GetStatus();
        if (!status.Available)
        {
            TextGpuTemp.Text = "ERR";
            return;
        }

        TextGpuTemp.Text = status.Temperature.ToString();
        TextGpuUsage.Text = $"{status.GpuUsage}%  {status.MemoryUsed / 1024 / 1024}MB";

        var temp = status.Temperature;
        if (temp >= 80)
            TextGpuTemp.Foreground = new SolidColorBrush(Colors.OrangeRed);
        else if (temp >= 70)
            TextGpuTemp.Foreground = new SolidColorBrush(Colors.Orange);
        else if (temp >= 60)
            TextGpuTemp.Foreground = new SolidColorBrush(Colors.Khaki);
        else
            TextGpuTemp.Foreground = new SolidColorBrush(Colors.White);
    }

    private void UpdateCpu()
    {
        if (_hwMonitor == null || !_hwMonitor.IsAvailable)
        {
            TextCpuTemp.Text = "NA";
            return;
        }

        _hwMonitor.Update();
        var temp = _hwMonitor.CpuTemperature;

        if (temp <= 0)
        {
            TextCpuTemp.Text = "NA";
            TextCpuInfo.Text = "no sensor";
            return;
        }

        TextCpuTemp.Text = temp.ToString();
        TextCpuInfo.Text = "";

        if (temp >= 80)
            TextCpuTemp.Foreground = new SolidColorBrush(Colors.OrangeRed);
        else if (temp >= 70)
            TextCpuTemp.Foreground = new SolidColorBrush(Colors.Orange);
        else if (temp >= 60)
            TextCpuTemp.Foreground = new SolidColorBrush(Colors.Khaki);
        else
            TextCpuTemp.Foreground = new SolidColorBrush(Colors.White);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_config.IsLocked) return;
        if (e.ChangedButton != MouseButton.Left) return;

        try
        {
            _isDragging = true;
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            _isDragging = false;
            SaveConfigNow();
        }
    }

    private void MenuLock_Click(object sender, RoutedEventArgs e)
    {
        SetLocked(!_config.IsLocked);
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SetLocked(bool locked)
    {
        _config.IsLocked = locked;
        ApplyLockState(locked);
        UpdateTrayMenuState();
        SaveConfigNow();
    }

    private void ApplyLockState(bool locked)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        var exStyle = GetExStyle(hwnd).ToInt64();
        if (locked)
        {
            exStyle |= WS_EX_TRANSPARENT;
            RootBorder.Opacity = 0.6;
        }
        else
        {
            exStyle &= ~WS_EX_TRANSPARENT;
            RootBorder.Opacity = 1.0;
        }
        SetExStyle(hwnd, new IntPtr(exStyle));

        MenuLock.Header = locked ? "解锁位置" : "锁定位置";
    }

    private void UpdateTrayMenuState()
    {
        if (_config.IsLocked)
        {
            if (_trayMenuLock != null) _trayMenuLock.Visible = false;
            if (_trayMenuUnlock != null) _trayMenuUnlock.Visible = true;
        }
        else
        {
            if (_trayMenuLock != null) _trayMenuLock.Visible = true;
            if (_trayMenuUnlock != null) _trayMenuUnlock.Visible = false;
        }
    }

    private void CreateTrayIcon()
    {
        if (_trayIcon != null) return;

        var menu = new Forms.ContextMenuStrip();
        _trayMenuUnlock = new Forms.ToolStripMenuItem("解锁位置", null, (_, _) =>
            Dispatcher.Invoke(() => SetLocked(false)));
        _trayMenuLock = new Forms.ToolStripMenuItem("锁定位置", null, (_, _) =>
            Dispatcher.Invoke(() => SetLocked(true)));
        var exit = new Forms.ToolStripMenuItem("退出", null, (_, _) =>
            Dispatcher.Invoke(() => Close()));

        menu.Items.Add(_trayMenuUnlock);
        menu.Items.Add(_trayMenuLock);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exit);

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = BuildTrayText(),
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(() =>
        {
            if (WindowState == WindowState.Minimized || !IsVisible)
            {
                Show();
                WindowState = WindowState.Normal;
            }

            SetLocked(false);
            Activate();
        });

        UpdateTrayMenuState();
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "2r.ico");
        if (File.Exists(iconPath))
            return new System.Drawing.Icon(iconPath);

        return System.Drawing.SystemIcons.Application;
    }

    private string BuildTrayText()
    {
        var text = Nvml.IsAvailable && !string.IsNullOrWhiteSpace(Nvml.DeviceName)
            ? $"GPU: {Nvml.DeviceName}"
            : "GPU 温度监控";
        return text.Length > 63 ? text[..63] : text;
    }

    private void ScheduleSaveConfig()
    {
        if (_isDragging) return;
        if (_saveScheduled) return;
        _saveScheduled = true;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            _saveScheduled = false;
            if (IsLoaded && !_isDragging)
            {
                _config.Left = Left;
                _config.Top = Top;
                _config.Save();
            }
        }), DispatcherPriority.Background);
    }

    private void SaveConfigNow()
    {
        if (!IsLoaded) return;
        _config.Left = Left;
        _config.Top = Top;
        _config.Opacity = Opacity;
        _config.Save();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveConfigNow();
        _timer?.Stop();
        Nvml.Shutdown();
        _hwMonitor?.Dispose();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }
}
