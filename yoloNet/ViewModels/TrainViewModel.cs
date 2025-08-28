using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace yoloNet.ViewModels
{
    public partial class TrainViewModel : ViewModelBase
    {
        [ObservableProperty] private string yamlContent = "";
        [ObservableProperty] string pyContent = "";
        [ObservableProperty] private string log = "";

        private Process? _trainProcess;

        // 数据集路径
        private readonly string imagesTrain = Path.Combine("dataset", "images", "train");
        private readonly string labelsTrain = Path.Combine("dataset", "labels", "train");
        private readonly string imagesVal = Path.Combine("dataset", "images", "val");
        private readonly string labelsVal = Path.Combine("dataset", "labels", "val");

        public TrainViewModel()
        {
            LoadYaml();
            LoadPy();
        }
        private void LoadYaml()
        {
            string path = Path.Combine("dataset", "data.yaml");
            if (File.Exists(path))
                YamlContent = File.ReadAllText(path);
        }

        [RelayCommand]
        private void SaveYaml()
        {
            string path = Path.Combine("dataset", "data.yaml");
            if (!Directory.Exists(@"dataset"))
            {
                Directory.CreateDirectory(@"dataset");
            }
            File.WriteAllText(path, YamlContent);
            Log += "已保存 data.yaml\n";
        }

        private void LoadPy()
        {
            string path = Path.Combine("train_yolo.py"); // 与 dataset 同级
            if (File.Exists(path))
                PyContent = File.ReadAllText(path);
            else
                PyContent = "# train_yolo.py 文件不存在，可以在这里编辑";
        }

        [RelayCommand]
        private void SavePy()
        {
            string path = Path.Combine("train_yolo.py"); // 与 dataset 同级
            File.WriteAllText(path, PyContent);
            Log += "已保存 train_yolo.py\n";
        }



        [RelayCommand]
        private void StartTraining()
        {
            if (_trainProcess != null && !_trainProcess.HasExited) return;

            // 这里直接指定虚拟环境 python.exe
            var venvPython = Path.Combine(AppContext.BaseDirectory, "yolov11_env", "Scripts", "python.exe");

            var psi = new ProcessStartInfo
            {
                FileName = venvPython,
                Arguments = "train_yolo.py",   // 注意：这里是脚本相对路径，如果不在同目录，改成绝对路径
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8, // 🔑 关键
                StandardErrorEncoding = Encoding.UTF8   // 🔑 关键
            };

            _trainProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

            _trainProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    var line = e.Data.Replace("\r", "");
                    Log += line + "\n";
                }
            };

            _trainProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data)) Log += e.Data + "\n";
            };

            _trainProcess.Exited += (s, e) => Log += "训练结束\n";

            _trainProcess.Start();
            _trainProcess.BeginOutputReadLine();
            _trainProcess.BeginErrorReadLine();

        }

        [RelayCommand]
        private void StopTraining()
        {
            if (_trainProcess != null && !_trainProcess.HasExited)
            {
                _trainProcess.Kill(true);
                Log += "训练已停止\n";
            }
        }


        [RelayCommand]
        private void CreateValidationSet()
        {
            double valRatio = 0.1; // 默认抽取 10%
            string imagesTrain = Path.Combine("dataset", "images", "train");
            string labelsTrain = Path.Combine("dataset", "labels", "train");
            string imagesVal = Path.Combine("dataset", "images", "val");
            string labelsVal = Path.Combine("dataset", "labels", "val");


            // 删除旧的验证集
            if (Directory.Exists(imagesVal))
                Directory.Delete(imagesVal, true);
            if (Directory.Exists(labelsVal))
                Directory.Delete(labelsVal, true);

            Directory.CreateDirectory(imagesVal);
            Directory.CreateDirectory(labelsVal);

            var allImages = Directory.GetFiles(imagesTrain, "*.jpg");
            int valCount = (int)(allImages.Length * valRatio);

            var rnd = new Random();
            var valImages = allImages.OrderBy(x => rnd.Next()).Take(valCount);

            foreach (var imgPath in valImages)
            {
                var fileName = Path.GetFileName(imgPath);

                // 复制图片
                File.Copy(imgPath, Path.Combine(imagesVal, fileName), true);

                // 复制对应标签
                string labelPath = Path.Combine(labelsTrain, Path.ChangeExtension(fileName, ".txt"));
                if (File.Exists(labelPath))
                {
                    File.Copy(labelPath, Path.Combine(labelsVal, Path.GetFileName(labelPath)), true);
                }

                Log += $"复制 {fileName} 到验证集\n";
            }

            Log += $"完成验证集生成，共 {valCount} 张图片\n";
        }
    }
}
