using System;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using yoloNet.Views;
namespace yoloNet
{
    public partial class App : Application
    {
        /// <summary>
        /// IOC容器
        /// </summary>
        public static IServiceProvider? ServiceProvider { get; set; }
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            // 将服务提供者保存到全局访问对象
            var view = App.ServiceProvider?.GetRequiredService<HomeView>();
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                desktop.MainWindow = new MainWindow { Content = view };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform) //安卓平台
            {
                singleViewPlatform.MainView = view;
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}