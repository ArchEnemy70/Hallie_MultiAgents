using Hallie.Views;
using HallieCore.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace Hallie;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);

            Services = services.BuildServiceProvider();

            // Force instantiation
            Services.GetRequiredService<ApprovalWindowManager>();

            var mainWindow = Services.GetRequiredService<AvatarFace>();
            Current.MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Startup crash");
            Shutdown(-1);
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IApprovalService, ApprovalService>();
        services.AddSingleton<ApprovalWindowManager>();
        services.AddSingleton<SafeToolRunner>();
        services.AddSingleton<AvatarFace>();
        services.AddSingleton<IApprovalSummaryBuilder, ApprovalSummaryBuilder>();
    }
}