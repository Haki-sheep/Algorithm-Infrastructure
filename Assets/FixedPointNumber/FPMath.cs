using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Photon.Deterministic
{
	/// <summary>
	/// 常用数学函数集合
	/// </summary>
	/// \ingroup MathAPI
	[StructLayout(LayoutKind.Sequential, Size = 1)]
	public struct FPMath
	{
		internal struct ExponentMantisaPair
		{
			public int Exponent;

			public int Mantissa;
		}

		internal const string LUT_NOT_LOADED_ERROR = "Math Lookup Tables (LUT) are not loaded and a trigonometric function was called. Call FPMathUtils.LoadLookupTables from Unity or manually call Init to load it.";

		/// <summary>
		/// 返回以下数值的符号<paramref name="value" />
		/// </summary>
		/// <param name="value"></param>
		/// <returns>1当正或零 -1当负</returns>
		public static FP Sign(FP value)
		{
			if (value.RawValue >= 0)
			{
				return FP._1;
			}
			return FP.Minus_1;
		}

		/// <summary>
		/// 如果<paramref name="value" />非零则返回其符号
		/// </summary>
		/// <param name="value"></param>
		/// <returns>1当正 0当零 -1当负</returns>
		public static FP SignZero(FP value)
		{
			if (value.RawValue < 0)
			{
				return FP.Minus_1;
			}
			if (value.RawValue > 0)
			{
				return FP._1;
			}
			return FP._0;
		}

		/// <summary>
		/// 返回以下数值的符号<paramref name="value" />
		/// </summary>
		/// <param name="value"></param>
		/// <returns>1当正或零 -1当负</returns>
		public static int SignInt(FP value)
		{
			if (value.RawValue >= 0)
			{
				return 1;
			}
			return -1;
		}

		/// <summary>
		/// 如果<paramref name="value" />非零则返回其符号
		/// </summary>
		/// <param name="value"></param>
		/// <returns>1当正 0当零 -1当负</returns>
		public static int SignZeroInt(FP value)
		{
			if (value.RawValue < 0)
			{
				return -1;
			}
			if (value.RawValue > 0)
			{
				return 1;
			}
			return 0;
		}

		/// <summary>
		/// 返回大于或等于参数的最小二次幂
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static int NextPowerOfTwo(int value)
		{
			if (value <= 0)
			{
				throw new InvalidOperationException("Number must be positive");
			}
			uint num = (uint)value;
			num--;
			num |= num >> 1;
			num |= num >> 2;
			num |= num >> 4;
			num |= num >> 8;
			num |= num >> 16;
			return (int)(num + 1);
		}

		/// <summary>
		/// 返回参数的绝对值
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP Abs(FP value)
		{
			long num = value.RawValue >> 63;
			value.RawValue = (value.RawValue + num) ^ num;
			return value;
		}

		/// <summary>
		/// 将 <paramref name="value" /> 舍入到最接近的整数
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FP Round(FP value)
		{
			long num = value.RawValue & 0xFFFF;
			FP fP = Floor(value);
			if (num < 32768)
			{
				return fP;
			}
			if (num > 32768)
			{
				return fP + FP._1;
			}
			if ((fP.RawValue & FP._1.RawValue) != 0L)
			{
				return fP + FP._1;
			}
			return fP;
		}

		/// <summary>
		/// 将 <paramref name="value" /> 舍入到最接近的整数
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static int RoundToInt(FP value)
		{
			if ((value.RawValue & 0xFFFF) >= FP._0_50.RawValue)
			{
				return (int)((value.RawValue >> 16) + 1);
			}
			return (int)(value.RawValue >> 16);
		}

		/// <summary>
		/// 返回小于或等于 <paramref name="value" /> 的最大整数
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FP Floor(FP value)
		{
			value.RawValue &= -65536L;
			return value;
		}

		/// <inheritdoc cref="M:Photon.Deterministic.FPMath.Floor(Photon.Deterministic.FP)" />
		public static long FloorRaw(long value)
		{
			return value & -65536;
		}

		/// <summary>
		/// 返回小于或等于 <paramref name="value" /> 的最大整数
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static int FloorToInt(FP value)
		{
			return (int)(value.RawValue >> 16);
		}

		/// <summary>
		/// 返回大于或等于 <paramref name="value" /> 的最小整数
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FP Ceiling(FP value)
		{
			if ((value.RawValue & 0xFFFF) != 0L)
			{
				value.RawValue = (value.RawValue & -65536) + 65536;
			}
			return value;
		}

		/// <summary>
		/// 返回大于或等于 <paramref name="value" /> 的最小整数
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static int CeilToInt(FP value)
		{
			if ((value.RawValue & 0xFFFF) >= 1)
			{
				return (int)((value.RawValue >> 16) + 1);
			}
			return (int)(value.RawValue >> 16);
		}

		/// <summary>
		/// 返回两个或更多值中的最大值
		/// </summary>
		/// <param name="val1"></param>
		/// <param name="val2"></param>
		/// <returns></returns>
		public static FP Max(FP val1, FP val2)
		{
			if (val1.RawValue <= val2.RawValue)
			{
				return val2;
			}
			return val1;
		}

		/// <summary>
		/// 返回两个或更多值中的最小值
		/// </summary>
		/// <param name="val1"></param>
		/// <param name="val2"></param>
		/// <returns></returns>
		public static FP Min(FP val1, FP val2)
		{
			if (val1.RawValue >= val2.RawValue)
			{
				return val2;
			}
			return val1;
		}

		/// <summary>
		/// 返回两个或更多值中的最小值
		/// </summary>
		/// <param name="numbers"></param>
		/// <returns></returns>
		public static FP Min(params FP[] numbers)
		{
			FP fP = numbers[0];
			for (int i = 1; i < numbers.Length; i++)
			{
				fP = Min(fP, numbers[i]);
			}
			return fP;
		}

		/// <summary>
		/// 返回最小值的三个值
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <param name="c"></param>
		public static FP Min(FP a, FP b, FP c)
		{
			if (a > b)
			{
				a = b;
			}
			if (a > c)
			{
				a = c;
			}
			return a;
		}

		/// <summary>
		/// 返回三个值中的最大值
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <param name="c"></param>
		/// <returns></returns>
		public static FP Max(FP a, FP b, FP c)
		{
			if (a < b)
			{
				a = b;
			}
			if (a < c)
			{
				a = c;
			}
			return a;
		}

		/// <summary>
		/// 返回两个或更多值中的最大值
		/// </summary>
		/// <param name="numbers"></param>
		/// <returns></returns>
		public static FP Max(params FP[] numbers)
		{
			FP fP = numbers[0];
			for (int i = 1; i < numbers.Length; i++)
			{
				fP = Max(fP, numbers[i]);
			}
			return fP;
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <param name="min"></param>
		/// <param name="max"></param>
		public static void MinMax(FP a, FP b, out FP min, out FP max)
		{
			if (a.RawValue < b.RawValue)
			{
				min = a;
				max = b;
			}
			else
			{
				min = b;
				max = a;
			}
		}

		/// <summary>
		/// 将给定值限制在给定最小值和最大值之间
		/// </summary>
		/// <param name="value"></param>
		/// <param name="min"></param>
		/// <param name="max"></param>
		/// <returns></returns>
		public static FP Clamp(FP value, FP min, FP max)
		{
			if (value.RawValue < min.RawValue)
			{
				return min;
			}
			if (value.RawValue > max.RawValue)
			{
				return max;
			}
			return value;
		}

		/// <summary>
		/// 将给定值限制在0和1
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FP Clamp01(FP value)
		{
			if (value.RawValue < 0)
			{
				return FP._0;
			}
			if (value.RawValue > 65536)
			{
				return FP._1;
			}
			return value;
		}

		/// <summary>
		/// 将给定值限制在给定最小值和最大值之间
		/// </summary>
		/// <param name="value"></param>
		/// <param name="min"></param>
		/// <param name="max"></param>
		/// <returns></returns>
		public static int Clamp(int value, int min, int max)
		{
			if (value < min)
			{
				return min;
			}
			if (value > max)
			{
				return max;
			}
			return value;
		}

		/// <summary>
		/// 将给定值限制在给定最小值和最大值之间
		/// </summary>
		/// <param name="value"></param>
		/// <param name="min"></param>
		/// <param name="max"></param>
		/// <returns></returns>
		public static long Clamp(long value, long min, long max)
		{
			if (value < min)
			{
				return min;
			}
			if (value > max)
			{
				return max;
			}
			return value;
		}

		/// <summary>
		/// 将给定值限制在<see cref="P:Photon.Deterministic.FP.UseableMin" />和<see cref="P:Photon.Deterministic.FP.UseableMax" />
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FP ClampUseable(FP value)
		{
			if (value.RawValue < int.MinValue)
			{
				return FP.FromRaw(-2147483648L);
			}
			if (value.RawValue > int.MaxValue)
			{
				return FP.FromRaw(2147483647L);
			}
			return value;
		}

		/// <summary>
		/// 返回参数的小数部分
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FP Fraction(FP value)
		{
			value.RawValue &= 65535L;
			return value;
		}

		/// <summary>
		/// 循环 <paramref name="t" /> 使其始终处于 0 到 <paramref name="length" /> 之间
		/// </summary>
		/// <param name="t"></param>
		/// <param name="length"></param>
		/// <returns></returns>
		public static FP Repeat(FP t, FP length)
		{
			FP result = default(FP);
			result.RawValue = RepeatRaw(t.RawValue, length.RawValue);
			return result;
		}

		internal static long RepeatRaw(long t, long length)
		{
			return t - (FloorRaw((t << 16) / length) * length + 32768 >> 16);
		}

		/// <summary>
		/// 根据 <paramref name="t" /> 在 <paramref name="start" /> 与 <paramref name="end" /> 之间进行线性插值
		/// 参数 <paramref name="t" /> 会限制在 [0 1] 范围内 用于计算 <paramref name="start" /> 与 <paramref name="end" /> 之间的差值
		/// 角度会转换到 [-Pi/2 Pi/2] 范围内
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static FP LerpRadians(FP start, FP end, FP t)
		{
			long num = RepeatRaw(end.RawValue - start.RawValue, FP.PiTimes2.RawValue);
			if (num > FP.Pi.RawValue)
			{
				num -= FP.PiTimes2.RawValue;
			}
			start.RawValue += num * Clamp01(t).RawValue + 32768 >> 16;
			return start;
		}

		/// <summary>
		/// 根据 <paramref name="t" /> 在 <paramref name="start" /> 与 <paramref name="end" /> 之间进行线性插值
		/// 参数 <paramref name="t" /> 会限制在 [0 1] 范围内
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static FP Lerp(FP start, FP end, FP t)
		{
			if (t.RawValue < 0)
			{
				t.RawValue = 0L;
			}
			if (t.RawValue > 65536)
			{
				t.RawValue = 65536L;
			}
			start.RawValue += (end.RawValue - start.RawValue) * t.RawValue + 32768 >> 16;
			return start;
		}

		/// <summary>
		/// 根据 <paramref name="t" /> 在 <paramref name="start" /> 与 <paramref name="end" /> 之间进行线性插值
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static FP LerpUnclamped(FP start, FP end, FP t)
		{
			start.RawValue += (end.RawValue - start.RawValue) * t.RawValue + 32768 >> 16;
			return start;
		}

		/// <summary>
		/// 计算使 <paramref name="start" /> 与 <paramref name="end" /> 插值得到 <paramref name="value" /> 的线性参数
		/// 结果会限制在 [0 1] 范围内
		/// <remarks><paramref name="start" /> 与 <paramref name="end" /> 相等时返回 0</remarks>
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FP InverseLerp(FP start, FP end, FP value)
		{
			if (start.RawValue == end.RawValue)
			{
				return default(FP);
			}
			value.RawValue = (value.RawValue - start.RawValue << 16) / (end.RawValue - start.RawValue);
			if (value.RawValue < 0)
			{
				value.RawValue = 0L;
			}
			if (value.RawValue > 65536)
			{
				value.RawValue = 65536L;
			}
			return value;
		}

		/// <summary>
		/// 计算使 <paramref name="start" /> 与 <paramref name="end" /> 插值得到 <paramref name="value" /> 的线性参数且不限制结果
		/// <remarks>结果系数不会限制在 [0 1] 范围内</remarks>
		/// <remarks><paramref name="start" /> 与 <paramref name="end" /> 相等时返回 0</remarks>
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FP InverseLerpUnclamped(FP start, FP end, FP value)
		{
			if (start.RawValue == end.RawValue)
			{
				return default(FP);
			}
			value.RawValue = (value.RawValue - start.RawValue << 16) / (end.RawValue - start.RawValue);
			return value;
		}

		/// <summary>
		/// 将 <paramref name="from" /> 向 <paramref name="to" /> 移动 每次最多移动 <paramref name="maxDelta" />
		/// 负的 <paramref name="maxDelta" /> 会使数值远离 <paramref name="to" />
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <param name="maxDelta"></param>
		/// <returns></returns>
		public static FP MoveTowards(FP from, FP to, FP maxDelta)
		{
			FP value = default(FP);
			value.RawValue = to.RawValue - from.RawValue;
			if (Abs(value).RawValue <= maxDelta.RawValue)
			{
				return to;
			}
			FP result = default(FP);
			if (value.RawValue < 0)
			{
				result.RawValue = from.RawValue - maxDelta.RawValue;
			}
			else
			{
				result.RawValue = from.RawValue + maxDelta.RawValue;
			}
			return result;
		}

		/// <summary>
		/// 在 <paramref name="start" /> 与 <paramref name="end" /> 之间进行平滑插值
		/// 等价于将切线设为 0 并将 <paramref name="t" /> 限制在 0 到 1 后调用 <see cref="M:Photon.Deterministic.FPMath.Hermite(Photon.Deterministic.FP,Photon.Deterministic.FP,Photon.Deterministic.FP,Photon.Deterministic.FP,Photon.Deterministic.FP)" />
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static FP SmoothStep(FP start, FP end, FP t)
		{
			return Hermite(start, FP._0, end, FP._0, Clamp01(t));
		}

		/// <summary>
		/// 返回 <paramref name="value" /> 的平方根
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="value"></param>
		/// <returns></returns>
		/// <exception cref="T:System.ArgumentOutOfRangeException">当 <paramref name="value" /> 小于 0 时抛出</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP Sqrt(FP value)
		{
			value.RawValue = SqrtRaw(value.RawValue);
			return value;
		}

		/// <summary>
		/// 返回平方根的<paramref name="x" />
		/// </summary>
		/// <param name="x">待平方的值</param>
		/// <returns></returns>
		/// <exception cref="T:System.ArgumentOutOfRangeException">数值不是正数</exception>
		public static long SqrtRaw(long x)
		{
			if (x <= 65536)
			{
				if (x < 0)
				{
					throw new ArgumentOutOfRangeException("x", $"The number has to be positive: {x}");
				}
				return FPLut.sqrt_aprox_lut[x] >> 6;
			}
			long num = x;
			int num2 = 0;
			if (num >> 32 != 0L)
			{
				num >>= 32;
				num2 += 32;
			}
			if (num >> 16 != 0L)
			{
				num >>= 16;
				num2 += 16;
			}
			if (num >> 8 != 0L)
			{
				num >>= 8;
				num2 += 8;
			}
			if (num >> 4 != 0L)
			{
				num >>= 4;
				num2 += 4;
			}
			if (num >> 2 != 0L)
			{
				num2 += 2;
			}
			int num3 = num2 - 16 + 2;
			int num4 = FPLut.sqrt_aprox_lut[x >> num3];
			num = (long)num4 << (num3 >> 1);
			return num >> 6;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static ExponentMantisaPair GetSqrtExponentMantissa(ulong x)
		{
			if (x <= 65536)
			{
				return new ExponentMantisaPair
				{
					Exponent = 0,
					Mantissa = FPLut.sqrt_aprox_lut[x]
				};
			}
			ulong num = x;
			int num2 = 0;
			if (num >> 32 != 0L)
			{
				num >>= 32;
				num2 += 32;
			}
			if (num >> 16 != 0L)
			{
				num >>= 16;
				num2 += 16;
			}
			if (num >> 8 != 0L)
			{
				num >>= 8;
				num2 += 8;
			}
			if (num >> 4 != 0L)
			{
				num >>= 4;
				num2 += 4;
			}
			if (num >> 2 != 0L)
			{
				num2 += 2;
			}
			int num3 = num2 - 16 + 2;
			return new ExponentMantisaPair
			{
				Exponent = num3 >> 1,
				Mantissa = FPLut.sqrt_aprox_lut[x >> num3]
			};
		}

		/// <summary>
		/// 执行重心插值
		/// </summary>
		/// <param name="value1"></param>
		/// <param name="value2"></param>
		/// <param name="value3"></param>
		/// <param name="t1"></param>
		/// <param name="t2"></param>
		/// <returns><paramref name="value1" />+ (<paramref name="value2" />-<paramref name="value1" />) *<paramref name="t1" />+ (<paramref name="value3" />-<paramref name="value1" />) *<paramref name="t2" />
		/// </returns>
		public static FP Barycentric(FP value1, FP value2, FP value3, FP t1, FP t2)
		{
			value1.RawValue = value1.RawValue + ((value2.RawValue - value1.RawValue) * t1.RawValue + 32768 >> 16) + ((value3.RawValue - value1.RawValue) * t2.RawValue + 32768 >> 16);
			return value1;
		}

		/// <summary>
		/// 执行Catmull-Rom插值
		/// </summary>
		/// <param name="value1"></param>
		/// <param name="value2"></param>
		/// <param name="value3"></param>
		/// <param name="value4"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static FP CatmullRom(FP value1, FP value2, FP value3, FP value4, FP t)
		{
			FP fP = default(FP);
			fP.RawValue = t.RawValue * t.RawValue + 32768 >> 16;
			FP fP2 = default(FP);
			fP2.RawValue = fP.RawValue * t.RawValue + 32768 >> 16;
			value1.RawValue = (value2.RawValue << 1) + ((value3.RawValue - value1.RawValue) * t.RawValue + 32768 >> 16) + (((value1.RawValue << 1) - (FP._5.RawValue * value2.RawValue + 32768 >> 16) + (value3.RawValue << 2) - value4.RawValue) * fP.RawValue + 32768 >> 16) + (((FP._3.RawValue * value2.RawValue + 32768 >> 16) - value1.RawValue - (FP._3.RawValue * value3.RawValue + 32768 >> 16) + value4.RawValue) * fP2.RawValue + 32768 >> 16) >> 1;
			return value1;
		}

		/// <summary>
		/// 执行三次Hermite插值
		/// </summary>
		/// <param name="value1"></param>
		/// <param name="tangent1"></param>
		/// <param name="value2"></param>
		/// <param name="tangent2"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static FP Hermite(FP value1, FP tangent1, FP value2, FP tangent2, FP t)
		{
			FP fP = default(FP);
			fP.RawValue = t.RawValue * t.RawValue + 32768 >> 16;
			FP fP2 = default(FP);
			fP2.RawValue = fP.RawValue * t.RawValue + 32768 >> 16;
			if (t == FP._0)
			{
				return value1;
			}
			if (t == FP._1)
			{
				return value2;
			}
			FP result = default(FP);
			result.RawValue = (((value1.RawValue << 1) - (value2.RawValue << 1) + tangent2.RawValue + tangent1.RawValue) * fP2.RawValue + 32768 >> 16) + (((FP._3.RawValue * value2.RawValue + 32768 >> 16) - (FP._3.RawValue * value1.RawValue + 32768 >> 16) - (tangent1.RawValue << 1) - tangent2.RawValue) * fP.RawValue + 32768 >> 16) + (tangent1.RawValue * t.RawValue + 32768 >> 16) + value1.RawValue;
			return result;
		}

		/// <summary>
		/// 执行取模运算且不强制结果与被除数同号 因此 Mod(-9 10) 等于 1
		/// </summary>
		/// <param name="a">被除数</param>
		/// <param name="n">除数</param>
		/// <returns>余数之后除法</returns>
		/// <exception cref="T:System.InvalidOperationException">当n大于Int64.MaxValue大于大于2或n小于Int64.MinValue大于大于2</exception>
		/// <exception cref="T:System.DivideByZeroException">当n == 0</exception>
		public static long ModuloClamped(long a, long n)
		{
			if (n > 2305843009213693951L)
			{
				throw new InvalidOperationException("N too big");
			}
			if (n < -2305843009213693952L)
			{
				throw new InvalidOperationException("N too small");
			}
			return (a % n + n) % n;
		}

		/// <summary>
		/// 执行取模运算且不强制结果与被除数同号 因此 Mod(-9 10) 等于 1
		/// </summary>
		/// <param name="a">被除数</param>
		/// <param name="n">除数</param>
		/// <returns>余数之后除法</returns>
		/// <exception cref="T:System.InvalidOperationException">当n大于<see cref="P:Photon.Deterministic.FP.UseableMax" />或n小于<see cref="P:Photon.Deterministic.FP.UseableMin" /></exception>
		/// <exception cref="T:System.DivideByZeroException">当n == 0</exception>
		public static FP ModuloClamped(FP a, FP n)
		{
			if (n > FP.UseableMax)
			{
				throw new InvalidOperationException("N too big");
			}
			if (n < FP.UseableMin)
			{
				throw new InvalidOperationException("N too small");
			}
			return new FP
			{
				RawValue = (a.RawValue % n.RawValue + n.RawValue) % n.RawValue
			};
		}

		/// <summary>
		/// 计算任意两个角度之间的最小有符号角度 例如从 -179 度到 179 度的逆时针旋转结果为 -2 度
		/// </summary>
		/// <param name="source">源角度在度</param>
		/// <param name="target">目标角度在度</param>
		/// <returns></returns>
		public static FP AngleBetweenDegrees(FP source, FP target)
		{
			long num = target.RawValue - source.RawValue;
			return new FP
			{
				RawValue = ModuloClamped(num + 11796480, 23592960L) - 11796480
			};
		}

		/// <summary>
		/// 与 AngleBetweenDegrees 相同 但使用 Raw 值优化
		/// </summary>
		/// <param name="source">源角度在度 (原始)</param>
		/// <param name="target">目标角度在度 (原始)</param>
		/// <returns></returns>
		public static long AngleBetweenDegreesRaw(long source, long target)
		{
			long num = target - source;
			return ModuloClamped(num + 11796480, 23592960L) - 11796480;
		}

		/// <summary>
		/// 计算任意两个角度之间的最小有符号角度
		/// </summary>
		/// <param name="source">源角度在弧度</param>
		/// <param name="target">目标角度在弧度</param>
		/// <returns></returns>
		public static FP AngleBetweenRadians(FP source, FP target)
		{
			long num = target.RawValue - source.RawValue;
			return new FP
			{
				RawValue = ModuloClamped(num + FP.Pi.RawValue, FP.PiTimes2.RawValue) - FP.Pi.RawValue
			};
		}

		/// <summary>
		/// 与 AngleBetweenDegrees 相同 但使用 Raw 值优化
		/// </summary>
		/// <param name="source">源角度在弧度 (原始)</param>
		/// <param name="target">目标角度在弧度 (原始)</param>
		/// <returns></returns>
		public static long AngleBetweenRadiansRaw(long source, long target)
		{
			long num = target - source;
			return ModuloClamped(num + FP.Pi.RawValue, FP.PiTimes2.RawValue) - FP.Pi.RawValue;
		}

		/// <summary>
		/// 返回 <paramref name="value" /> 以 2 为底的对数向下取整结果
		/// 更快比调用<see cref="M:Photon.Deterministic.FPMath.Log2(Photon.Deterministic.FP)" />和然后<see cref="M:Photon.Deterministic.FPMath.FloorToInt(Photon.Deterministic.FP)" />
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="value"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Log2FloorToInt(FP value)
		{
			return Log2FloorToIntRaw(value.RawValue);
		}

		/// <summary>
		/// 快速返回 <paramref name="value" /> 以 2 为底的对数向上取整结果
		/// 快于调用<see cref="M:Photon.Deterministic.FPMath.Log2(Photon.Deterministic.FP)" />再调用<see cref="M:Photon.Deterministic.FPMath.CeilToInt(Photon.Deterministic.FP)" />
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="value"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Log2CeilingToInt(FP value)
		{
			int num = 0;
			if ((value.RawValue & (value.RawValue - 1)) != 0L)
			{
				num = 1;
			}
			return Log2FloorToIntRaw(value.RawValue) + num;
		}

		/// <summary>
		/// 返回 <paramref name="value" /> 以 2 为底的对数
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FP Log2(FP value)
		{
			value.RawValue = Log2RawAdditionalPrecision(value.RawValue) >> 15;
			return value;
		}

		/// <summary>
		/// 返回 <paramref name="value" /> 的自然对数
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FP Ln(FP value)
		{
			value.RawValue = Log2RawAdditionalPrecision(value.RawValue);
			value.RawValue >>= 6;
			value.RawValue = FPHighPrecisionDivisor.RawDiv(value.RawValue, 6196328018L);
			value.RawValue >>= 9;
			return value;
		}

		/// <summary>
		/// 返回 <paramref name="value" /> 以 10 为底的对数
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FP Log10(FP value)
		{
			value.RawValue = Log2RawAdditionalPrecision(value.RawValue);
			value.RawValue >>= 6;
			value.RawValue = FPHighPrecisionDivisor.RawDiv(value.RawValue, 14267572527L);
			value.RawValue >>= 9;
			return value;
		}

		/// <summary>
		/// 返回<paramref name="value" />以<paramref name="logBase" />为底的对数
		/// 当<paramref name="logBase" />为2 10或e时使用Log2 Log10和Ln性能更高且精度更好
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="value"></param>
		/// <param name="logBase"></param>
		/// <returns></returns>
		public static FP Log(FP value, FP logBase)
		{
			value.RawValue = Log2RawAdditionalPrecision(value.RawValue);
			logBase.RawValue = Log2RawAdditionalPrecision(logBase.RawValue);
			value.RawValue = (value.RawValue << 16) / logBase.RawValue;
			return value;
		}

		private static int Log2FloorToIntRaw(long x)
		{
			if (x <= 0)
			{
				throw new ArgumentOutOfRangeException("x", "The number has to be positive");
			}
			long num = x;
			int num2 = 0;
			if (num >> 32 != 0L)
			{
				num >>= 32;
				num2 += 32;
			}
			if (num >> 16 != 0L)
			{
				num >>= 16;
				num2 += 16;
			}
			if (num >> 8 != 0L)
			{
				num >>= 8;
				num2 += 8;
			}
			if (num >> 4 != 0L)
			{
				num >>= 4;
				num2 += 4;
			}
			if (num >> 2 != 0L)
			{
				num >>= 2;
				num2 += 2;
			}
			if (num >> 1 != 0L)
			{
				num2++;
			}
			return num2 - 16;
		}

		private static long Log2RawAdditionalPrecision(long x)
		{
			uint[] log2_approx_lut = FPLut.log2_approx_lut;
			int num = Log2FloorToIntRaw(x);
			uint num2 = (uint)((int)x << 48 - num);
			uint num3 = num2 >> 26;
			uint num4 = log2_approx_lut[num3 + 1] - log2_approx_lut[num3];
			uint num5 = log2_approx_lut[num3 + 2] - log2_approx_lut[num3];
			int num6 = (int)((num5 >> 1) - num4);
			int num7 = (int)((num4 << 1) - (num5 >> 1));
			uint num8 = num2 & 0x3FFFFFF;
			int num9 = (int)(((num6 * num8 >> 26) + num7) * num8 >> 26);
			uint num10 = (uint)(log2_approx_lut[num3] + num9);
			num10 += 16384;
			long num11 = (long)num << 31;
			return num11 + num10;
		}

		/// <summary>
		/// 返回 e 的指定次幂 在范围 [-6 32] 内最大相对误差约为 0.3%
		/// </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static FP Exp(FP x)
		{
			long num = x.RawValue >> 16;
			long num2 = x.RawValue & 0xFFFF;
			if (num2 >= 32768)
			{
				num2 -= 65536;
				num++;
			}
			if (num < -30)
			{
				return 0;
			}
			if (num >= 33)
			{
				return FP.MaxValue;
			}
			long num3 = 65536L;
			num3 += num2;
			num3 += num2 * num2 / 2 >> 16;
			num3 += num2 * num2 * num2 / 6 >> 32;
			num3 += num2 * num2 * num2 * num2 / 24 >> 48;
			long num4 = FPLut.exp_integral_lut[30 + num];
			FP result = default(FP);
			if (num < 0)
			{
				long num5 = num3 * num4;
				result.RawValue = num5 + 2199023255552L >> 42;
			}
			else if (num > 20)
			{
				result.RawValue = num3 * num4;
			}
			else
			{
				result.RawValue = num3 * num4 >> 16;
			}
			return result;
		}

		/// <summary>
		/// 返回正弦的角度<paramref name="rad" />
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="rad">角度在弧度</param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP Sin(FP rad)
		{
			long rawValue = rad.RawValue;
			long num = rawValue >> 63;
			rawValue = (rawValue + num) ^ num;
			rawValue %= 411775;
			long num2 = FPLut.sin_cos_lut[rawValue];
			rawValue = ((int)(num2 & 0xFFFFFFFFu) + num) ^ num;
			rad.RawValue = rawValue;
			return rad;
		}

		/// <summary>
		/// 返回高精度正弦的角度<paramref name="rad" />
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="rad">角度在弧度</param>
		/// <returns></returns>
		public static FP SinHighPrecision(FP rad)
		{
			long rawValue = rad.RawValue;
			long num = rawValue >> 63;
			rawValue = (rawValue + num) ^ num;
			rawValue <<= 16;
			rawValue %= 26986075409L;
			rawValue >>= 16;
			long num2 = FPLut.sin_cos_lut[rawValue];
			rawValue = ((int)(num2 & 0xFFFFFFFFu) + num) ^ num;
			rad.RawValue = rawValue;
			return rad;
		}

		/// <summary>
		/// 返回余弦的角度<paramref name="rad" />
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="rad">角度在弧度</param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP Cos(FP rad)
		{
			long rawValue = rad.RawValue;
			long num = rawValue >> 63;
			rawValue = (rawValue + num) ^ num;
			rawValue %= 411775;
			long num2 = FPLut.sin_cos_lut[rawValue];
			rawValue = num2 >> 32;
			rad.RawValue = rawValue;
			return rad;
		}

		/// <summary>
		/// 返回高精度余弦的角度<paramref name="rad" />
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="rad">角度在弧度</param>
		/// <returns></returns>
		public static FP CosHighPrecision(FP rad)
		{
			long rawValue = rad.RawValue;
			long num = rawValue >> 63;
			rawValue = (rawValue + num) ^ num;
			rawValue <<= 16;
			rawValue %= 26986075409L;
			rawValue >>= 16;
			long num2 = FPLut.sin_cos_lut[rawValue];
			rawValue = num2 >> 32;
			rad.RawValue = rawValue;
			return rad;
		}

		/// <summary>
		/// 计算正弦和余弦的角度<paramref name="rad" />它是更快比
		/// 调用<see cref="M:Photon.Deterministic.FPMath.Sin(Photon.Deterministic.FP)" />和<see cref="M:Photon.Deterministic.FPMath.Cos(Photon.Deterministic.FP)" />分别
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="rad">角度在弧度</param>
		/// <param name="sin"></param>
		/// <param name="cos"></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SinCos(FP rad, out FP sin, out FP cos)
		{
			long rawValue = rad.RawValue;
			long num = rawValue >> 63;
			rawValue = (rawValue + num) ^ num;
			rawValue %= 411775;
			long num2 = FPLut.sin_cos_lut[rawValue];
			cos.RawValue = num2 >> 32;
			sin.RawValue = ((int)(num2 & 0xFFFFFFFFu) + num) ^ num;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SinCosRaw(FP rad, out long sinRaw, out long cosRaw)
		{
			long rawValue = rad.RawValue;
			long num = rawValue >> 63;
			rawValue = (rawValue + num) ^ num;
			rawValue %= 411775;
			long num2 = FPLut.sin_cos_lut[rawValue];
			cosRaw = num2 >> 32;
			sinRaw = ((int)(num2 & 0xFFFFFFFFu) + num) ^ num;
		}

		/// <summary>
		/// 计算高精度正弦和余弦的角度<paramref name="rad" />它是更快比
		/// 调用<see cref="M:Photon.Deterministic.FPMath.SinHighPrecision(Photon.Deterministic.FP)" />和<see cref="M:Photon.Deterministic.FPMath.CosHighPrecision(Photon.Deterministic.FP)" />分别
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="rad">角度在弧度</param>
		/// <param name="sin"></param>
		/// <param name="cos"></param>
		public static void SinCosHighPrecision(FP rad, out FP sin, out FP cos)
		{
			long rawValue = rad.RawValue;
			long num = rawValue >> 63;
			rawValue = (rawValue + num) ^ num;
			rawValue <<= 16;
			rawValue %= 26986075409L;
			rawValue >>= 16;
			long num2 = FPLut.sin_cos_lut[rawValue];
			cos.RawValue = num2 >> 32;
			sin.RawValue = ((int)(num2 & 0xFFFFFFFFu) + num) ^ num;
		}

		/// <summary>
		/// 返回正切的角度<paramref name="rad" />
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="rad">角度在弧度</param>
		/// <returns></returns>
		public static FP Tan(FP rad)
		{
			if (rad.RawValue < -205887)
			{
				rad.RawValue %= -205887L;
			}
			if (rad.RawValue > 205887)
			{
				rad.RawValue %= 205887L;
			}
			rad.RawValue = FPLut.tan_lut[rad.RawValue + 205887];
			return rad;
		}

		/// <summary>
		/// 返回 <paramref name="value" /> 的反正弦值 单位为弧度
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FP Asin(FP value)
		{
			if (value.RawValue < -65536 || value.RawValue > 65536)
			{
				return FP.MinValue;
			}
			value.RawValue = FPLut.asin_lut[value.RawValue + 65536];
			return value;
		}

		/// <summary>
		/// 返回 <paramref name="value" /> 的反余弦值 单位为弧度
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FP Acos(FP value)
		{
			if (value.RawValue < -65536 || value.RawValue > 65536)
			{
				return FP.MinValue;
			}
			value.RawValue = FPLut.acos_lut[value.RawValue + 65536];
			return value;
		}

		/// <summary>
		/// 返回 <paramref name="value" /> 的反正切值 单位为弧度
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FP Atan(FP value)
		{
			long num = value.RawValue >> 63;
			value.RawValue = (value.RawValue + num) ^ num;
			if (value.RawValue <= 393216)
			{
				value.RawValue = (FPLut.atan_lut[value.RawValue] + num) ^ num;
				return value;
			}
			if (value.RawValue <= 16384000)
			{
				value.RawValue = value.RawValue - 393216 >> 12;
				value.RawValue = (FPLut.atan_lut[393216 + value.RawValue] + num) ^ num;
				return value;
			}
			if (value.RawValue <= 655360000)
			{
				value.RawValue = value.RawValue - 16384000 >> 20;
				value.RawValue = (FPLut.atan_lut[397120 + value.RawValue] + num) ^ num;
				return value;
			}
			value.RawValue = (FPLut.atan_lut[FPLut.atan_lut.Length - 1] + num) ^ num;
			return value;
		}

		/// <summary>
		/// 返回正切值为 <paramref name="y" /> 除以 <paramref name="x" /> 的弧度角 即使 x 为零也能返回正确角度
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="y"></param>
		/// <param name="x"></param>
		/// <returns></returns>
		public static FP Atan2(FP y, FP x)
		{
			if (x.RawValue > 0)
			{
				return Atan(y / x);
			}
			if (x.RawValue < 0)
			{
				if (y.RawValue >= 0)
				{
					y.RawValue = Atan(y / x).RawValue + 205887;
				}
				else
				{
					y.RawValue = Atan(y / x).RawValue - 205887;
				}
				return y;
			}
			if (y.RawValue > 0)
			{
				return FP.PiOver2;
			}
			if (y.RawValue == 0L)
			{
				return FP._0;
			}
			return -FP.PiOver2;
		}
	}
}

