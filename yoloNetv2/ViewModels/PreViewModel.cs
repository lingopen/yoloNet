using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using yoloNetv2.Controls;

namespace yoloNetv2.ViewModels;

public partial class PreViewModel : ViewModelBase
{
    [ObservableProperty] private string? _onnxPath;
    [ObservableProperty] private bool _isRunning = false;
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
        if (list == null || !list.Any()) return;
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
    async Task SelectOnnxFile()
    {
        if (App.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow == null)
        {
            await ShowTip((false, "无法获取主窗口"));
            return;
        }
        FilePickerOpenOptions file_options;
        if (OperatingSystem.IsWindows())
            file_options = new FilePickerOpenOptions
            {
                Title = "选择 ONNX 文件",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("ONNX 文件") { Patterns = new[] { "*.onnx" } } }
            };
        else
            file_options = new FilePickerOpenOptions
            {
                Title = "选择 RKNN 文件",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("RKNN 文件") { Patterns = new[] { "*.rknn" } } }
            };
     

        var result = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(file_options);
        if (result != null && result.Count > 0)
        {
            OnnxPath = result[0].Path.LocalPath;
        }
    }




    [RelayCommand]
    public async Task Start()
    {
        if (string.IsNullOrEmpty(OnnxPath))
        {
            if(OperatingSystem.IsWindows())
                await ShowTip((false, "请选择ONNX文件"));
            else
                await ShowTip((false, "请选择RKNN文件"));
            return;
        }
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
            await VideoHelper.Init(DevicesIndex, CharacterIndex);
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
            await VideoHelper.Start(0, OnnxPath);
            IsRunning = VideoHelper.IsRunning;


        }
    }


}

