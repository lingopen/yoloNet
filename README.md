### 1.抽帧
<img width="1426" height="752" alt="image" src="https://github.com/user-attachments/assets/b029c3e2-e4fa-4baa-9de1-149027578b01" />

### 2.标注
<img width="1426" height="752" alt="image" src="https://github.com/user-attachments/assets/0b62011c-1b1d-4019-a6a0-7239a1a5e182" />

### 3.训练
<img width="1426" height="752" alt="image" src="https://github.com/user-attachments/assets/327b29e2-86a5-4c58-a437-425d75599be3" />

### 4.推理
<img width="1920" height="1017" alt="image" src="https://github.com/user-attachments/assets/5019e567-6955-4edd-800e-1cbeaa909341" />

### 5.使用CUDA

1.由于开发环境使用的是 GTX 1660 Ti，并且程序使用了最新的 Microsoft.ML.OnnxRuntime.Gpu.Windows 版本1.22.1,对应的是 CUDA 12的版本，因此安装 CUDA 12.2.0 (下载cuda_12.2.0_536.25_windows）以及 cuDNN(下载cudnn_9.10.2_windows)
2.将cuDNN目录的 12.9 文件夹里面的 bin、lib、include3个文件夹拷贝到你的 CUDA 12.2 的目录中,覆盖bin、lib、include3个文件夹
cuDNN的路径：
<img width="857" height="242" alt="image" src="https://github.com/user-attachments/assets/5cb53a51-5aa5-4bcc-86e0-92c24fdf7066" />
CUDA的路径：
<img width="940" height="497" alt="image" src="https://github.com/user-attachments/assets/1ecf00b4-bd3c-481f-b259-013ad5b2cc74" />
3.检查环境变量 C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.2\bin 这一步在安装CUDA时自动安装；
<img width="1125" height="657" alt="image" src="https://github.com/user-attachments/assets/8bb1c14f-f58c-43c0-a922-c17ba115829e" />
4.代码中使用CUDA
```C#
var options = new SessionOptions();
try
{
    options.AppendExecutionProvider_CUDA(0);
    //options.LogSeverityLevel =  OrtLoggingLevel.ORT_LOGGING_LEVEL_INFO; // 打印详细信息
    Console.WriteLine("CUDA Execution Provider added successfully.");
}
catch (OnnxRuntimeException ex)
{
    Console.WriteLine("CUDA Execution Provider failed: " + ex.Message);
    Console.WriteLine("Fallback to CPU.");
}
// 创建 InferenceSession
session = new InferenceSession(onnxPath, options);
```
