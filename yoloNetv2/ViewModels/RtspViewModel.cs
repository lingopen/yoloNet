using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using yoloNetv2.Controls;
using yoloNetv2.Extentions;

namespace yoloNetv2.ViewModels
{
    public partial class RtspViewModel : ViewModelBase
    {

        /// <summary>
        /// 抽帧 每N帧保存一次
        /// </summary>
        [ObservableProperty]
        double _interval = 0;
        [ObservableProperty]
        int _frameCounter = 0;

        [ObservableProperty]
        int _width = 0;

        [ObservableProperty]
        int _height = 0;
        [ObservableProperty]
        bool isRunning = false;
        [ObservableProperty]
        ObservableCollection<string>? _devices = new ObservableCollection<string>();

        [ObservableProperty]
        int _devicesIndex = 0;

        [ObservableProperty]
        ObservableCollection<string>? _characters = new ObservableCollection<string>();

        [ObservableProperty]
        int _characterIndex = 0;

        partial void OnDevicesIndexChanged(int value)
        {
            if (value < 0) return;
            var list = VideoHelper.GetDevices();
            var characteristics = list[value].Characteristics;
            Characters?.Clear();
            foreach (var item in characteristics)
            {
                Characters!.Add(item.ToString());
            }
        }

        public async Task OnInit()
        {
            await VideoHelper.UnInit();
            var list = VideoHelper.GetDevices();
            Devices?.Clear();
            foreach (var item in list)
            {
                Devices!.Add($"{item.Description}");
            }
            DevicesIndex = 0;
            OnDevicesIndexChanged(0);
        }

        [RelayCommand]
        public async Task Start()
        {
            if (IsRunning)
            {
                IsRunning = false;

                if (VideoHelper.IsInit)
                {
                    await VideoHelper.UnInit();
                }
                await Task.Delay(200);//休眠200ms 
                return;
            }



            try
            {
               await  VideoHelper.Init(DevicesIndex, CharacterIndex);
            }
            catch (Exception)
            {

                return;
            }
            if (!VideoHelper.IsInit)
            {
                IsRunning = false;
                return;
            }
            else
            {
                Width = VideoHelper.Characteristics?.Width ?? 0;
                Height = VideoHelper.Characteristics?.Height ?? 0;

                await VideoHelper.Start(Interval);
                IsRunning = VideoHelper.IsRunning;
            }
        }


        [RelayCommand]
        async Task Clear()
        {
            var res = MatExtensions.ClearFrame();
            await ShowTip(res);
        }
    }
}
