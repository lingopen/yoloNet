using System;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using yoloNet.Extentions;
namespace yoloNet
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            var services = new ServiceCollection();
            // 公共服务
            services.RegisterSharedServices();
            // 平台服务 这里忽略

            //创建ioc容器
            App.ServiceProvider = services.BuildServiceProvider();
            BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
