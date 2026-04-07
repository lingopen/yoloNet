using System;
using System.Runtime.InteropServices;

namespace yoloNetv2
{
    public static class RknnNative
    {
        // ---------------- RKNN 常量 ----------------
        public const int RKNN_SUCC = 0;
        public const int RKNN_ERR_FAIL = -1;
        public const int RKNN_ERR_PARAM_INVALID = -5;

        public enum rknn_tensor_type : int
        {
            RKNN_TENSOR_FLOAT32 = 0,
            RKNN_TENSOR_FLOAT16,
            RKNN_TENSOR_INT8,
            RKNN_TENSOR_UINT8,
            RKNN_TENSOR_INT16,
            RKNN_TENSOR_UINT16,
            RKNN_TENSOR_INT32,
            RKNN_TENSOR_UINT32,
            RKNN_TENSOR_INT64,
            RKNN_TENSOR_BOOL,
            RKNN_TENSOR_INT4,
            RKNN_TENSOR_BFLOAT16,
        }

        public enum rknn_tensor_format : int
        {
            RKNN_TENSOR_NCHW = 0,
            RKNN_TENSOR_NHWC,
            RKNN_TENSOR_NC1HWC2,
            RKNN_TENSOR_UNDEFINED
        }

        public enum rknn_tensor_qnt_type : int
        {
            RKNN_TENSOR_QNT_NONE = 0,
            RKNN_TENSOR_QNT_DFP,
            RKNN_TENSOR_QNT_AFFINE_ASYMMETRIC
        }

        public enum rknn_query_cmd : int
        {
            RKNN_QUERY_IN_OUT_NUM = 0,
            RKNN_QUERY_INPUT_ATTR = 1,
            RKNN_QUERY_OUTPUT_ATTR = 2,
            RKNN_QUERY_PERF_DETAIL = 3,
            RKNN_QUERY_PERF_RUN = 4,
            RKNN_QUERY_SDK_VERSION = 5,
            RKNN_QUERY_MEM_SIZE = 6,
            RKNN_QUERY_CUSTOM_STRING = 7,
            RKNN_QUERY_NATIVE_INPUT_ATTR = 8,
            RKNN_QUERY_NATIVE_OUTPUT_ATTR = 9,
            RKNN_QUERY_NATIVE_NHWC_INPUT_ATTR = 10,
            RKNN_QUERY_NATIVE_NHWC_OUTPUT_ATTR = 11,
            RKNN_QUERY_DEVICE_MEM_INFO = 12,
            RKNN_QUERY_INPUT_DYNAMIC_RANGE = 13,
            RKNN_QUERY_CURRENT_INPUT_ATTR = 14,
            RKNN_QUERY_CURRENT_OUTPUT_ATTR = 15,
            RKNN_QUERY_CURRENT_NATIVE_INPUT_ATTR = 16,
            RKNN_QUERY_CURRENT_NATIVE_OUTPUT_ATTR = 17
        }

        // ---------------- 结构体 ----------------
        [StructLayout(LayoutKind.Sequential)]
        public struct rknn_input_output_num
        {
            public uint n_input;
            public uint n_output;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct rknn_tensor_attr
        {
            public uint index;
            public uint n_dims;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public uint[] dims;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string name;
            public uint n_elems;
            public uint size;
            public rknn_tensor_format fmt;
            public rknn_tensor_type type;
            public rknn_tensor_qnt_type qnt_type;
            public sbyte fl;
            public int zp;
            public float scale;
            public uint w_stride;
            public uint size_with_stride;
            public byte pass_through;
            public uint h_stride;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct rknn_input
        {
            public uint index;
            public IntPtr buf;
            public uint size;
            public byte pass_through;
            public rknn_tensor_type type;
            public rknn_tensor_format fmt;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct rknn_output
        {
            public byte want_float;
            public byte is_prealloc;
            public uint index;
            public IntPtr buf;
            public uint size;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct rknn_init_extend
        {
            public ulong ctx; // rknn_context 在非ARM64是 uint64
            public int real_model_offset;
            public uint real_model_size;
            public int model_buffer_fd;
            public uint model_buffer_flags;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 112)]
            public byte[] reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct rknn_run_extend
        {
            public ulong frame_id;
            public int non_block;
            public int timeout_ms;
            public int fence_fd;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct rknn_output_extend
        {
            public ulong frame_id;
        }

        // ---------------- P/Invoke ----------------
        /// <summary>
        /// RKNN 库路径
        /// </summary> 
        private const string RKNN_LIB = "librknnrt.so"; // RK3568 Linux/Android

        [DllImport(RKNN_LIB, EntryPoint = "rknn_init")]
        public static extern int rknn_init(ref ulong context, IntPtr model, uint size, uint flag, ref rknn_init_extend extend);

        [DllImport(RKNN_LIB, EntryPoint = "rknn_destroy")]
        public static extern int rknn_destroy(ulong context);

        [DllImport(RKNN_LIB, EntryPoint = "rknn_query")]
        public static extern int rknn_query(ulong context, rknn_query_cmd cmd, IntPtr info, uint size);

        [DllImport(RKNN_LIB, EntryPoint = "rknn_inputs_set")]
        public static extern int rknn_inputs_set(ulong context, uint n_inputs, [In] rknn_input[] inputs);

        [DllImport(RKNN_LIB, EntryPoint = "rknn_run")]
        public static extern int rknn_run(ulong context, ref rknn_run_extend run_extend);

        [DllImport(RKNN_LIB, EntryPoint = "rknn_outputs_get")]
        public static extern int rknn_outputs_get(ulong context, uint n_outputs, [In, Out] rknn_output[] outputs, ref rknn_output_extend output_extend, int block);

        [DllImport(RKNN_LIB, EntryPoint = "rknn_outputs_release")]
        public static extern int rknn_outputs_release(ulong context, uint n_outputs, [In] rknn_output[] outputs);


        /// <summary>
        /// 根据 RKNN 返回码获取中文描述
        /// </summary>
        public static string GetErrorMessage(int code)
        {
            return code switch
            {
                0 => "执行成功",
                -1 => "执行出错",
                -2 => "执行超时",
                -3 => "NPU设备不可用",
                -4 => "内存分配失败",
                -5 => "传入参数错误",
                -6 => "传入的RKNN模型无效",
                -7 => "传入的rknn_context无效",
                -8 => "传入的rknn_input对象无效",
                -9 => "传入的rknn_output对象无效",
                -10 => "版本不匹配",
                -12 => "RKNN模型设置了优化等级的选项，但和当前驱动不兼容",
                -13 => "RKNN模型和当前平台不兼容",
                _ => $"未知错误码 ({code})",
            };
        }
    }

}
