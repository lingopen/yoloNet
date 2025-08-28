## 一、训练环境
### 1.安装ptython 3.11.9
https://www.python.org/ftp/python/3.11.9/python-3.11.9-amd64.exe
下载并自动安装,勾选add PATH。
### 2.创建Yolo虚拟环境并激活
```
# 跳转到程序根目录
cd D:\yoloNet\yoloNet\bin\Debug\net9.0

# 创建虚拟环境
python -m venv D:\yoloNet\yolov11_env

# 激活环境
Scripts\activate

# 命令提示符显示：(yolov11_env)，表明已经进入虚拟环境；
```
### 3.升级 pip、wheel、setuptools
```
python -m pip install --upgrade pip setuptools wheel
```
### 4.安装 PyTorch（带 CUDA 12.1）
```
# 我的显卡是GTX 1660Ti ,需要安装cu121版本，大概耗时半个小时
pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121

# 如果安装成功，你可以测试
python -c "import torch; print(torch.cuda.is_available())"

# 输出 True 表示 GPU 可用

```
### 5.安装 Yolo
```
pip install ultralytics

# 这个包会自动安装 numpy、opencv-python、torchvision 等常用依赖。
```
在D:\yoloNet\yoloNet\bin\Debug\net9.0\yolov11_env创建一个测试文件：test.py，并运行
```
from ultralytics import YOLO
import cv2

# 加载模型
model = YOLO("yolo11n.pt")

# 读图推理
results = model("https://ultralytics.com/images/bus.jpg")

# 遍历每个结果
for r in results:
    boxes = r.boxes.xyxy.cpu().numpy()   # [x1,y1,x2,y2]
    scores = r.boxes.conf.cpu().numpy()  # 置信度
    cls = r.boxes.cls.cpu().numpy()      # 类别

    img = r.orig_img.copy()
    for (x1,y1,x2,y2), score, c in zip(boxes, scores, cls):
        label = f"{model.names[int(c)]} {score:.2f}"
        cv2.rectangle(img, (int(x1),int(y1)), (int(x2),int(y2)), (0,255,0), 2)
        cv2.putText(img, label, (int(x1), int(y1)-10),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0,255,0), 2)

    cv2.imwrite("out.jpg", img) # 可以看到输出了 out.jpg，并做了标注
```
```
#在激活的虚拟环境中运行:

(yolov11_env) D:\yoloNet\yoloNet\bin\Debug\net9.0\yolov11_env>python -m test.py
```
### 6.安装 ONNX
```
pip install onnx

# 如果你打算同时做 ONNX Runtime 推理，可以顺便安装：
pip install onnxruntime-gpu  # GPU
或
pip install onnxruntime      # CPU
```
## 二、训练和推理
### 1.抽帧
图片会自动保存到根目录生成dataset数据集：D:\yoloNet\yoloNet\bin\Debug\net9.0\dataset\images\train。
<img width="1426" height="752" alt="image" src="https://github.com/user-attachments/assets/6e199fd8-39ae-47b6-a544-43e99c3596e5" />

### 2.标注
标注会自动保存到：D:\yoloNet\yoloNet\bin\Debug\net9.0\dataset\labels\train。
<img width="1421" height="747" alt="image" src="https://github.com/user-attachments/assets/939cabfd-c0fb-4414-bbdc-0951bd16872f" />
### 3.训练
1.data.yaml会自动保存到:D:\yoloNet\yoloNet\bin\Debug\net9.0\dataset\data.yaml
2.train_yolo.py会自动保存到:D:\yoloNet\yoloNet\bin\Debug\net9.0\
```
# 训练脚本可以根据实际情况修改：

from ultralytics import YOLO

# 使用 YOLOv11 nano 模型训练
model = YOLO("yolov11n.pt")  # 可以换成 yolov11s.pt / yolov11m.pt

# 训练
model.train(data="data.yaml", epochs=100, imgsz=640, batch=16, device=0)

```
3.点击“生成验证集”,会在D:\yoloNet\yoloNet\bin\Debug\net9.0\dataset目录下生成val文件夹,格式如下：
```
D:\yoloNet\yoloNet\bin\Debug\net9.0\dataset\
    images\
        train\
        val\
    labels\
        train\
        val\
    data.yaml
```
<img width="1426" height="752" alt="image" src="https://github.com/user-attachments/assets/ebc671fb-d900-4e6f-8bf5-c6c8a68ae781" />

### 4.推理
<img width="1426" height="752" alt="image" src="https://github.com/user-attachments/assets/8061c281-e26c-427f-b9ed-a9d3ea294f02" />

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
