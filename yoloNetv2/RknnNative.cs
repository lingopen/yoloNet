using System;
using System.Runtime.InteropServices;
 
namespace yoloNetv2
{
    public static class RknnNative
    {
        /// <summary>
        /// RKNN 库路径
        /// </summary>
        public const string LIB_RKNN = "/usr/lib/librknnrt.so";

        // ================== Error Codes ==================
        public const int RKNN_SUCC = 0;
        public const int RKNN_ERR_FAIL = -1;
        public const int RKNN_ERR_TIMEOUT = -2;
        public const int RKNN_ERR_DEVICE_UNAVAILABLE = -3;
        public const int RKNN_ERR_MALLOC_FAIL = -4;
        public const int RKNN_ERR_PARAM_INVALID = -5;
        public const int RKNN_ERR_MODEL_INVALID = -6;
        public const int RKNN_ERR_CTX_INVALID = -7;
        public const int RKNN_ERR_INPUT_INVALID = -8;
        public const int RKNN_ERR_OUTPUT_INVALID = -9;
        public const int RKNN_ERR_DEVICE_UNMATCH = -10;

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

        // ================== Tensor Type ==================
        public enum rknn_tensor_type
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
            RKNN_TENSOR_BFLOAT16
        }

        public enum rknn_tensor_qnt_type
        {
            RKNN_TENSOR_QNT_NONE = 0,
            RKNN_TENSOR_QNT_DFP,
            RKNN_TENSOR_QNT_AFFINE_ASYMMETRIC
        }

        public enum rknn_tensor_format
        {
            RKNN_TENSOR_NCHW = 0,
            RKNN_TENSOR_NHWC,
            RKNN_TENSOR_NC1HWC2,
            RKNN_TENSOR_UNDEFINED
        }

        // ================== Tensor Attribute ==================
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

        // ================== Input / Output ==================
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

        // ================== Query ==================
        public enum rknn_query_cmd : uint
        {
            RKNN_QUERY_IN_OUT_NUM = 0,
            RKNN_QUERY_INPUT_ATTR,
            RKNN_QUERY_OUTPUT_ATTR,
            RKNN_QUERY_PLATFORM,
            RKNN_QUERY_QUANTIZE_AWARE,
            RKNN_QUERY_PERF_PROFILE
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct rknn_input_output_num
        {
            public uint n_input;
            public uint n_output;
        }

        // ================== Core Functions ==================
        [DllImport(LIB_RKNN, EntryPoint = "rknn_init", CallingConvention = CallingConvention.Cdecl)]
        public static extern int rknn_init(ref IntPtr context, IntPtr model, uint size, uint flag, IntPtr extend);

        [DllImport(LIB_RKNN, EntryPoint = "rknn_destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern int rknn_destroy(IntPtr context);

        [DllImport(LIB_RKNN, EntryPoint = "rknn_query", CallingConvention = CallingConvention.Cdecl)]
        public static extern int rknn_query(IntPtr context, uint cmd, IntPtr info, uint size);

        [DllImport(LIB_RKNN, EntryPoint = "rknn_inputs_set", CallingConvention = CallingConvention.Cdecl)]
        public static extern int rknn_inputs_set(IntPtr context, uint n_inputs, [In] rknn_input[] inputs);

        [DllImport(LIB_RKNN, EntryPoint = "rknn_outputs_get", CallingConvention = CallingConvention.Cdecl)]
        public static extern int rknn_outputs_get(IntPtr context, uint n_outputs, [In, Out] rknn_output[] outputs, uint timeout);

        [DllImport(LIB_RKNN, EntryPoint = "rknn_outputs_release", CallingConvention = CallingConvention.Cdecl)]
        public static extern int rknn_outputs_release(IntPtr context, uint n_outputs, [In] rknn_output[] outputs);
    }
}
