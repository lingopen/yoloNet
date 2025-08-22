using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using yoloNet.ViewModels;

namespace yoloNet.Extentions
{
    public static class ServiceExtensions
    {
        public static void RegisterSharedServices(this IServiceCollection services)
        {
            // 公共服务
            services.AddSingleton<HomeView>();
            services.AddSingleton<HomeViewModel>();
            services.AddSingleton<RtspView>();
            services.AddSingleton<RtspViewModel>();

            services.AddSingleton<MarkView>();
            services.AddSingleton<MarkViewModel>();

            services.AddSingleton<TrainView>();
            services.AddSingleton<TrainViewModel>();
            
            
            services.AddSingleton<PreView>();
            services.AddSingleton<PreViewModel>();
        }
    }
}
