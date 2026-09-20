using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Photon.Deterministic
{
	/// <summary>
	/// 表示一个3D向量
	/// </summary>
	/// \ingroup MathAPI
	[Serializable]
	[StructLayout(LayoutKind.Explicit)]
	public struct FPVector3 : IEquatable<FPVector3>
	{
		/// <summary>
		/// 用于比较 FPVector3 对象是否相等
		/// </summary>
		public class EqualityComparer : IEqualityComparer<FPVector3>
		{
			public static readonly EqualityComparer Instance = new EqualityComparer();

			private EqualityComparer()
			{
			}

			bool IEqualityComparer<FPVector3>.Equals(FPVector3 x, FPVector3 y)
			{
				return x == y;
			}

			int IEqualityComparer<FPVector3>.GetHashCode(FPVector3 obj)
			{
				return obj.GetHashCode();
			}
		}

		/// <summary>
		/// 该向量占用的内存大小 即 3 个 FP 值
		/// </summary>
		public const int SIZE = 24;

		/// <summary>向量的 x 分量</summary>
		[FieldOffset(0)]
		public FP X;

		/// <summary>向量的 y 分量</summary>
		[FieldOffset(8)]
		public FP Y;

		/// <summary>向量的 z 分量</summary>
		[FieldOffset(16)]
		public FP Z;

		/// <summary>
		/// 分量为 (0,0,0)
		/// </summary>
		public static FPVector3 Zero => default(FPVector3);

		/// <summary>
		/// 分量为 (-1,0,0)
		/// </summary>
		public static FPVector3 Left => new FPVector3
		{
			X = 
			{
				RawValue = -65536L
			}
		};

		/// <summary>
		/// 分量为 (1,0,0)
		/// </summary>
		public static FPVector3 Right => new FPVector3
		{
			X = 
			{
				RawValue = 65536L
			}
		};

		/// <summary>
		/// 分量为 (0,1,0)
		/// </summary>
		public static FPVector3 Up => new FPVector3
		{
			Y = 
			{
				RawValue = 65536L
			}
		};

		/// <summary>
		/// 分量为 (0,-1,0)
		/// </summary>
		public static FPVector3 Down => new FPVector3
		{
			Y = 
			{
				RawValue = -65536L
			}
		};

		/// <summary>
		/// 分量为 (0,0,-1)
		/// </summary>
		public static FPVector3 Back => new FPVector3
		{
			Z = 
			{
				RawValue = -65536L
			}
		};

		/// <summary>
		/// 分量为 (0,0,1)
		/// </summary>
		public static FPVector3 Forward => new FPVector3
		{
			Z = 
			{
				RawValue = 65536L
			}
		};

		/// <summary>
		/// 分量为 (1,1,1)
		/// </summary>
		public static FPVector3 One => new FPVector3
		{
			X = 
			{
				RawValue = 65536L
			},
			Y = 
			{
				RawValue = 65536L
			},
			Z = 
			{
				RawValue = 65536L
			}
		};

		/// <summary>
		/// 分量为
		/// (FP MinValue FP MinValue FP MinValue)
		/// </summary>
		public static FPVector3 MinValue => new FPVector3
		{
			X = 
			{
				RawValue = long.MinValue
			},
			Y = 
			{
				RawValue = long.MinValue
			},
			Z = 
			{
				RawValue = long.MinValue
			}
		};

		/// <summary>
		/// 分量为
		/// (FP MaxValue FP MaxValue FP MaxValue)
		/// </summary>
		public static FPVector3 MaxValue => new FPVector3
		{
			X = 
			{
				RawValue = long.MaxValue
			},
			Y = 
			{
				RawValue = long.MaxValue
			},
			Z = 
			{
				RawValue = long.MaxValue
			}
		};

		/// <summary>
		/// 分量为
		/// (FP UseableMin FP UseableMin FP UseableMin)
		/// </summary>
		public static FPVector3 UseableMin => new FPVector3
		{
			X = 
			{
				RawValue = -2147483648L
			},
			Y = 
			{
				RawValue = -2147483648L
			},
			Z = 
			{
				RawValue = -2147483648L
			}
		};

		/// <summary>
		/// 分量为
		/// (FP UseableMax FP UseableMax FP UseableMax)
		/// </summary>
		public static FPVector3 UseableMax => new FPVector3
		{
			X = 
			{
				RawValue = 2147483647L
			},
			Y = 
			{
				RawValue = 2147483647L
			},
			Z = 
			{
				RawValue = 2147483647L
			}
		};

		/// <summary>
		/// 获取向量长度的平方
		/// </summary>
		/// <returns>返回向量长度的平方</returns>
		public readonly FP SqrMagnitude
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FP result = default(FP);
				result.RawValue = (X.RawValue * X.RawValue + 32768 >> 16) + (Y.RawValue * Y.RawValue + 32768 >> 16) + (Z.RawValue * Z.RawValue + 32768 >> 16);
				return result;
			}
		}

		/// <summary>
		/// 获取向量长度
		/// </summary>
		/// <returns>返回向量长度</returns>
		public readonly FP Magnitude
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return FPMath.Sqrt(SqrMagnitude);
			}
		}

		/// <summary>
		/// 获取向量的归一化结果
		/// </summary>
		/// <returns>向量的归一化结果</returns>
		public readonly FPVector3 Normalized
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return Normalize(this);
			}
		}

		public readonly FPVector3 XXX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = X;
				result.Y = X;
				result.Z = X;
				return result;
			}
		}

		public readonly FPVector3 XXY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = X;
				result.Y = X;
				result.Z = Y;
				return result;
			}
		}

		public readonly FPVector3 XXZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = X;
				result.Y = X;
				result.Z = Z;
				return result;
			}
		}

		public readonly FPVector3 XYX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = X;
				result.Y = Y;
				result.Z = X;
				return result;
			}
		}

		public readonly FPVector3 XYY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = X;
				result.Y = Y;
				result.Z = Y;
				return result;
			}
		}

		public readonly FPVector3 XYZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = X;
				result.Y = Y;
				result.Z = Z;
				return result;
			}
		}

		public readonly FPVector3 XZX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = X;
				result.Y = Z;
				result.Z = X;
				return result;
			}
		}

		public readonly FPVector3 XZY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = X;
				result.Y = Z;
				result.Z = Y;
				return result;
			}
		}

		public readonly FPVector3 XZZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = X;
				result.Y = Z;
				result.Z = Z;
				return result;
			}
		}

		public readonly FPVector2 XX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector2 result = default(FPVector2);
				result.X = X;
				result.Y = X;
				return result;
			}
		}

		public readonly FPVector2 XY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector2 result = default(FPVector2);
				result.X = X;
				result.Y = Y;
				return result;
			}
		}

		public readonly FPVector2 XZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector2 result = default(FPVector2);
				result.X = X;
				result.Y = Z;
				return result;
			}
		}

		public readonly FPVector3 YYY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Y;
				result.Y = Y;
				result.Z = Y;
				return result;
			}
		}

		public readonly FPVector3 YYZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Y;
				result.Y = Y;
				result.Z = Z;
				return result;
			}
		}

		public readonly FPVector3 YYX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Y;
				result.Y = Y;
				result.Z = X;
				return result;
			}
		}

		public readonly FPVector3 YZY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Y;
				result.Y = Z;
				result.Z = Y;
				return result;
			}
		}

		public readonly FPVector3 YZZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Y;
				result.Y = Z;
				result.Z = Z;
				return result;
			}
		}

		public readonly FPVector3 YZX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Y;
				result.Y = Z;
				result.Z = X;
				return result;
			}
		}

		public readonly FPVector3 YXY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Y;
				result.Y = X;
				result.Z = Y;
				return result;
			}
		}

		public readonly FPVector3 YXZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Y;
				result.Y = X;
				result.Z = Z;
				return result;
			}
		}

		public readonly FPVector3 YXX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Y;
				result.Y = X;
				result.Z = X;
				return result;
			}
		}

		public readonly FPVector2 YY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector2 result = default(FPVector2);
				result.X = Y;
				result.Y = Y;
				return result;
			}
		}

		public readonly FPVector2 YZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector2 result = default(FPVector2);
				result.X = Y;
				result.Y = Z;
				return result;
			}
		}

		public readonly FPVector2 YX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector2 result = default(FPVector2);
				result.X = Y;
				result.Y = X;
				return result;
			}
		}

		public readonly FPVector3 ZZZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Z;
				result.Y = Z;
				result.Z = Z;
				return result;
			}
		}

		public readonly FPVector3 ZZX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Z;
				result.Y = Z;
				result.Z = X;
				return result;
			}
		}

		public readonly FPVector3 ZZY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Z;
				result.Y = Z;
				result.Z = Y;
				return result;
			}
		}

		public readonly FPVector3 ZXZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Z;
				result.Y = X;
				result.Z = Z;
				return result;
			}
		}

		public readonly FPVector3 ZXX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Z;
				result.Y = X;
				result.Z = X;
				return result;
			}
		}

		public readonly FPVector3 ZXY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Z;
				result.Y = X;
				result.Z = Y;
				return result;
			}
		}

		public readonly FPVector3 ZYZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Z;
				result.Y = Y;
				result.Z = Z;
				return result;
			}
		}

		public readonly FPVector3 ZYX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Z;
				result.Y = Y;
				result.Z = X;
				return result;
			}
		}

		public readonly FPVector3 ZYY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = Z;
				result.Y = Y;
				result.Z = Y;
				return result;
			}
		}

		public readonly FPVector2 ZZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector2 result = default(FPVector2);
				result.X = Z;
				result.Y = Z;
				return result;
			}
		}

		public readonly FPVector2 ZX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector2 result = default(FPVector2);
				result.X = Z;
				result.Y = X;
				return result;
			}
		}

		public readonly FPVector2 ZY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector2 result = default(FPVector2);
				result.X = Z;
				result.Y = Y;
				return result;
			}
		}

		public readonly FPVector3 XYO
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = X;
				result.Y = Y;
				result.Z = default(FP);
				return result;
			}
		}

		public readonly FPVector3 XOZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = X;
				result.Y = default(FP);
				result.Z = Z;
				return result;
			}
		}

		public readonly FPVector3 OYZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FPVector3 result = default(FPVector3);
				result.X = default(FP);
				result.Y = Y;
				result.Z = Z;
				return result;
			}
		}

		/// <summary>
		/// 归一化给定向量 如果向量长度过小则返回 <see cref="P:Photon.Deterministic.FPVector3.Zero" />
		/// </summary>
		/// <param name="value">待归一化的向量</param>
		/// <returns>一个归一化向量</returns>
		public static FPVector3 Normalize(FPVector3 value)
		{
			ulong num = (ulong)(value.X.RawValue * value.X.RawValue + value.Y.RawValue * value.Y.RawValue + value.Z.RawValue * value.Z.RawValue);
			if (num == 0L)
			{
				return default(FPVector3);
			}
			FPMath.ExponentMantisaPair sqrtExponentMantissa = FPMath.GetSqrtExponentMantissa(num);
			long num2 = 17592186044416L / sqrtExponentMantissa.Mantissa;
			value.X.RawValue = value.X.RawValue * num2 >> 22 + sqrtExponentMantissa.Exponent - 8;
			value.Y.RawValue = value.Y.RawValue * num2 >> 22 + sqrtExponentMantissa.Exponent - 8;
			value.Z.RawValue = value.Z.RawValue * num2 >> 22 + sqrtExponentMantissa.Exponent - 8;
			return value;
		}

		/// <summary>
		/// 归一化给定向量 如果向量长度过小则返回 <see cref="P:Photon.Deterministic.FPVector3.Zero" />
		/// </summary>
		/// <param name="value">待归一化的向量</param>
		/// <param name="magnitude">原向量的模长</param>
		/// <returns>一个归一化向量</returns>
		public static FPVector3 Normalize(FPVector3 value, out FP magnitude)
		{
			ulong num = (ulong)(value.X.RawValue * value.X.RawValue + value.Y.RawValue * value.Y.RawValue + value.Z.RawValue * value.Z.RawValue);
			if (num == 0L)
			{
				magnitude.RawValue = 0L;
				return default(FPVector3);
			}
			FPMath.ExponentMantisaPair sqrtExponentMantissa = FPMath.GetSqrtExponentMantissa(num);
			long num2 = 17592186044416L / sqrtExponentMantissa.Mantissa;
			value.X.RawValue = value.X.RawValue * num2 >> 22 + sqrtExponentMantissa.Exponent - 8;
			value.Y.RawValue = value.Y.RawValue * num2 >> 22 + sqrtExponentMantissa.Exponent - 8;
			value.Z.RawValue = value.Z.RawValue * num2 >> 22 + sqrtExponentMantissa.Exponent - 8;
			magnitude.RawValue = (long)sqrtExponentMantissa.Mantissa << sqrtExponentMantissa.Exponent;
			magnitude.RawValue >>= 14;
			return value;
		}

		public unsafe static void Serialize(void* ptr, IDeterministicFrameSerializer serializer)
		{
			FP.Serialize(&((FPVector3*)ptr)->X, serializer);
			FP.Serialize(&((FPVector3*)ptr)->Y, serializer);
			FP.Serialize(&((FPVector3*)ptr)->Z, serializer);
		}

		/// <summary>
		/// 初始化结构体的新实例
		/// </summary>
		/// <param name="x">向量的 x 分量</param>
		/// <param name="y">向量的 y 分量</param>
		/// <param name="z">向量的 z 分量</param>
		public FPVector3(int x, int y, int z)
		{
			X.RawValue = (long)x << 16;
			Y.RawValue = (long)y << 16;
			Z.RawValue = (long)z << 16;
		}

		/// <summary>
		/// 初始化结构体的新实例
		/// </summary>
		/// <param name="x">向量的 x 分量</param>
		/// <param name="y">向量的 y 分量</param>
		public FPVector3(int x, int y)
		{
			X.RawValue = (long)x << 16;
			Y.RawValue = (long)y << 16;
			Z = FP._0;
		}

		/// <summary>
		/// 初始化结构体的新实例
		/// </summary>
		/// <param name="x">向量的 x 分量</param>
		/// <param name="y">向量的 y 分量</param>
		/// <param name="z">向量的 z 分量</param>
		public FPVector3(FP x, FP y, FP z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		/// <summary>
		/// 初始化结构体的新实例
		/// </summary>
		/// <param name="x">向量的 x 分量</param>
		/// <param name="y">向量的 y 分量</param>
		public FPVector3(FP x, FP y)
		{
			X = x;
			Y = y;
			Z = FP._0;
		}

		/// <summary>
		/// 根据FPVector3构建字符串
		/// </summary>
		/// <returns>包含三个分量的字符串</returns>
		public override readonly string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "({0}, {1}, {2})", X.AsFloat, Y.AsFloat, Z.AsFloat);
		}

		/// <summary>
		/// 判断对象是否与此向量相等
		/// </summary>
		/// <param name="obj">待比较的对象</param>
		/// <returns>如果两者相等则返回<see langword="true" />否则返回<see langword="false" /></returns>
		public override readonly bool Equals(object obj)
		{
			if (obj is FPVector3)
			{
				return this == (FPVector3)obj;
			}
			return false;
		}

		/// <summary>
		/// 判断当前实例是否与指定FPVector3相等
		/// </summary>
		/// <param name="other">用于与当前实例比较的FPVector3</param>
		/// <returns>
		/// <see langword="true" />表示当前实例与指定FPVector3相等否则为<see langword="false" />
		/// </returns>
		public readonly bool Equals(FPVector3 other)
		{
			return this == other;
		}

		/// <summary>
		/// 获取向量的哈希码
		/// </summary>
		/// <returns>向量的哈希码</returns>
		public override readonly int GetHashCode()
		{
			int num = 17;
			num = num * 31 + X.GetHashCode();
			num = num * 31 + Y.GetHashCode();
			return num * 31 + Z.GetHashCode();
		}

		/// <summary>
		/// 返回由 <paramref name="value" /> 各分量绝对值组成的向量
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FPVector3 Abs(FPVector3 value)
		{
			long num = value.X.RawValue >> 63;
			value.X.RawValue = (value.X.RawValue + num) ^ num;
			num = value.Y.RawValue >> 63;
			value.Y.RawValue = (value.Y.RawValue + num) ^ num;
			num = value.Z.RawValue >> 63;
			value.Z.RawValue = (value.Z.RawValue + num) ^ num;
			return value;
		}

		/// <summary>
		/// 根据 <paramref name="t" /> 在 <paramref name="start" /> 与 <paramref name="end" /> 之间进行线性插值
		/// 参数 <paramref name="t" /> 会限制在 [0 1] 范围内
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static FPVector3 Lerp(FPVector3 start, FPVector3 end, FP t)
		{
			if (t.RawValue < 0)
			{
				t.RawValue = 0L;
			}
			if (t.RawValue > 65536)
			{
				t.RawValue = 65536L;
			}
			start.X.RawValue += (end.X.RawValue - start.X.RawValue) * t.RawValue + 32768 >> 16;
			start.Y.RawValue += (end.Y.RawValue - start.Y.RawValue) * t.RawValue + 32768 >> 16;
			start.Z.RawValue += (end.Z.RawValue - start.Z.RawValue) * t.RawValue + 32768 >> 16;
			return start;
		}

		/// <summary>
		/// 根据 <paramref name="t" /> 在 <paramref name="start" /> 与 <paramref name="end" /> 之间进行线性插值
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static FPVector3 LerpUnclamped(FPVector3 start, FPVector3 end, FP t)
		{
			start.X.RawValue += (end.X.RawValue - start.X.RawValue) * t.RawValue + 32768 >> 16;
			start.Y.RawValue += (end.Y.RawValue - start.Y.RawValue) * t.RawValue + 32768 >> 16;
			start.Z.RawValue += (end.Z.RawValue - start.Z.RawValue) * t.RawValue + 32768 >> 16;
			return start;
		}

		/// <summary>
		/// 根据 <paramref name="t" /> 在 <paramref name="from" /> 与 <paramref name="to" /> 之间进行球面插值
		/// 参数 <paramref name="t" /> 会限制在 [0 1] 范围内
		/// </summary>
		/// <remarks>输入向量会归一化并视为方向向量
		/// <remarks>结果向量的方向按夹角进行球面插值 模长在 <paramref name="from" /> 与 <paramref name="to" /> 的模长之间进行线性插值</remarks>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPVector3 Slerp(FPVector3 from, FPVector3 to, FP t)
		{
			if (t.RawValue < 0)
			{
				t.RawValue = 0L;
			}
			else if (t.RawValue > 65536)
			{
				t.RawValue = 65536L;
			}
			return SlerpUnclamped(from, to, t);
		}

		/// <summary>
		/// 根据 <paramref name="t" /> 在 <paramref name="from" /> 与 <paramref name="to" /> 之间进行球面插值
		/// </summary>
		/// <remarks>输入向量会归一化并视为方向向量
		/// <remarks>结果向量的方向按夹角进行球面插值 模长在 <paramref name="from" /> 与 <paramref name="to" /> 的模长之间进行线性插值</remarks>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static FPVector3 SlerpUnclamped(FPVector3 from, FPVector3 to, FP t)
		{
			FP magnitude;
			FPVector3 fPVector = Normalize(from, out magnitude);
			if (magnitude.RawValue == 0L)
			{
				FPVector3 result = default(FPVector3);
				result.X.RawValue = to.X.RawValue * t.RawValue + 32768 >> 16;
				result.Y.RawValue = to.Y.RawValue * t.RawValue + 32768 >> 16;
				result.Z.RawValue = to.Z.RawValue * t.RawValue + 32768 >> 16;
				return result;
			}
			FP magnitude2;
			FPVector3 b = Normalize(to, out magnitude2);
			magnitude.RawValue += (magnitude2.RawValue - magnitude.RawValue) * t.RawValue + 32768 >> 16;
			FP rad = default(FP);
			rad.RawValue = (fPVector.X.RawValue * b.X.RawValue + 32768 >> 16) + (fPVector.Y.RawValue * b.Y.RawValue + 32768 >> 16) + (fPVector.Z.RawValue * b.Z.RawValue + 32768 >> 16);
			if (rad.RawValue < -65534)
			{
				rad.RawValue = -65536L;
			}
			else if (rad.RawValue > 65534)
			{
				rad.RawValue = 65536L;
			}
			FP rad2 = default(FP);
			rad2.RawValue = FPLut.acos_lut[rad.RawValue + 65536];
			FPMath.SinCosHighPrecision(rad2, out var sin, out var cos);
			if (Math.Abs(rad.RawValue) >= 65534)
			{
				if (cos.RawValue > 0)
				{
					fPVector.X.RawValue = fPVector.X.RawValue * magnitude.RawValue + 32768 >> 16;
					fPVector.Y.RawValue = fPVector.Y.RawValue * magnitude.RawValue + 32768 >> 16;
					fPVector.Z.RawValue = fPVector.Z.RawValue * magnitude.RawValue + 32768 >> 16;
					return fPVector;
				}
				long num = b.Z.RawValue * b.Z.RawValue + 32768 >> 16;
				if (num == 65536)
				{
					b.X.RawValue = 0L;
					b.Y.RawValue = fPVector.Z.RawValue;
					b.Z.RawValue = -fPVector.Y.RawValue;
				}
				else
				{
					b.X.RawValue = fPVector.Y.RawValue;
					b.Y.RawValue = -fPVector.X.RawValue;
					b.Z.RawValue = 0L;
				}
				b = Cross(fPVector, b).Normalized;
				t.RawValue <<= 1;
				sin.RawValue = 65536L;
				cos.RawValue = 0L;
				rad2.RawValue = FPLut.acos_lut[65536];
			}
			rad.RawValue = t.RawValue * rad2.RawValue + 32768 >> 16;
			FPMath.SinCosRaw(rad, out var sinRaw, out var cosRaw);
			rad.RawValue = (sin.RawValue * cosRaw + 32768 >> 16) - (cos.RawValue * sinRaw + 32768 >> 16);
			fPVector.X.RawValue = (fPVector.X.RawValue * rad.RawValue + b.X.RawValue * sinRaw) / sin.RawValue;
			fPVector.Y.RawValue = (fPVector.Y.RawValue * rad.RawValue + b.Y.RawValue * sinRaw) / sin.RawValue;
			fPVector.Z.RawValue = (fPVector.Z.RawValue * rad.RawValue + b.Z.RawValue * sinRaw) / sin.RawValue;
			fPVector.X.RawValue = fPVector.X.RawValue * magnitude.RawValue + 32768 >> 16;
			fPVector.Y.RawValue = fPVector.Y.RawValue * magnitude.RawValue + 32768 >> 16;
			fPVector.Z.RawValue = fPVector.Z.RawValue * magnitude.RawValue + 32768 >> 16;
			return fPVector;
		}

		/// <summary>
		/// 将两个向量的对应分量相乘
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static FPVector3 Scale(FPVector3 a, FPVector3 b)
		{
			a.X.RawValue = a.X.RawValue * b.X.RawValue + 32768 >> 16;
			a.Y.RawValue = a.Y.RawValue * b.Y.RawValue + 32768 >> 16;
			a.Z.RawValue = a.Z.RawValue * b.Z.RawValue + 32768 >> 16;
			return a;
		}

		/// <summary>
		/// 限制向量的模长
		/// </summary>
		/// <param name="vector">待限制长度的向量</param>
		/// <param name="maxLength">给定向量允许的最大长度</param>
		/// <returns>限制长度后的向量</returns>
		public static FPVector3 ClampMagnitude(FPVector3 vector, FP maxLength)
		{
			long num = maxLength.RawValue * maxLength.RawValue + 32768 >> 16;
			long num2 = (vector.X.RawValue * vector.X.RawValue + 32768 >> 16) + (vector.Y.RawValue * vector.Y.RawValue + 32768 >> 16) + (vector.Z.RawValue * vector.Z.RawValue + 32768 >> 16);
			if (num2 > num)
			{
				vector = Normalize(vector);
				vector.X.RawValue = vector.X.RawValue * maxLength.RawValue + 32768 >> 16;
				vector.Y.RawValue = vector.Y.RawValue * maxLength.RawValue + 32768 >> 16;
				vector.Z.RawValue = vector.Z.RawValue * maxLength.RawValue + 32768 >> 16;
			}
			return vector;
		}

		/// <summary>
		/// 返回由两个向量各分量较小值组成的向量
		/// </summary>
		/// <param name="value1">第一个值</param>
		/// <param name="value2">第二个值</param>
		/// <returns>由两个向量各分量较小值组成的向量</returns>
		public static FPVector3 Min(FPVector3 value1, FPVector3 value2)
		{
			value1.X = ((value1.X.RawValue < value2.X.RawValue) ? value1.X : value2.X);
			value1.Y = ((value1.Y.RawValue < value2.Y.RawValue) ? value1.Y : value2.Y);
			value1.Z = ((value1.Z.RawValue < value2.Z.RawValue) ? value1.Z : value2.Z);
			return value1;
		}

		/// <summary>
		/// 返回由两个向量各分量较大值组成的向量
		/// </summary>
		/// <param name="value1">第一个值</param>
		/// <param name="value2">第二个值</param>
		/// <returns>由两个向量各分量较大值组成的向量</returns>
		public static FPVector3 Max(FPVector3 value1, FPVector3 value2)
		{
			value1.X = ((value1.X.RawValue > value2.X.RawValue) ? value1.X : value2.X);
			value1.Y = ((value1.Y.RawValue > value2.Y.RawValue) ? value1.Y : value2.Y);
			value1.Z = ((value1.Z.RawValue > value2.Z.RawValue) ? value1.Z : value2.Z);
			return value1;
		}

		/// <summary>
		/// 计算两个向量之间的距离
		/// </summary>
		/// <param name="a">第一向量</param>
		/// <param name="b">第二向量</param>
		/// <returns>两个向量之间的距离</returns>
		public static FP Distance(FPVector3 a, FPVector3 b)
		{
			a.X.RawValue = a.X.RawValue - b.X.RawValue;
			a.Y.RawValue = a.Y.RawValue - b.Y.RawValue;
			a.Z.RawValue = a.Z.RawValue - b.Z.RawValue;
			a.X.RawValue = FPMath.SqrtRaw((a.X.RawValue * a.X.RawValue + 32768 >> 16) + (a.Y.RawValue * a.Y.RawValue + 32768 >> 16) + (a.Z.RawValue * a.Z.RawValue + 32768 >> 16));
			return a.X;
		}

		/// <summary>
		/// 计算两个向量之间距离的平方
		/// </summary>
		/// <param name="a">第一向量</param>
		/// <param name="b">第二向量</param>
		/// <returns>两个向量之间距离的平方</returns>
		public static FP DistanceSquared(FPVector3 a, FPVector3 b)
		{
			a.X.RawValue = a.X.RawValue - b.X.RawValue;
			a.Y.RawValue = a.Y.RawValue - b.Y.RawValue;
			a.Z.RawValue = a.Z.RawValue - b.Z.RawValue;
			a.X.RawValue = (a.X.RawValue * a.X.RawValue + 32768 >> 16) + (a.Y.RawValue * a.Y.RawValue + 32768 >> 16) + (a.Z.RawValue * a.Z.RawValue + 32768 >> 16);
			return a.X;
		}

		/// <summary>
		/// 计算两个向量的叉积
		/// </summary>
		/// <param name="a">第一个向量</param>
		/// <param name="b">第二个向量</param>
		/// <returns>两个向量的叉积</returns>
		public static FPVector3 Cross(FPVector3 a, FPVector3 b)
		{
			FPVector3 result = default(FPVector3);
			result.X.RawValue = (a.Y.RawValue * b.Z.RawValue + 32768 >> 16) - (a.Z.RawValue * b.Y.RawValue + 32768 >> 16);
			result.Y.RawValue = (a.Z.RawValue * b.X.RawValue + 32768 >> 16) - (a.X.RawValue * b.Z.RawValue + 32768 >> 16);
			result.Z.RawValue = (a.X.RawValue * b.Y.RawValue + 32768 >> 16) - (a.Y.RawValue * b.X.RawValue + 32768 >> 16);
			return result;
		}

		/// <summary>
		/// 计算两个向量的点积
		/// </summary>
		/// <param name="a">第一个向量</param>
		/// <param name="b">第二个向量</param>
		/// <returns>两个向量的点积</returns>
		public static FP Dot(FPVector3 a, FPVector3 b)
		{
			a.X.RawValue = (a.X.RawValue * b.X.RawValue + 32768 >> 16) + (a.Y.RawValue * b.Y.RawValue + 32768 >> 16) + (a.Z.RawValue * b.Z.RawValue + 32768 >> 16);
			return a.X;
		}

		/// <summary>
		/// 返回 <paramref name="a" /> 绕 <paramref name="axis" /> 旋转到 <paramref name="b" /> 的有符号角度 单位为度
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <param name="axis"></param>
		/// <returns></returns>
		public static FP SignedAngle(FPVector3 a, FPVector3 b, FPVector3 axis)
		{
			FP result = Angle(a, b);
			FPVector3 fPVector = default(FPVector3);
			fPVector.X.RawValue = (a.Y.RawValue * b.Z.RawValue + 32768 >> 16) - (a.Z.RawValue * b.Y.RawValue + 32768 >> 16);
			fPVector.Y.RawValue = (a.Z.RawValue * b.X.RawValue + 32768 >> 16) - (a.X.RawValue * b.Z.RawValue + 32768 >> 16);
			fPVector.Z.RawValue = (a.X.RawValue * b.Y.RawValue + 32768 >> 16) - (a.Y.RawValue * b.X.RawValue + 32768 >> 16);
			a.X.RawValue = (fPVector.X.RawValue * axis.X.RawValue + 32768 >> 16) + (fPVector.Y.RawValue * axis.Y.RawValue + 32768 >> 16) + (fPVector.Z.RawValue * axis.Z.RawValue + 32768 >> 16);
			if (a.X.RawValue < 0)
			{
				result.RawValue = -result.RawValue;
			}
			return result;
		}

		/// <summary>
		/// 返回<paramref name="a" />和<paramref name="b" />之间的角度单位为度
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static FP Angle(FPVector3 a, FPVector3 b)
		{
			long num = (a.X.RawValue * a.X.RawValue + 32768 >> 16) + (a.Y.RawValue * a.Y.RawValue + 32768 >> 16) + (a.Z.RawValue * a.Z.RawValue + 32768 >> 16);
			if (num == 0L)
			{
				return default(FP);
			}
			num = 4294967296L / FPMath.SqrtRaw(num);
			a.X.RawValue = a.X.RawValue * num + 32768 >> 16;
			a.Y.RawValue = a.Y.RawValue * num + 32768 >> 16;
			a.Z.RawValue = a.Z.RawValue * num + 32768 >> 16;
			num = (b.X.RawValue * b.X.RawValue + 32768 >> 16) + (b.Y.RawValue * b.Y.RawValue + 32768 >> 16) + (b.Z.RawValue * b.Z.RawValue + 32768 >> 16);
			if (num == 0L)
			{
				return default(FP);
			}
			num = 4294967296L / FPMath.SqrtRaw(num);
			b.X.RawValue = b.X.RawValue * num + 32768 >> 16;
			b.Y.RawValue = b.Y.RawValue * num + 32768 >> 16;
			b.Z.RawValue = b.Z.RawValue * num + 32768 >> 16;
			num = (a.X.RawValue * b.X.RawValue + 32768 >> 16) + (a.Y.RawValue * b.Y.RawValue + 32768 >> 16) + (a.Z.RawValue * b.Z.RawValue + 32768 >> 16);
			if (num < -65534)
			{
				num = -65536L;
			}
			else if (num > 65534)
			{
				num = 65536L;
			}
			num = FPLut.acos_lut[num + 65536];
			a.X.RawValue = num * FP.Rad2Deg.RawValue + 32768 >> 16;
			return a.X;
		}

		/// <summary>
		/// 计算从 <paramref name="from" /> 向 <paramref name="to" /> 移动后的位置 移动距离不超过 <paramref name="maxDelta" />
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <param name="maxDelta"></param>
		/// <returns></returns>
		public static FPVector3 MoveTowards(FPVector3 from, FPVector3 to, FP maxDelta)
		{
			FPVector3 fPVector = default(FPVector3);
			fPVector.X.RawValue = to.X.RawValue - from.X.RawValue;
			fPVector.Y.RawValue = to.Y.RawValue - from.Y.RawValue;
			fPVector.Z.RawValue = to.Z.RawValue - from.Z.RawValue;
			long num = FPMath.SqrtRaw((fPVector.X.RawValue * fPVector.X.RawValue + 32768 >> 16) + (fPVector.Y.RawValue * fPVector.Y.RawValue + 32768 >> 16) + (fPVector.Z.RawValue * fPVector.Z.RawValue + 32768 >> 16));
			if (num <= maxDelta.RawValue || num == 0L)
			{
				return to;
			}
			from.X.RawValue += (fPVector.X.RawValue << 16) / num * maxDelta.RawValue + 32768 >> 16;
			from.Y.RawValue += (fPVector.Y.RawValue << 16) / num * maxDelta.RawValue + 32768 >> 16;
			from.Z.RawValue += (fPVector.Z.RawValue << 16) / num * maxDelta.RawValue + 32768 >> 16;
			return from;
		}

		/// <summary>
		/// 将向量投影到另一个向量上
		/// </summary>
		/// <param name="vector"></param>
		/// <param name="normal"></param>
		/// <returns></returns>
		public static FPVector3 Project(FPVector3 vector, FPVector3 normal)
		{
			FP fP = Dot(normal, normal);
			if (fP < FP.Epsilon)
			{
				return Zero;
			}
			return normal * Dot(vector, normal) / fP;
		}

		/// <summary>
		/// 将向量投影到由法线定义的平面上 法线与平面正交
		/// </summary>
		/// <param name="vector"></param>
		/// <param name="planeNormal"></param>
		/// <returns>向量在平面上的投影</returns>
		public static FPVector3 ProjectOnPlane(FPVector3 vector, FPVector3 planeNormal)
		{
			return vector - Project(vector, planeNormal);
		}

		/// <summary>
		/// 根据平面法线反射向量
		/// </summary>
		/// <param name="vector"></param>
		/// <param name="normal"></param>
		/// <returns></returns>
		public static FPVector3 Reflect(FPVector3 vector, FPVector3 normal)
		{
			return -2 * Dot(normal, vector) * normal + vector;
		}

		/// <summary>
		/// 计算三角形内一点的重心坐标 连续计算多个点积会带来精度损失
		/// </summary>
		/// <param name="p">点的惯量在三角形</param>
		/// <param name="p0">顶点1</param>
		/// <param name="p1">顶点2</param>
		/// <param name="p2">顶点3</param>
		/// <param name="u">重心变量用于p0</param>
		/// <param name="v">重心变量用于p1</param>
		/// <param name="w">重心变量用于p2</param>
		/// <returns>点在三角形内部时返回 <see langword="true" /> 点在外部时不设置输出参数</returns>
		internal static bool Barycentric(FPVector3 p, FPVector3 p0, FPVector3 p1, FPVector3 p2, out FP u, out FP v, out FP w)
		{
			v.RawValue = 0L;
			w.RawValue = 0L;
			u.RawValue = 0L;
			FPVector3 fPVector = default(FPVector3);
			FPVector3 fPVector2 = default(FPVector3);
			fPVector2.X.RawValue = p1.X.RawValue - p0.X.RawValue;
			fPVector2.Y.RawValue = p1.Y.RawValue - p0.Y.RawValue;
			fPVector2.Z.RawValue = p1.Z.RawValue - p0.Z.RawValue;
			FPVector3 fPVector3 = default(FPVector3);
			fPVector3.X.RawValue = p2.X.RawValue - p0.X.RawValue;
			fPVector3.Y.RawValue = p2.Y.RawValue - p0.Y.RawValue;
			fPVector3.Z.RawValue = p2.Z.RawValue - p0.Z.RawValue;
			fPVector.X.RawValue = p.X.RawValue - p0.X.RawValue;
			fPVector.Y.RawValue = p.Y.RawValue - p0.Y.RawValue;
			fPVector.Z.RawValue = p.Z.RawValue - p0.Z.RawValue;
			long num = (fPVector2.X.RawValue * fPVector2.X.RawValue + 32768 >> 16) + (fPVector2.Y.RawValue * fPVector2.Y.RawValue + 32768 >> 16) + (fPVector2.Z.RawValue * fPVector2.Z.RawValue + 32768 >> 16);
			long num2 = (fPVector2.X.RawValue * fPVector3.X.RawValue + 32768 >> 16) + (fPVector2.Y.RawValue * fPVector3.Y.RawValue + 32768 >> 16) + (fPVector2.Z.RawValue * fPVector3.Z.RawValue + 32768 >> 16);
			long num3 = (fPVector3.X.RawValue * fPVector3.X.RawValue + 32768 >> 16) + (fPVector3.Y.RawValue * fPVector3.Y.RawValue + 32768 >> 16) + (fPVector3.Z.RawValue * fPVector3.Z.RawValue + 32768 >> 16);
			long num4 = (fPVector.X.RawValue * fPVector2.X.RawValue + 32768 >> 16) + (fPVector.Y.RawValue * fPVector2.Y.RawValue + 32768 >> 16) + (fPVector.Z.RawValue * fPVector2.Z.RawValue + 32768 >> 16);
			long num5 = (fPVector.X.RawValue * fPVector3.X.RawValue + 32768 >> 16) + (fPVector.Y.RawValue * fPVector3.Y.RawValue + 32768 >> 16) + (fPVector.Z.RawValue * fPVector3.Z.RawValue + 32768 >> 16);
			long num6 = (num * num3 + 32768 >> 16) - (num2 * num2 + 32768 >> 16);
			if (num6 < 0)
			{
				return false;
			}
			float num7 = num2 * num5 - num3 * num4;
			float num8 = num2 * num4 - num * num5;
			if (num7 + num8 <= (float)num6 && num7 >= 0f)
			{
				_ = 0f;
			}
			v.RawValue = ((num3 * num4 + 32768 >> 16) - (num2 * num5 + 32768 >> 16) << 16) / num6;
			w.RawValue = ((num * num5 + 32768 >> 16) - (num2 * num4 + 32768 >> 16) << 16) / num6;
			u.RawValue = FP._1.RawValue - v.RawValue - w.RawValue;
			return true;
		}

		/// <summary>
		/// 如果两个向量完全相等则返回 <see langword="true" />
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FPVector3 a, FPVector3 b)
		{
			if (a.X.RawValue == b.X.RawValue && a.Y.RawValue == b.Y.RawValue)
			{
				return a.Z.RawValue == b.Z.RawValue;
			}
			return false;
		}

		/// <summary>
		/// 如果两个向量不完全相等则返回 <see langword="true" />
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FPVector3 a, FPVector3 b)
		{
			if (a.X.RawValue == b.X.RawValue && a.Y.RawValue == b.Y.RawValue)
			{
				return a.Z.RawValue != b.Z.RawValue;
			}
			return true;
		}

		/// <summary>
		/// 取反每个分量的<paramref name="v" />向量
		/// </summary>
		/// <param name="v"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPVector3 operator -(FPVector3 v)
		{
			v.X.RawValue = -v.X.RawValue;
			v.Y.RawValue = -v.Y.RawValue;
			v.Z.RawValue = -v.Z.RawValue;
			return v;
		}

		/// <summary>
		/// 乘以每个分量的<paramref name="v" />乘以<paramref name="s" />
		/// </summary>
		/// <param name="v"></param>
		/// <param name="s"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPVector3 operator *(FPVector3 v, FP s)
		{
			v.X.RawValue = v.X.RawValue * s.RawValue + 32768 >> 16;
			v.Y.RawValue = v.Y.RawValue * s.RawValue + 32768 >> 16;
			v.Z.RawValue = v.Z.RawValue * s.RawValue + 32768 >> 16;
			return v;
		}

		/// <summary>
		/// 乘以每个分量的<paramref name="v" />乘以<paramref name="s" />
		/// </summary>
		/// <param name="v"></param>
		/// <param name="s"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPVector3 operator *(FP s, FPVector3 v)
		{
			v.X.RawValue = v.X.RawValue * s.RawValue + 32768 >> 16;
			v.Y.RawValue = v.Y.RawValue * s.RawValue + 32768 >> 16;
			v.Z.RawValue = v.Z.RawValue * s.RawValue + 32768 >> 16;
			return v;
		}

		/// <summary>
		/// 将 <paramref name="v" /> 的每个分量除以 <paramref name="s" />
		/// </summary>
		/// <param name="v"></param>
		/// <param name="s"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPVector3 operator /(FPVector3 v, FP s)
		{
			v.X.RawValue = (v.X.RawValue << 16) / s.RawValue;
			v.Y.RawValue = (v.Y.RawValue << 16) / s.RawValue;
			v.Z.RawValue = (v.Z.RawValue << 16) / s.RawValue;
			return v;
		}

		/// <summary>
		/// 将 <paramref name="v" /> 的每个分量除以 <paramref name="s" />
		/// </summary>
		/// <param name="v"></param>
		/// <param name="s"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPVector3 operator /(FPVector3 v, int s)
		{
			v.X.RawValue /= s;
			v.Y.RawValue /= s;
			v.Z.RawValue /= s;
			return v;
		}

		/// <summary>
		/// 从 <paramref name="a" /> 中减去 <paramref name="b" />
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPVector3 operator -(FPVector3 a, FPVector3 b)
		{
			a.X.RawValue = a.X.RawValue - b.X.RawValue;
			a.Y.RawValue = a.Y.RawValue - b.Y.RawValue;
			a.Z.RawValue = a.Z.RawValue - b.Z.RawValue;
			return a;
		}

		/// <summary>
		/// 加上两个向量
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPVector3 operator +(FPVector3 a, FPVector3 b)
		{
			a.X.RawValue = a.X.RawValue + b.X.RawValue;
			a.Y.RawValue = a.Y.RawValue + b.Y.RawValue;
			a.Z.RawValue = a.Z.RawValue + b.Z.RawValue;
			return a;
		}
	}
}

