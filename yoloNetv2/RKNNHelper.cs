
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace yoloNetv2
{
    /// <summary>
    /// RKNN Lite2 推理帮助类（工程级别，支持 NHWC 输入和 YOLO 输出解析）
    /// </summary>
    // ------------------------
    // RKNN Helper
    // ------------------------
    public static class RKNNHelper
    {
        private static IntPtr _ctx = IntPtr.Zero;
        static bool _initialized = false;

        public static void Init(string modelPath)
        {
            if (_initialized) return;
            if (!File.Exists(modelPath)) throw new FileNotFoundException(modelPath); 
            var _modelData = File.ReadAllBytes(modelPath);
            IntPtr modelPtr = Marshal.AllocHGlobal(_modelData.Length);
            Marshal.Copy(_modelData, 0, modelPtr, _modelData.Length);

            int ret = RknnNative.rknn_init(ref _ctx, modelPtr, (uint)_modelData.Length, 0, IntPtr.Zero);
            Marshal.FreeHGlobal(modelPtr);
            if (ret != RknnNative.RKNN_SUCC)
                throw new Exception($"RKNN init failed: {RknnNative.GetErrorMessage(ret)}");

            // 查询输出 tensor 属性
            var outAttr = new RknnNative.rknn_tensor_attr();
            outAttr.index = 0;
            IntPtr outPtr = Marshal.AllocHGlobal(Marshal.SizeOf(outAttr));
            Marshal.StructureToPtr(outAttr, outPtr, false);

            ret = RknnNative.rknn_query(_ctx, 2, outPtr, (uint)Marshal.SizeOf<RknnNative.rknn_tensor_attr>());
            if (ret != RknnNative.RKNN_SUCC)
            {
                Marshal.FreeHGlobal(outPtr);
                throw new Exception($"RKNN query output_attr failed: {RknnNative.GetErrorMessage(ret)}");
            }

            outAttr = Marshal.PtrToStructure<RknnNative.rknn_tensor_attr>(outPtr);
            Marshal.FreeHGlobal(outPtr);

            var  _outputSize = 1;
            for (int i = 0; i < outAttr.n_dims; i++)
                _outputSize *= (int)outAttr.dims[i];

            _initialized = true;
            Console.WriteLine($"RKNN initialized. OutputSize={_outputSize}");
        }
        public static void Release()
        {
            if (_ctx != IntPtr.Zero)
            {
                RknnNative.rknn_destroy(_ctx);
                _ctx = IntPtr.Zero;
                _initialized = false;
                Console.WriteLine("RKNN context released");
            }
        }
        // Half16 -> float32
        private static float HalfToFloat(ushort half)
        {
            int sign = (half >> 15) & 0x0001;
            int exp = (half >> 10) & 0x001F;
            int mant = half & 0x03FF;

            if (exp == 0)
            {
                if (mant == 0) return sign == 0 ? 0f : -0f;
                float f = (float)(mant / 1024.0) * (float)Math.Pow(2, -14);
                return sign == 0 ? f : -f;
            }
            else if (exp == 31)
            {
                if (mant == 0) return sign == 0 ? float.PositiveInfinity : float.NegativeInfinity;
                return float.NaN;
            }

            float val = (1 + mant / 1024f) * (float)Math.Pow(2, exp - 15);
            return sign == 0 ? val : -val;
        }

        // Letterbox NHWC
        private static byte[] FillTensorWithLetterboxNHWC(byte[] rgb, int srcW, int srcH, int dstSize)
        {
            byte[] tensor = new byte[dstSize * dstSize * 3]; // NHWC
            float scale = Math.Min((float)dstSize / srcW, (float)dstSize / srcH);
            int newW = (int)(srcW * scale);
            int newH = (int)(srcH * scale);
            int offsetX = (dstSize - newW) / 2;
            int offsetY = (dstSize - newH) / 2;

            for (int y = 0; y < newH; y++)
            {
                for (int x = 0; x < newW; x++)
                {
                    int srcX = (int)(x / scale);
                    int srcY = (int)(y / scale);
                    int srcIndex = (srcY * srcW + srcX) * 3;
                    int dstIndex = ((y + offsetY) * dstSize + (x + offsetX)) * 3;

                    tensor[dstIndex + 0] = rgb[srcIndex + 0]; // R
                    tensor[dstIndex + 1] = rgb[srcIndex + 1]; // G
                    tensor[dstIndex + 2] = rgb[srcIndex + 2]; // B
                }
            }
            return tensor;
        }

        // SKBitmap -> byte[] RGB
        private static byte[] SKBitmapToRGB(SKBitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            byte[] rgb = new byte[width * height * 3];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    int idx = (y * width + x) * 3;
                    rgb[idx + 0] = c.Red;
                    rgb[idx + 1] = c.Green;
                    rgb[idx + 2] = c.Blue;
                }
            }
            return rgb;
        }

        // IoU
        private static float IoU(Box a, Box b)
        {
            float x1 = Math.Max(a.X1, b.X1);
            float y1 = Math.Max(a.Y1, b.Y1);
            float x2 = Math.Min(a.X2, b.X2);
            float y2 = Math.Min(a.Y2, b.Y2);
            float w = Math.Max(0, x2 - x1);
            float h = Math.Max(0, y2 - y1);
            float inter = w * h;
            float union = (a.X2 - a.X1) * (a.Y2 - a.Y1) + (b.X2 - b.X1) * (b.Y2 - b.Y1) - inter;
            return union > 0 ? inter / union : 0;
        }

        // ------------------------
        // 核心推理函数
        // ------------------------
        public static Box[] InferenceAndDebugNHWC(SKBitmap bmp, int dstSize,
            float confThreshold = 0.3f, float iouThreshold = 0.5f)
        {
            if (_ctx == IntPtr.Zero) throw new Exception("RKNN context not initialized");

            int srcW = bmp.Width;
            int srcH = bmp.Height;
            byte[] rgb = SKBitmapToRGB(bmp);
            byte[] input = FillTensorWithLetterboxNHWC(rgb, srcW, srcH, dstSize);

            IntPtr inputPtr = Marshal.AllocHGlobal(input.Length);
            Marshal.Copy(input, 0, inputPtr, input.Length);

            var inputs = new RknnNative.rknn_input[1];
            inputs[0] = new RknnNative.rknn_input
            {
                index = 0,
                buf = inputPtr,
                size = (uint)input.Length,
                pass_through = 0,
                type = RknnNative.rknn_tensor_type.RKNN_TENSOR_UINT8,
                fmt = RknnNative.rknn_tensor_format.RKNN_TENSOR_NHWC
            };

            int ret = RknnNative.rknn_inputs_set(_ctx, 1, inputs);
            Marshal.FreeHGlobal(inputPtr);
            if (ret != RknnNative.RKNN_SUCC)
                throw new Exception($"rknn_inputs_set failed: {RknnNative.GetErrorMessage(ret)}");

            // 查询输出属性
            var outAttr = new RknnNative.rknn_tensor_attr { index = 0 };
            IntPtr outPtr = Marshal.AllocHGlobal(Marshal.SizeOf(outAttr));
            Marshal.StructureToPtr(outAttr, outPtr, false);
            ret = RknnNative.rknn_query(_ctx, 2, outPtr, (uint)Marshal.SizeOf<RknnNative.rknn_tensor_attr>());
            outAttr = Marshal.PtrToStructure<RknnNative.rknn_tensor_attr>(outPtr);
            Marshal.FreeHGlobal(outPtr);

            IntPtr outBuffer = Marshal.AllocHGlobal((int)outAttr.size);
            var outputs = new RknnNative.rknn_output[1]
            {
            new RknnNative.rknn_output
            {
                index = 0,
                is_prealloc = 1,
                buf = outBuffer,
                size = outAttr.size
            }
            };

            ret = RknnNative.rknn_outputs_get(_ctx, 1, outputs, 0);
            if (ret != RknnNative.RKNN_SUCC)
            {
                Marshal.FreeHGlobal(outBuffer);
                throw new Exception($"rknn_outputs_get failed: {RknnNative.GetErrorMessage(ret)}");
            }

            // 解析输出 float[]
            int outCount;
            float[] output;

            if (outAttr.type == RknnNative.rknn_tensor_type.RKNN_TENSOR_FLOAT16)
            {
                outCount = (int)(outAttr.size / 2);
                byte[] tmpBytes = new byte[outAttr.size];
                Marshal.Copy(outBuffer, tmpBytes, 0, tmpBytes.Length);
                output = new float[outCount];
                for (int i = 0; i < outCount; i++)
                {
                    ushort half = (ushort)(tmpBytes[i * 2] | (tmpBytes[i * 2 + 1] << 8));
                    output[i] = HalfToFloat(half);
                }
            }
            else if (outAttr.type == RknnNative.rknn_tensor_type.RKNN_TENSOR_FLOAT32)
            {
                outCount = (int)(outAttr.size / 4);
                output = new float[outCount];
                Marshal.Copy(outBuffer, output, 0, outCount);
            }
            else if (outAttr.type == RknnNative.rknn_tensor_type.RKNN_TENSOR_UINT8)
            {
                outCount = (int)outAttr.size;
                byte[] tmp = new byte[outCount];
                Marshal.Copy(outBuffer, tmp, 0, outCount);
                output = new float[outCount];
                for (int i = 0; i < outCount; i++) output[i] = tmp[i] / 255f;
            }
            else
            {
                Marshal.FreeHGlobal(outBuffer);
                throw new Exception($"Unsupported output type: {outAttr.type}");
            }

            Marshal.FreeHGlobal(outBuffer);

            // 调试输出
            Console.WriteLine("[DEBUG] Output first 10 values:");
            for (int i = 0; i < Math.Min(10, output.Length); i++)
                Console.WriteLine($"output[{i}] = {output[i]}");

            // 解析 boxes
            int numBoxes = output.Length / 5;
            var boxes = new List<Box>();
            float scale = Math.Min((float)dstSize / srcW, (float)dstSize / srcH);
            float offsetX = (dstSize - srcW * scale) / 2;
            float offsetY = (dstSize - srcH * scale) / 2;

            for (int i = 0; i < numBoxes; i++)
            {
                float cx = output[i * 5 + 0];
                float cy = output[i * 5 + 1];
                float w = output[i * 5 + 2];
                float h = output[i * 5 + 3];
                float conf = output[i * 5 + 4];

                if (float.IsNaN(conf) || float.IsInfinity(conf) || conf < confThreshold)
                    continue;

                float x1 = (cx - w / 2 - offsetX) / scale;
                float y1 = (cy - h / 2 - offsetY) / scale;
                float x2 = (cx + w / 2 - offsetX) / scale;
                float y2 = (cy + h / 2 - offsetY) / scale;

                boxes.Add(new Box { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Score = conf, ClassId = 0 });
            }

            // NMS 去重
            var nmsList = new List<Box>();
            var sorted = boxes.OrderByDescending(b => b.Score).ToList();
            while (sorted.Count > 0)
            {
                var best = sorted[0];
                nmsList.Add(best);
                sorted.RemoveAt(0);
                sorted = sorted.Where(b => IoU(best, b) < iouThreshold).ToList();
            }

            Console.WriteLine(nmsList.Count > 0 ? $"检测到 {nmsList.Count} 个目标" : "未检测到目标");
            return nmsList.ToArray();
        }
    }
}

