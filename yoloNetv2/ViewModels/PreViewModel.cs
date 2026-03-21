using System;
using System.Collections.ObjectModel;
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


    public void OnInit()
    {
        var list = VideoHelper.GetDevices();
        Devices?.Clear();
        foreach (var item in list)
        {
            Devices!.Add($"{item.Description}");
        }
    }



    [RelayCommand]
    async Task SelectOnnxFile()
    {
        if (App.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow == null)
        {
            await ShowTip((false, "无法获取主窗口"));
            return;
        }

        var file_options = new FilePickerOpenOptions
        {
            Title = "选择 ONNX 文件",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("ONNX 文件") { Patterns = new[] { "*.onnx" } } }
        };

        var result = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(file_options);
        if (result != null && result.Count > 0)
        {
            OnnxPath = result[0].Path.LocalPath;
        }
    }




    [RelayCommand]
    public async Task Start(int index)
    {
        if (string.IsNullOrEmpty(OnnxPath))
        {
            await ShowTip((false, "请选择Onnx文件"));
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
            VideoHelper.Init(index);
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

