using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Photon.Deterministic
{
	public static class Maths
	{
		[StructLayout(LayoutKind.Explicit)]
		public struct FastAbs
		{
			public const uint Mask = 2147483647u;

			[FieldOffset(0)]
			public uint UInt32;

			[FieldOffset(0)]
			public float Single;
		}

		private static byte[] _debruijnTable32 = new byte[32]
		{
			0, 9, 1, 10, 13, 21, 2, 29, 11, 14,
			16, 18, 22, 25, 3, 30, 8, 12, 20, 28,
			15, 17, 24, 7, 19, 27, 23, 6, 26, 5,
			4, 31
		};

		private static byte[] _debruijnTable64 = new byte[64]
		{
			63, 0, 58, 1, 59, 47, 53, 2, 60, 39,
			48, 27, 54, 33, 42, 3, 61, 51, 37, 40,
			49, 18, 28, 20, 55, 30, 34, 11, 43, 14,
			22, 4, 62, 57, 46, 52, 38, 26, 32, 41,
			50, 36, 17, 19, 29, 10, 13, 21, 56, 45,
			25, 31, 35, 16, 9, 12, 44, 24, 15, 8,
			23, 7, 6, 5
		};

		private static readonly int[] DeBruijnLookupLong = new int[128]
		{
			0, 48, -1, -1, 31, -1, 15, 51, -1, 63,
			5, -1, -1, -1, 19, -1, 23, 28, -1, -1,
			-1, 40, 36, 46, -1, 13, -1, -1, -1, 34,
			-1, 58, -1, 60, 2, 43, 55, -1, -1, -1,
			50, 62, 4, -1, 18, 27, -1, 39, 45, -1,
			-1, 33, 57, -1, 1, 54, -1, 49, -1, 17,
			-1, -1, 32, -1, 53, -1, 16, -1, -1, 52,
			-1, -1, -1, 64, 6, 7, 8, -1, 9, -1,
			-1, -1, 20, 10, -1, -1, 24, -1, 29, -1,
			-1, 21, -1, 11, -1, -1, 41, -1, 25, 37,
			-1, 47, -1, 30, 14, -1, -1, -1, -1, 22,
			-1, -1, 35, 12, -1, -1, -1, 59, 42, -1,
			-1, 61, 3, 26, 38, 44, -1, 56
		};

		/// <summary>
		/// 返回类型占用的位数
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static int SizeOfBits<T>() where T : unmanaged
		{
			return sizeof(T) * 8;
		}

		/// <summary>
		/// 返回存储给定位数所需的字节数
		/// </summary>
		public static int BytesRequiredForBits(int b)
		{
			return b + 7 >> 3;
		}

		/// <summary>
		/// 返回存储给定位数所需的整数数量
		/// </summary>
		public static int IntsRequiredForBits(int b)
		{
			return b + 31 >> 5;
		}

		/// <summary>
		/// 返回存储给定位数所需的字节数
		/// </summary>
		public static short BytesRequiredForBits(short b)
		{
			return (short)(b + 7 >> 3);
		}

		/// <summary>
		/// 返回给定数据的位字符串表示
		/// </summary>
		public unsafe static string PrintBits(byte* data, int count)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("[lo ");
			for (int i = 0; i < count; i++)
			{
				byte b = data[i];
				for (int j = 0; j < 8; j++)
				{
					stringBuilder.Append(((b & (1 << j)) == 1 << j) ? '1' : '0');
				}
				if (i + 1 < count)
				{
					stringBuilder.Append(" ");
				}
			}
			stringBuilder.Append(" hi]");
			return stringBuilder.ToString();
		}

		/// <summary>
		/// 返回表示给定<paramref name="n" />所需的最少位数
		/// </summary>
		public static int BitsRequiredForNumber(int n)
		{
			for (int num = 31; num >= 0; num--)
			{
				int num2 = 1 << num;
				if ((n & num2) == num2)
				{
					return num + 1;
				}
			}
			return 0;
		}

		/// <summary>
		/// 返回小于或等于 <paramref name="value" /> 的最大整数
		/// </summary>
		public static int FloorToInt(double value)
		{
			return (int)Math.Floor(value);
		}

		/// <summary>
		/// 返回大于或等于 <paramref name="value" /> 的最小整数
		/// </summary>
		public static int CeilToInt(double value)
		{
			return (int)Math.Ceiling(value);
		}

		public static int CountUsedBitsMinOne(uint value)
		{
			int num = 0;
			do
			{
				num++;
				value >>= 1;
			}
			while (value != 0);
			return num;
		}

		/// <summary>
		/// 返回表示给定uint<paramref name="n" />所需的最少位数
		/// </summary>
		public static int BitsRequiredForNumber(uint n)
		{
			for (int num = 31; num >= 0; num--)
			{
				int num2 = 1 << num;
				if ((n & num2) == num2)
				{
					return num + 1;
				}
			}
			return 0;
		}

		public static uint NextPowerOfTwo(uint v)
		{
			v--;
			v |= v >> 1;
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;
			v++;
			return v;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CountSetBits(ulong x)
		{
			x -= (x >> 1) & 0x5555555555555555L;
			x = (x & 0x3333333333333333L) + ((x >> 2) & 0x3333333333333333L);
			x = (x + (x >> 4)) & 0xF0F0F0F0F0F0F0FL;
			return (int)(x * 72340172838076673L >> 56);
		}

		/// <summary>
		/// 将给定毫秒转换为秒
		/// </summary>
		public static double MillisecondsToSeconds(double ms)
		{
			return ms / 1000.0;
		}

		/// <summary>
		/// 将给定秒转换为毫秒
		/// </summary>
		public static long SecondsToMilliseconds(double seconds)
		{
			return (long)(seconds * 1000.0);
		}

		/// <summary>
		/// 将给定秒转换为微秒
		/// </summary>
		public static long SecondsToMicroseconds(double seconds)
		{
			return (long)(seconds * 1000000.0);
		}

		/// <summary>
		/// 将给定微秒转换为秒
		/// </summary>
		public static double MicrosecondsToSeconds(long microseconds)
		{
			return (double)microseconds / 1000000.0;
		}

		/// <summary>
		/// 将给定微秒转换为毫秒
		/// </summary>
		public static long MillisecondsToMicroseconds(long milliseconds)
		{
			return milliseconds * 1000;
		}

		public static double CosineInterpolate(double a, double b, double t)
		{
			double num = (1.0 - Math.Cos(t * Math.PI)) * 0.5;
			return a * (1.0 - num) + b * num;
		}

		public static byte ClampToByte(int v)
		{
			if (v < 0)
			{
				return 0;
			}
			if (v > 255)
			{
				return byte.MaxValue;
			}
			return (byte)v;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ZigZagEncode(int i)
		{
			return (i >> 31) ^ (i << 1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ZigZagDecode(int i)
		{
			return (i >> 1) ^ -(i & 1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long ZigZagEncode(long i)
		{
			return (i >> 63) ^ (i << 1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long ZigZagDecode(long i)
		{
			return (i >> 1) ^ -(i & 1);
		}

		/// <summary>
		/// 将给定值限制在给定范围内
		/// </summary>
		/// <param name="v">要限制的值</param>
		/// <param name="min">最小值</param>
		/// <param name="max">最大值</param>
		public static int Clamp(int v, int min, int max)
		{
			if (v < min)
			{
				return min;
			}
			if (v > max)
			{
				return max;
			}
			return v;
		}

		/// <inheritdoc cref="M:Photon.Deterministic.Maths.Clamp(System.Int32,System.Int32,System.Int32)" />
		public static uint Clamp(uint v, uint min, uint max)
		{
			if (v < min)
			{
				return min;
			}
			if (v > max)
			{
				return max;
			}
			return v;
		}

		/// <inheritdoc cref="M:Photon.Deterministic.Maths.Clamp(System.Int32,System.Int32,System.Int32)" />
		public static double Clamp(double v, double min, double max)
		{
			if (v < min)
			{
				return min;
			}
			if (v > max)
			{
				return max;
			}
			return v;
		}

		/// <inheritdoc cref="M:Photon.Deterministic.Maths.Clamp(System.Int32,System.Int32,System.Int32)" />
		public static float Clamp(float v, float min, float max)
		{
			if (v < min)
			{
				return min;
			}
			if (v > max)
			{
				return max;
			}
			return v;
		}

		/// <summary>
		/// 将给定值限制在 [0, 1] 范围内
		/// </summary>
		public static double Clamp01(double v)
		{
			if (v < 0.0)
			{
				return 0.0;
			}
			if (v > 1.0)
			{
				return 1.0;
			}
			return v;
		}

		/// <inheritdoc cref="M:Photon.Deterministic.Maths.Clamp01(System.Double)" />
		public static float Clamp01(float v)
		{
			if (v < 0f)
			{
				return 0f;
			}
			if (v > 1f)
			{
				return 1f;
			}
			return v;
		}

		/// <summary>
		/// 根据 <paramref name="t" /> 在 <paramref name="a" /> 与 <paramref name="b" /> 之间进行线性插值
		/// </summary>
		public static float Lerp(float a, float b, float t)
		{
			return a + (b - a) * Clamp01(t);
		}

		/// <inheritdoc cref="M:Photon.Deterministic.Maths.Lerp(System.Single,System.Single,System.Single)" />
		public static double Lerp(double a, double b, double t)
		{
			return a + (b - a) * Clamp01(t);
		}

		/// <summary>
		/// 返回 <paramref name="v" /> 与 <paramref name="max" /> 中的较小值
		/// </summary>
		public static uint Min(uint v, uint max)
		{
			if (v > max)
			{
				return max;
			}
			return v;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int BitScanReverse(int v)
		{
			return BitScanReverse((uint)v);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int BitScanReverse(uint v)
		{
			v |= v >> 1;
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;
			return _debruijnTable32[v * 130329821 >> 27];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int BitScanReverse(long v)
		{
			return BitScanReverse((ulong)v);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int BitScanReverse(ulong v)
		{
			v |= v >> 1;
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;
			v |= v >> 32;
			return DeBruijnLookupLong[v * 7783611145303519083L >> 57];
		}
	}
}

