
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace yoloNetv2
{

    // ------------------------
    // RKNN Helper
    // ------------------------
    public static class RKNNHelper
    {
        private static ulong _rknnCtx = 0;
        private static bool _initialized = false;

        public static void Init(string modelPath)
        {
            if (_initialized) return;
            if (!System.IO.File.Exists(modelPath))
                throw new Exception($"RKNN model not found: {modelPath}");

            byte[] rknnModel = System.IO.File.ReadAllBytes(modelPath);
            IntPtr modelPtr = Marshal.AllocHGlobal(rknnModel.Length);
            Marshal.Copy(rknnModel, 0, modelPtr, rknnModel.Length);

            RknnNative.rknn_init_extend extend = new RknnNative.rknn_init_extend
            {
                ctx = 0,
                reserved = new byte[112]
            };

            int ret = RknnNative.rknn_init(ref _rknnCtx, modelPtr, (uint)rknnModel.Length, 0, ref extend);
            Marshal.FreeHGlobal(modelPtr);

            if (ret != RknnNative.RKNN_SUCC)
                throw new Exception($"rknn_init failed: {RknnNative.GetErrorMessage(ret)}");

            _initialized = true;
        }

        public static void Release()
        {
            if (!_initialized) return;
            RknnNative.rknn_destroy(_rknnCtx);
            _rknnCtx = 0;
            _initialized = false;
        }

        public static Box[] Run(SKBitmap bmp, int modelInputSize = 320, float confThreshold = 0.3f, float iouThreshold = 0.5f)
        {
            if (!_initialized) throw new Exception("RKNN not initialized");

            // 1️⃣ Letterbox + 最近邻 + NHWC float32
            var (inputData, scale, offsetX, offsetY) = LetterboxAndConvert(bmp, modelInputSize);

            // 2️⃣ 设置输入
            IntPtr bufPtr = Marshal.AllocHGlobal(sizeof(float) * inputData.Length);
            Marshal.Copy(inputData, 0, bufPtr, inputData.Length);

            var input = new RknnNative.rknn_input
            {
                index = 0,
                buf = bufPtr,
                size = (uint)(sizeof(float) * inputData.Length),
                type = RknnNative.rknn_tensor_type.RKNN_TENSOR_FLOAT32,
                fmt = RknnNative.rknn_tensor_format.RKNN_TENSOR_NHWC,
                pass_through = 0
            };

            int ret = RknnNative.rknn_inputs_set(_rknnCtx, 1, new[] { input });
            Marshal.FreeHGlobal(bufPtr);

            if (ret != RknnNative.RKNN_SUCC)
                throw new Exception($"rknn_inputs_set failed: {RknnNative.GetErrorMessage(ret)}");

            // 3️⃣ 执行推理
            var runExtend = new RknnNative.rknn_run_extend
            {
                frame_id = 0,
                non_block = 0,
                timeout_ms = 0,
                fence_fd = -1
            };

            ret = RknnNative.rknn_run(_rknnCtx, ref runExtend);
            if (ret != RknnNative.RKNN_SUCC)
                throw new Exception($"rknn_run failed: {RknnNative.GetErrorMessage(ret)}");

            // 4️⃣ 获取输出数量
            RknnNative.rknn_input_output_num ioNum = new RknnNative.rknn_input_output_num();
            IntPtr ptrIO = Marshal.AllocHGlobal(Marshal.SizeOf(ioNum));
            ret = RknnNative.rknn_query(_rknnCtx, RknnNative.rknn_query_cmd.RKNN_QUERY_IN_OUT_NUM, ptrIO, (uint)Marshal.SizeOf(ioNum));
            ioNum = Marshal.PtrToStructure<RknnNative.rknn_input_output_num>(ptrIO);
            Marshal.FreeHGlobal(ptrIO);

            int nOutput = (int)ioNum.n_output;

            // 5️⃣ 获取输出属性
            RknnNative.rknn_tensor_attr[] outputAttrs = new RknnNative.rknn_tensor_attr[nOutput];
            int attrSize = Marshal.SizeOf(typeof(RknnNative.rknn_tensor_attr));
            IntPtr ptrAttr = Marshal.AllocHGlobal(attrSize * nOutput);
            ret = RknnNative.rknn_query(_rknnCtx, RknnNative.rknn_query_cmd.RKNN_QUERY_OUTPUT_ATTR, ptrAttr, (uint)(attrSize * nOutput));
            for (int i = 0; i < nOutput; i++)
                outputAttrs[i] = Marshal.PtrToStructure<RknnNative.rknn_tensor_attr>(IntPtr.Add(ptrAttr, i * attrSize));
            Marshal.FreeHGlobal(ptrAttr);

            // 6️⃣ 获取输出数据
            var outputs = new RknnNative.rknn_output[nOutput];
            var outputExtend = new RknnNative.rknn_output_extend { frame_id = runExtend.frame_id };
            for (int i = 0; i < nOutput; i++)
            {
                outputs[i].want_float = 1;
                outputs[i].is_prealloc = 0;
            }

            ret = RknnNative.rknn_outputs_get(_rknnCtx, (uint)nOutput, outputs, ref outputExtend, 0);
            if (ret != RknnNative.RKNN_SUCC)
                throw new Exception($"rknn_outputs_get failed: {RknnNative.GetErrorMessage(ret)}");

            // 7️⃣ 假设单输出，float32，shape (5,N)
            int nBoxes = (int)outputs[0].size / sizeof(float) / 5;
            float[] pred = new float[5 * nBoxes];
            Marshal.Copy(outputs[0].buf, pred, 0, pred.Length);

            RknnNative.rknn_outputs_release(_rknnCtx, (uint)nOutput, outputs);

            // 8️⃣ 后处理
            var boxes = new List<Box>();
            for (int i = 0; i < nBoxes; i++)
            {
                float conf = pred[4 * nBoxes + i]; // conf
                if (conf < confThreshold) continue;

                float cx = pred[0 * nBoxes + i];
                float cy = pred[1 * nBoxes + i];
                float w = pred[2 * nBoxes + i];
                float h = pred[3 * nBoxes + i];

                float x1 = (cx - w / 2 - offsetX) / scale;
                float y1 = (cy - h / 2 - offsetY) / scale;
                float x2 = (cx + w / 2 - offsetX) / scale;
                float y2 = (cy + h / 2 - offsetY) / scale;

                boxes.Add(new Box { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Score = conf, ClassId = 0 });
            }

            // 9️⃣ NMS
            return NMS(boxes, iouThreshold).ToArray();
        }

        // ====== Letterbox + 最近邻 + NHWC float32 ======
        private static (float[] data, float scale, int offsetX, int offsetY) LetterboxAndConvert(SKBitmap bmp, int dstSize)
        {
            int srcW = bmp.Width;
            int srcH = bmp.Height;
            float scale = Math.Min((float)dstSize / srcW, (float)dstSize / srcH);
            int newW = (int)(srcW * scale);
            int newH = (int)(srcH * scale);
            int offsetX = (dstSize - newW) / 2;
            int offsetY = (dstSize - newH) / 2;

            float[] data = new float[dstSize * dstSize * 3];

            using (var pixmap = bmp.PeekPixels())
            {
                for (int y = 0; y < newH; y++)
                {
                    int srcY = (int)(y / scale);
                    for (int x = 0; x < newW; x++)
                    {
                        int srcX = (int)(x / scale);
                        SKColor c = pixmap.GetPixelColor(srcX, srcY);
                        int idx = ((y + offsetY) * dstSize + (x + offsetX)) * 3;
                        data[idx + 0] = c.Red / 255f;
                        data[idx + 1] = c.Green / 255f;
                        data[idx + 2] = c.Blue / 255f;
                    }
                }
            }
            return (data, scale, offsetX, offsetY);
        }

        // ====== 简单 NMS ======
        private static List<Box> NMS(List<Box> boxes, float iouThres)
        {
            var keep = new List<Box>();
            var sorted = boxes.OrderByDescending(b => b.Score).ToList();
            while (sorted.Count > 0)
            {
                var best = sorted[0];
                keep.Add(best);
                sorted.RemoveAt(0);
                sorted = sorted.Where(b => IoU(best, b) < iouThres).ToList();
            }
            return keep;
        }

        private static float IoU(Box a, Box b)
        {
            float x1 = Math.Max(a.X1, b.X1);
            float y1 = Math.Max(a.Y1, b.Y1);
            float x2 = Math.Min(a.X2, b.X2);
            float y2 = Math.Min(a.Y2, b.Y2);
            float inter = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            float areaA = (a.X2 - a.X1) * (a.Y2 - a.Y1);
            float areaB = (b.X2 - b.X1) * (b.Y2 - b.Y1);
            return inter / (areaA + areaB - inter + 1e-6f);
        }
    }
}

