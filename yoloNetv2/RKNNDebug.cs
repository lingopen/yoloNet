using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace yoloNetv2
{
    public static class RKNNDebug
    {
        private static IntPtr _ctx = IntPtr.Zero;
        private static bool _initialized = false;
        private static int _outputSize = 0;
        private static byte[] _modelData;

        public static void Init(string modelPath)
        {
            if (_initialized) return;
            if (!File.Exists(modelPath)) throw new FileNotFoundException(modelPath);

            _modelData = File.ReadAllBytes(modelPath);
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

            _outputSize = 1;
            for (int i = 0; i < outAttr.n_dims; i++)
                _outputSize *= (int)outAttr.dims[i];

            _initialized = true;
            Console.WriteLine($"RKNN initialized. OutputSize={_outputSize}");
        }

        /// <summary>
        /// 填充 NHWC Tensor（对应 RKNN_TENSOR_NHWC）
        /// </summary>
        public static float[] FillTensorWithLetterboxNHWC(byte[] rgb, int srcW, int srcH, int dstSize)
        {
            float[] tensor = new float[1 * dstSize * dstSize * 3]; // NHWC: [1,H,W,C]

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
                    int dstIndexBase = ((offsetY + y) * dstSize + (offsetX + x)) * 3;

                    tensor[dstIndexBase + 0] = rgb[srcIndex + 0] / 255f; // R
                    tensor[dstIndexBase + 1] = rgb[srcIndex + 1] / 255f; // G
                    tensor[dstIndexBase + 2] = rgb[srcIndex + 2] / 255f; // B
                }
            }

            return tensor;
        }

        /// <summary>
        /// RKNN 推理 + 打印调试信息 + NMS
        /// 注意：输入必须 NHWC
        /// </summary>
        public static Box[] InferenceAndDebugNHWC(float[] input, int srcW, int srcH, int dstSize, float confThreshold = 0.3f, float iouThreshold = 0.5f)
        {
            if (_ctx == 0)
                throw new Exception("RKNN context is not initialized!");

            if (input == null || input.Length == 0)
                throw new ArgumentException("Input tensor is empty!");

            int inputSize = input.Length * sizeof(float);
            IntPtr inputPtr = Marshal.AllocHGlobal(inputSize);
            Marshal.Copy(input, 0, inputPtr, input.Length);

            var inputs = new RknnNative.rknn_input[1];
            inputs[0] = new RknnNative.rknn_input
            {
                index = 0,
                buf = inputPtr,
                size = (uint)inputSize,
                pass_through = 0,
                type = RknnNative.rknn_tensor_type.RKNN_TENSOR_FLOAT32,
                fmt = RknnNative.rknn_tensor_format.RKNN_TENSOR_NHWC // NHWC
            };

            int ret = RknnNative.rknn_inputs_set(_ctx, 1, inputs);
            Marshal.FreeHGlobal(inputPtr);
            if (ret != RknnNative.RKNN_SUCC)
                throw new Exception($"RKNN set inputs failed: {RknnNative.GetErrorMessage(ret)}");

            // ------------------------
            // 获取输出属性
            // ------------------------
            var outAttr = new RknnNative.rknn_tensor_attr();
            outAttr.index = 0;
            IntPtr outPtr = Marshal.AllocHGlobal(Marshal.SizeOf(outAttr));
            Marshal.StructureToPtr(outAttr, outPtr, false);

            ret = RknnNative.rknn_query(_ctx, 2 /* RKNN_QUERY_OUTPUT_ATTR */, outPtr, (uint)Marshal.SizeOf<RknnNative.rknn_tensor_attr>());
            if (ret != RknnNative.RKNN_SUCC)
            {
                Marshal.FreeHGlobal(outPtr);
                throw new Exception($"RKNN query output_attr failed: {RknnNative.GetErrorMessage(ret)}");
            }

            outAttr = Marshal.PtrToStructure<RknnNative.rknn_tensor_attr>(outPtr);
            Marshal.FreeHGlobal(outPtr);

            int outCount = (int)(outAttr.size / sizeof(float));
            var output = new float[outCount];
            IntPtr outBuffer = Marshal.AllocHGlobal((int)outAttr.size);

            var outputs = new RknnNative.rknn_output[1];
            outputs[0] = new RknnNative.rknn_output
            {
                index = 0,
                is_prealloc = 1,
                buf = outBuffer,
                size = outAttr.size
            };

            ret = RknnNative.rknn_outputs_get(_ctx, 1, outputs, 0);
            if (ret != RknnNative.RKNN_SUCC)
            {
                Marshal.FreeHGlobal(outBuffer);
                throw new Exception($"RKNN get outputs failed: {RknnNative.GetErrorMessage(ret)}");
            }

            Marshal.Copy(outBuffer, output, 0, outCount);
            Marshal.FreeHGlobal(outBuffer);

            Console.WriteLine($"[DEBUG] Output dims: {string.Join(",", outAttr.dims[..(int)outAttr.n_dims])} size={outAttr.size}");
            Console.WriteLine("[DEBUG] Output first 10 values:");
            for (int i = 0; i < Math.Min(10, output.Length); i++)
                Console.WriteLine($"output[{i}] = {output[i]}");

            // ------------------------
            // 解析 boxes (假设 [cx, cy, w, h, conf])
            // ------------------------
            int numBoxes = outCount / 5;
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

                boxes.Add(new Box
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Score = conf,
                    ClassId = 0
                });
            }

            // ------------------------
            // 打印 boxes
            // ------------------------
            if (boxes.Count > 0)
            {
                Console.WriteLine($"检测到 {boxes.Count} 个目标：");
                for (int i = 0; i < boxes.Count; i++)
                {
                    var b = boxes[i];
                    Console.WriteLine($"Box[{i}] - X1:{b.X1:F2}, Y1:{b.Y1:F2}, X2:{b.X2:F2}, Y2:{b.Y2:F2}, Score:{b.Score:F3}");
                }
            }
            else
            {
                Console.WriteLine("未检测到目标");
            }

            // ------------------------
            // NMS 去重
            // ------------------------
            var nmsList = new List<Box>();
            var sorted = boxes.OrderByDescending(b => b.Score).ToList();
            while (sorted.Count > 0)
            {
                var best = sorted[0];
                nmsList.Add(best);
                sorted.RemoveAt(0);
                sorted = sorted.Where(b => IoU(best, b) < iouThreshold).ToList();
            }

            return nmsList.ToArray();
        }

        private static float IoU(Box a, Box b)
        {
            float x1 = Math.Max(a.X1, b.X1);
            float y1 = Math.Max(a.Y1, b.Y1);
            float x2 = Math.Min(a.X2, b.X2);
            float y2 = Math.Min(a.Y2, b.Y2);

            float inter = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            float union = (a.X2 - a.X1) * (a.Y2 - a.Y1) + (b.X2 - b.X1) * (b.Y2 - b.Y1) - inter;
            return inter / union;
        }
    }

}
