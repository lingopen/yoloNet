using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace yoloNet.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        [ObservableProperty]
        string msg = "就绪";
        [ObservableProperty]
        UserControl? userContent=null;
        [RelayCommand]
        async Task Show(string index)
        {
            try
            {
                switch (index)
                {
                    case "1":
                        UserContent = App.ServiceProvider?.GetRequiredService<RtspView>();
                        break;
                    case "2":
                        UserContent = App.ServiceProvider?.GetRequiredService<MarkView>();
                        break;
                    case "3":
                        UserContent = App.ServiceProvider?.GetRequiredService<TrainView>();
                        break;
                    case "4":
                        UserContent = App.ServiceProvider?.GetRequiredService<PreView>();
                        break;
                }
            }
            catch (Exception ex)
            {
                await MessageBox.Show(ex.Message, "错误");
            }
        }
    }
}
