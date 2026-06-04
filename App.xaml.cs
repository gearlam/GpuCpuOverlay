using System;
using System.Windows;
using System.Windows.Threading;

namespace GpuOverlay;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += App_DispatcherUnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"未处理异常: {e.Exception.Message}\n\n{e.Exception.StackTrace}",
            "GPU Overlay 错误",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Nvml.Shutdown();
        base.OnExit(e);
    }
}
