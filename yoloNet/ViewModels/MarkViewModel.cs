using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using yoloNet.Models;

namespace yoloNet.ViewModels
{
    public partial class MarkViewModel : ViewModelBase
    {
        private string[] _imageFiles;
        [ObservableProperty]
        int _currentIndex;
        [ObservableProperty]
        string msg = "就绪";
        [ObservableProperty]
        private Bitmap? currentImage;
        /// <summary>
        /// 请求重绘
        /// </summary>
        public Action? RequestInvalidate { get; set; }
        [ObservableProperty]
        private string? selectedClass;

        [ObservableProperty]
        private Rect? tempRect;  // 临时矩形，用于鼠标拖动实时显示

        public ObservableCollection<string> Classes { get; } = new()
    {
        "forklift",   // 叉车本体
        "loaded",     // 有货
        "unloaded"    // 无货
    };

        public ObservableCollection<Annotation> Annotations { get; } = new();

        public double ImageWidth => CurrentImage?.PixelSize.Width ?? 800;
        public double ImageHeight => CurrentImage?.PixelSize.Height ?? 600;

        public MarkViewModel()
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

            CurrentIndex = 0;
            LoadCurrentImage();

            SelectedClass = Classes[0];
        }

        private void LoadCurrentImage()
        {
            if (_imageFiles.Length == 0) return;
            CurrentImage = new Bitmap(_imageFiles[CurrentIndex]);
            Annotations.Clear();
            TempRect = null;
            OnPropertyChanged(nameof(ImageWidth));
            OnPropertyChanged(nameof(ImageHeight));
            Msg = _imageFiles[CurrentIndex];

            // 调用 View 进行重绘
            RequestInvalidate?.Invoke();
        }

        [RelayCommand]
        private void NextImage(string step)
        {
            if (_imageFiles.Length == 0) return;
            int istep = int.Parse(step);
            if (istep == 0)
                CurrentIndex = _imageFiles.Length-1;
            else
                CurrentIndex = (CurrentIndex + istep) % _imageFiles.Length;
            LoadCurrentImage();
        }

        [RelayCommand]
        private void PrevImage(string step)
        {
            if (_imageFiles.Length == 0) return;
            int istep = int.Parse(step);
            if (istep == 0)
                CurrentIndex = 0;
            else CurrentIndex = (CurrentIndex - istep + _imageFiles.Length) % _imageFiles.Length;
            LoadCurrentImage();
        }

        [RelayCommand]
        private void SaveAnnotation()
        {
            if (_imageFiles.Length == 0 || CurrentImage == null) return;

            string imgPath = _imageFiles[CurrentIndex];
            string labelFolder = Path.Combine("dataset", "labels", "train");
            Directory.CreateDirectory(labelFolder);

            string fileName = Path.GetFileNameWithoutExtension(imgPath);
            string txtPath = Path.Combine(labelFolder, fileName + ".txt");

            using var writer = new StreamWriter(txtPath);
            foreach (var ann in Annotations)
            {
                double imgW = CurrentImage.PixelSize.Width;
                double imgH = CurrentImage.PixelSize.Height;

                double xCenter = (ann.BoundingBox.X + ann.BoundingBox.Width / 2) / imgW;
                double yCenter = (ann.BoundingBox.Y + ann.BoundingBox.Height / 2) / imgH;
                double w = ann.BoundingBox.Width / imgW;
                double h = ann.BoundingBox.Height / imgH;

                writer.WriteLine($"{ann.ClassId} {xCenter:F6} {yCenter:F6} {w:F6} {h:F6}");
            }
            Msg = "保存标注成功!";
        }

        /// <summary>
        /// 添加已完成的标注矩形
        /// </summary>
        public void AddAnnotation(Rect rect)
        {
            if (string.IsNullOrEmpty(SelectedClass)) return;
            int classId = Classes.IndexOf(SelectedClass);
            Annotations.Add(new Annotation { ClassId = classId, BoundingBox = rect });
            Msg = $"添加标注成功,ClassId:{classId},rect:{rect.X},{rect.Y},{rect.Width},{rect.Height}!";
        }

        [RelayCommand]
        private void UndoAnnotation()
        {
            if (Annotations.Count > 0)
            {
                // 删除最后一个标注
                Annotations.RemoveAt(Annotations.Count - 1);

                // 刷新 Canvas
                RequestInvalidate?.Invoke();
            }
        }
    }
}
