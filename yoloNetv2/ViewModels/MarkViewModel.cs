using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using yoloNetv2.Models;

namespace yoloNetv2.ViewModels
{
    public partial class MarkViewModel : ViewModelBase
    {
        private string[]? _imageFiles;
        [ObservableProperty]
        int _currentIndex;
        [ObservableProperty]
        string? msg = "就绪";
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

        private const double MinZoomScale = 0.5;
        private const double MaxZoomScale = 8.0;
        private const double ZoomStep = 0.25;
        private double _zoomScale = 2.0;

        public double ZoomScale
        {
            get => _zoomScale;
            set
            {
                var clamped = Math.Round(Math.Clamp(value, MinZoomScale, MaxZoomScale), 2);
                if (SetProperty(ref _zoomScale, clamped))
                {
                    OnPropertyChanged(nameof(DisplayImageWidth));
                    OnPropertyChanged(nameof(DisplayImageHeight));
                    OnPropertyChanged(nameof(ZoomPercent));
                    RequestInvalidate?.Invoke();
                }
            }
        }

        public ObservableCollection<string> Classes { get; } = new()
        {
            "red",
            "green"
        };

        public ObservableCollection<Annotation> Annotations { get; } = new();

        public double ImageWidth => CurrentImage?.PixelSize.Width ?? 800;
        public double ImageHeight => CurrentImage?.PixelSize.Height ?? 600;
        public double DisplayImageWidth => ImageWidth * ZoomScale;
        public double DisplayImageHeight => ImageHeight * ZoomScale;
        public string ZoomPercent => $"{ZoomScale * 100:0}%";

        public MarkViewModel()
        {
            LoadAllImage();
            CurrentIndex = 0;
            LoadCurrentImage();

            SelectedClass = Classes[0];
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
            if (CurrentIndex <= 0)//返回第一张先刷新一下
                LoadAllImage();
            if (_imageFiles == null || _imageFiles.Length == 0) return;
            CurrentImage = new Bitmap(_imageFiles[CurrentIndex]);
            Annotations.Clear();
            TempRect = null;
            OnPropertyChanged(nameof(ImageWidth));
            OnPropertyChanged(nameof(ImageHeight));
            OnPropertyChanged(nameof(DisplayImageWidth));
            OnPropertyChanged(nameof(DisplayImageHeight));
            Msg = _imageFiles[CurrentIndex];


            LoadAnnotation();


            // 调用 View 进行重绘
            RequestInvalidate?.Invoke();
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

        private void LoadAnnotation()
        {
            if (_imageFiles == null || _imageFiles.Length == 0 || CurrentImage == null)
                return;

            string imgPath = _imageFiles[CurrentIndex];

            string labelFolder = Path.Combine("dataset", "labels", "train");
            string fileName = Path.GetFileNameWithoutExtension(imgPath);
            string txtPath = Path.Combine(labelFolder, fileName + ".txt");

            Annotations.Clear();

            if (!File.Exists(txtPath))
            {
                RequestInvalidate?.Invoke();
                return;
            }

            double imgW = CurrentImage.PixelSize.Width;
            double imgH = CurrentImage.PixelSize.Height;

            var lines = File.ReadAllLines(txtPath);

            foreach (var line in lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 5) continue;

                int classId = int.Parse(parts[0], CultureInfo.InvariantCulture);
                double xCenter = double.Parse(parts[1], CultureInfo.InvariantCulture);
                double yCenter = double.Parse(parts[2], CultureInfo.InvariantCulture);
                double w = double.Parse(parts[3], CultureInfo.InvariantCulture);
                double h = double.Parse(parts[4], CultureInfo.InvariantCulture);

                // 🔥 YOLO → 像素坐标（与你保存完全反向）
                double px = (xCenter - w / 2) * imgW;
                double py = (yCenter - h / 2) * imgH;
                double pw = w * imgW;
                double ph = h * imgH;

                Annotations.Add(new Annotation
                {
                    ClassId = classId,
                    BoundingBox = NormalizeAndClampImageRect(new Rect(px, py, pw, ph))
                });
            } 
        }

        [RelayCommand]
        private void SaveAnnotation()
        {
            if (_imageFiles == null || _imageFiles.Length == 0 || CurrentImage == null) return;

            string imgPath = _imageFiles[CurrentIndex];
            string labelFolder = Path.Combine("dataset", "labels", "train");
            Directory.CreateDirectory(labelFolder);

            string fileName = Path.GetFileNameWithoutExtension(imgPath);
            string txtPath = Path.Combine(labelFolder, fileName + ".txt");

            double imgW = CurrentImage.PixelSize.Width;
            double imgH = CurrentImage.PixelSize.Height;

            using var writer = new StreamWriter(txtPath);
            foreach (var ann in Annotations)
            {
                var rect = NormalizeAndClampImageRect(ann.BoundingBox);
                if (rect.Width <= 0 || rect.Height <= 0)
                    continue;

                double xCenter = (rect.X + rect.Width / 2) / imgW;
                double yCenter = (rect.Y + rect.Height / 2) / imgH;
                double w = rect.Width / imgW;
                double h = rect.Height / imgH;

                writer.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} {1:F6} {2:F6} {3:F6} {4:F6}",
                    ann.ClassId,
                    xCenter,
                    yCenter,
                    w,
                    h));
            }
            Msg = "保存标注成功!";
        }

        /// <summary>
        /// 添加已完成的标注矩形
        /// </summary>
        public void AddAnnotation(Rect rect)
        {
            if (string.IsNullOrEmpty(SelectedClass)) return;
            var imageRect = NormalizeAndClampImageRect(rect);
            if (imageRect.Width <= 0 || imageRect.Height <= 0)
                return;

            int classId = Classes.IndexOf(SelectedClass);
            Annotations.Add(new Annotation { ClassId = classId, BoundingBox = imageRect });
            Msg = $"添加标注成功,ClassId:{classId},rect:{imageRect.X},{imageRect.Y},{imageRect.Width},{imageRect.Height}!";
            RequestInvalidate?.Invoke();
        }

        public Point DisplayToImagePoint(Point displayPoint)
        {
            var zoom = ZoomScale <= 0 ? 1.0 : ZoomScale;
            return new Point(
                Math.Clamp(displayPoint.X / zoom, 0, ImageWidth),
                Math.Clamp(displayPoint.Y / zoom, 0, ImageHeight));
        }

        public Rect ImageToDisplayRect(Rect imageRect)
        {
            var rect = NormalizeAndClampImageRect(imageRect);
            return new Rect(
                rect.X * ZoomScale,
                rect.Y * ZoomScale,
                rect.Width * ZoomScale,
                rect.Height * ZoomScale);
        }

        public void ZoomBy(double delta)
        {
            ZoomScale += delta;
        }

        [RelayCommand]
        private void ZoomIn()
        {
            ZoomBy(ZoomStep);
        }

        [RelayCommand]
        private void ZoomOut()
        {
            ZoomBy(-ZoomStep);
        }

        [RelayCommand]
        private void ResetZoom()
        {
            ZoomScale = 1.0;
        }

        private Rect NormalizeAndClampImageRect(Rect rect)
        {
            var x1 = Math.Min(rect.X, rect.X + rect.Width);
            var y1 = Math.Min(rect.Y, rect.Y + rect.Height);
            var x2 = Math.Max(rect.X, rect.X + rect.Width);
            var y2 = Math.Max(rect.Y, rect.Y + rect.Height);

            x1 = Math.Clamp(x1, 0, ImageWidth);
            y1 = Math.Clamp(y1, 0, ImageHeight);
            x2 = Math.Clamp(x2, 0, ImageWidth);
            y2 = Math.Clamp(y2, 0, ImageHeight);

            return new Rect(x1, y1, Math.Max(0, x2 - x1), Math.Max(0, y2 - y1));
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

        [RelayCommand]
        private void RemoveCurrentImage()
        {
            if (_imageFiles == null || _imageFiles.Length == 0)
                return;

            string imgPath = _imageFiles[CurrentIndex];

            string labelPath = imgPath
                .Replace("\\images\\", "\\labels\\")
                .Replace("/images/", "/labels/")
                .Replace(".jpg", ".txt")
                .Replace(".png", ".txt");

            string emptyImgDir = Path.Combine("dataset", "images", "empty");
            string emptyLabelDir = Path.Combine("dataset", "labels", "empty");

            Directory.CreateDirectory(emptyImgDir);
            Directory.CreateDirectory(emptyLabelDir);

            try
            {
                File.Move(imgPath, Path.Combine(emptyImgDir, Path.GetFileName(imgPath)), true);

                if (File.Exists(labelPath))
                    File.Move(labelPath, Path.Combine(emptyLabelDir, Path.GetFileName(labelPath)), true);

                // 🔥 关键：重新加载
                LoadAllImage();

                LoadCurrentImage();
                SelectedClass = Classes[0];
                Msg = "已移动（并刷新列表）";
            }
            catch (Exception ex)
            {
                Msg = $"操作失败: {ex.Message}";
            }
        }
    }
}
