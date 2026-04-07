using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using yoloNetv2.Controls;
using yoloNetv2.Models;

namespace yoloNetv2.ViewModels
{
    public partial class PreImageViewModel : ViewModelBase
    {
        private string[]? _imageFiles;
        [ObservableProperty]
        int _currentIndex;
        [ObservableProperty]
        string? msg = "就绪";

        [ObservableProperty]
        int imageWidth;
        [ObservableProperty]
        int imageHeight;

        public PreImageViewModel()
        {


        }
        public void LoadAllImage()
        {
            string folder = Path.Combine("dataset", "images", "train");
            // 获取文件并按帧号排序
            _imageFiles = Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*.jpg")
                          .OrderBy(f =>
                          {
                              var name = Path.GetFileNameWithoutExtension(f);
                              var frameStr = name.Split('_')[0];   // 拿到 "10"
                              return int.TryParse(frameStr, out var frame) ? frame : int.MaxValue;
                          })
                          .ToArray()
                : Array.Empty<string>();
        }
        public void LoadCurrentImage()
        {
            if (CurrentIndex == 0)//返回第一张先刷新一下
                LoadAllImage();
            if (_imageFiles == null || _imageFiles.Length == 0) return;

            var bitmap = SKBitmap.Decode(_imageFiles[CurrentIndex]);
            
            ImageWidth = bitmap.Width;
            ImageHeight = bitmap.Height;
            if (OperatingSystem.IsWindows())
                ImageHelper.OnDraw(bitmap);
            else ImageHelper.OnDraw_RKNN(bitmap);

            Msg = _imageFiles[CurrentIndex];
        }

        [RelayCommand]
        private void NextImage(string step)
        {
            if (_imageFiles == null || _imageFiles.Length == 0) return;
            int istep = int.Parse(step);
            if (istep == 0)
                CurrentIndex = _imageFiles.Length - 1;
            else
                CurrentIndex = (CurrentIndex + istep) % _imageFiles.Length;
            LoadCurrentImage();
        }

        [RelayCommand]
        private void PrevImage(string step)
        {
            if (_imageFiles == null || _imageFiles.Length == 0) return;
            int istep = int.Parse(step);
            if (istep == 0)
                CurrentIndex = 0;
            else CurrentIndex = (CurrentIndex - istep + _imageFiles.Length) % _imageFiles.Length;
            LoadCurrentImage();
        }





        [ObservableProperty] private string? _onnxPath;
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
            if (OperatingSystem.IsWindows())
                ImageHelper.Init(OnnxPath);
            else
                ImageHelper.Init_RKNN(OnnxPath);
            LoadCurrentImage();
        }
    }
}
