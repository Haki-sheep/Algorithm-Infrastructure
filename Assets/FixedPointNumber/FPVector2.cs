using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Photon.Deterministic
{
	/// <summary>
	/// 表示一个2D向量
	/// </summary>
	/// \ingroup MathAPI
	[Serializable]
	[StructLayout(LayoutKind.Explicit)]
	public struct FPVector2 : IEquatable<FPVector2>
	{
		/// <summary>
		/// 用于比较 FPVector2 对象是否相等
		/// </summary>
		public class EqualityComparer : IEqualityComparer<FPVector2>
		{
			/// <summary>
			/// 全局FPVector2相等比较器实例
			/// </summary>
			public static readonly EqualityComparer Instance = new EqualityComparer();

			private EqualityComparer()
			{
			}

			bool IEqualityComparer<FPVector2>.Equals(FPVector2 x, FPVector2 y)
			{
				return x == y;
			}

			int IEqualityComparer<FPVector2>.GetHashCode(FPVector2 obj)
			{
				return obj.GetHashCode();
			}
		}

		/// <summary>
		/// 分量或结构体类型在Frame数据缓冲区或栈中的内存大小作为值参数传递时适用
		/// 这与结构体占用的快照载荷无关快照载荷会按位打包并压缩
		/// </summary>
		public const int SIZE = 16;

		/// <summary>向量的 x 分量</summary>
		[FieldOffset(0)]
		public FP X;

		/// <summary>向量的 y 分量</summary>
		[FieldOffset(8)]
		public FP Y;

		/// <summary>
		/// 分量为 (0,0)
		/// </summary>
		public static FPVector2 Zero => default(FPVector2);

		/// <summary>
		/// 分量为 (1,1)
		/// </summary>
		public static FPVector2 One => new FPVector2
		{
			X = 
			{
				RawValue = 65536L
			},
			Y = 
			{
				RawValue = 65536L
			}
		};

		/// <summary>
		/// 分量为 (1,0)
		/// </summary>
		public static FPVector2 Right => new FPVector2
		{
			X = 
			{
				RawValue = 65536L
			}
		};

		/// <summary>
		/// 分量为 (-1,0)
		/// </summary>
		public static FPVector2 Left => new FPVector2
		{
			X = 
			{
				RawValue = -65536L
			}
		};

		/// <summary>
		/// 分量为 (0,1)
		/// </summary>
		public static FPVector2 Up => new FPVector2
		{
			Y = 
			{
				RawValue = 65536L
			}
		};

		/// <summary>
		/// 分量为 (0,-1)
		/// </summary>
		public static FPVector2 Down => new FPVector2
		{
			Y = 
			{
				RawValue = -65536L
			}
		};

		/// <summary>
		/// 分量为
		/// (FP MinValue FP MinValue)
		/// </summary>
		public static FPVector2 MinValue => new FPVector2
		{
			X = 
			{
				RawValue = long.MinValue
			},
			Y = 
			{
				RawValue = long.MinValue
			}
		};

		/// <summary>
		/// 分量为
		/// (FP MaxValue FP MaxValue)
		/// </summary>
		public static FPVector2 MaxValue => new FPVector2
		{
			X = 
			{
				RawValue = long.MaxValue
			},
			Y = 
			{
				RawValue = long.MaxValue
			}
		};

		/// <summary>
		/// 分量为
		/// (FP UseableMin FP UseableMin)
		/// </summary>
		public static FPVector2 UseableMin => new FPVector2
		{
			X = 
			{
				RawValue = -2147483648L
			},
			Y = 
			{
				RawValue = -2147483648L
			}
		};

		/// <summary>
		/// 分量为
		/// (FP UseableMax FP UseableMax)
		/// </summary>
		public static FPVector2 UseableMax => new FPVector2
		{
			X = 
			{
				RawValue = 2147483647L
			},
			Y = 
			{
				RawValue = 2147483647L
			}
		};

		/// <summary>
		/// 获取向量长度
		/// </summary>
		/// <returns>返回向量长度</returns>
		public readonly FP Magnitude
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				FP result = default(FP);
				result.RawValue = FPMath.SqrtRaw((X.RawValue * X.RawValue + 32768 >> 16) + (Y.RawValue * Y.RawValue + 32768 >> 16));
				return result;
			}
		}

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
				result.RawValue = (X.RawValue * X.RawValue + 32768 >> 16) + (Y.RawValue * Y.RawValue + 32768 >> 16);
				return result;
			}
		}

		/// <summary>
		/// 获取向量的归一化结果
		/// </summary>
		/// <returns>向量的归一化结果</returns>
		public readonly FPVector2 Normalized
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return Normalize(this);
			}
		}

		/// <summary>
		/// 返回向量 (x 0, y)
		/// </summary>
		public readonly FPVector3 XOY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return new FPVector3(X, FP._0, Y);
			}
		}

		/// <summary>
		/// 返回向量 (x y 0)
		/// </summary>
		public readonly FPVector3 XYO
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return new FPVector3(X, Y, FP._0);
			}
		}

		/// <summary>
		/// 返回向量 (0, x y)
		/// </summary>
		public readonly FPVector3 OXY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return new FPVector3(FP._0, X, Y);
			}
		}

		/// <summary>
		/// 按 X X X 分量顺序创建新的 FPVector3
		/// </summary>
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

		/// <summary>
		/// 按 X X Y 分量顺序创建新的 FPVector3
		/// </summary>
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

		/// <summary>
		/// 按 X Y X 分量顺序创建新的 FPVector3
		/// </summary>
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

		/// <summary>
		/// 按 X Y Y 分量顺序创建新的 FPVector3
		/// </summary>
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

		/// <summary>
		/// 按 X X 分量顺序创建新的 FPVector2
		/// </summary>
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

		/// <summary>
		/// 按 X Y 分量顺序创建新的 FPVector2
		/// </summary>
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

		/// <summary>
		/// 按 Y Y Y 分量顺序创建新的 FPVector3
		/// </summary>
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

		/// <summary>
		/// 按 Y Y X 分量顺序创建新的 FPVector3
		/// </summary>
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

		/// <summary>
		/// 按 Y X Y 分量顺序创建新的 FPVector3
		/// </summary>
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

		/// <summary>
		/// 按 Y X X 分量顺序创建新的 FPVector3
		/// </summary>
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

		/// <summary>
		/// 按 Y Y 分量顺序创建新的 FPVector2
		/// </summary>
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

		/// <summary>
		/// 按 Y X 分量顺序创建新的 FPVector2
		/// </summary>
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

		/// <summary>
		/// 归一化给定向量 如果向量长度过小则返回 <see cref="P:Photon.Deterministic.FPVector2.Zero" />
		/// </summary>
		/// <param name="value">待归一化的向量</param>
		/// <returns>一个归一化向量</returns>
		public static FPVector2 Normalize(FPVector2 value)
		{
			ulong num = (ulong)(value.X.RawValue * value.X.RawValue + value.Y.RawValue * value.Y.RawValue);
			if (num == 0L)
			{
				return default(FPVector2);
			}
			FPMath.ExponentMantisaPair sqrtExponentMantissa = FPMath.GetSqrtExponentMantissa(num);
			long num2 = 17592186044416L / sqrtExponentMantissa.Mantissa;
			value.X.RawValue = value.X.RawValue * num2 >> 22 + sqrtExponentMantissa.Exponent - 8;
			value.Y.RawValue = value.Y.RawValue * num2 >> 22 + sqrtExponentMantissa.Exponent - 8;
			return value;
		}

		/// <summary>
		/// 归一化给定向量 如果向量长度过小则返回 <see cref="P:Photon.Deterministic.FPVector2.Zero" />
		/// </summary>
		/// <param name="value">待归一化的向量</param>
		/// <param name="magnitude">原向量的模长</param>
		/// <returns>一个归一化向量</returns>
		public static FPVector2 Normalize(FPVector2 value, out FP magnitude)
		{
			ulong num = (ulong)(value.X.RawValue * value.X.RawValue + value.Y.RawValue * value.Y.RawValue);
			if (num == 0L)
			{
				magnitude.RawValue = 0L;
				return default(FPVector2);
			}
			FPMath.ExponentMantisaPair sqrtExponentMantissa = FPMath.GetSqrtExponentMantissa(num);
			long num2 = 17592186044416L / sqrtExponentMantissa.Mantissa;
			value.X.RawValue = value.X.RawValue * num2 >> 22 + sqrtExponentMantissa.Exponent - 8;
			value.Y.RawValue = value.Y.RawValue * num2 >> 22 + sqrtExponentMantissa.Exponent - 8;
			magnitude.RawValue = (long)sqrtExponentMantissa.Mantissa << sqrtExponentMantissa.Exponent;
			magnitude.RawValue >>= 14;
			return value;
		}

		/// <summary>
		/// 序列化FPVector2实例
		/// </summary>
		/// <param name="ptr">FPVector2实例的指针</param>
		/// <param name="serializer">用于序列化的IDeterministicFrameSerializer实例</param>
		public unsafe static void Serialize(void* ptr, IDeterministicFrameSerializer serializer)
		{
			FP.Serialize(&((FPVector2*)ptr)->X, serializer);
			FP.Serialize(&((FPVector2*)ptr)->Y, serializer);
		}

		/// <summary>
		/// 初始化 FPVector2 结构体的新实例
		/// </summary>
		/// <param name="x">向量的 x 坐标</param>
		/// <param name="y">向量的 y 坐标</param>
		public FPVector2(int x, int y)
		{
			X.RawValue = (long)x << 16;
			Y.RawValue = (long)y << 16;
		}

		/// <summary>
		/// 创建新的 FPVector2 实例
		/// </summary>
		/// <param name="x">x分量</param>
		/// <param name="y">y分量</param>
		public FPVector2(FP x, FP y)
		{
			X = x;
			Y = y;
		}

		/// <summary>
		/// 创建新的 FPVector2 实例
		/// </summary>
		/// <param name="value">赋给两个分量的值</param>
		public FPVector2(FP value)
		{
			X = value;
			Y = value;
		}

		/// <summary>
		/// 判断当前FPVector2实例是否与另一个对象相等
		/// </summary>
		/// <param name="obj">与当前实例比较的对象</param>
		/// <returns>对象与当前 FPVector2 实例相等时返回 <see langword="true" /> 否则返回 <see langword="false" /></returns>
		public override readonly bool Equals(object obj)
		{
			if (!(obj is FPVector2))
			{
				return false;
			}
			return this == (FPVector2)obj;
		}

		/// <summary>
		/// 判断FPVector2实例是否与另一个FPVector2实例相等
		/// </summary>
		/// <param name="other">用于比较的另一个FPVector2实例</param>
		/// <returns>两个实例相等时返回 <see langword="true" /> 否则返回 <see langword="false" /></returns>
		public readonly bool Equals(FPVector2 other)
		{
			return this == other;
		}

		/// <summary>
		/// 计算当前 FPVector2 实例的哈希码
		/// </summary>
		/// <returns>
		/// 32位有符号整数哈希码
		/// </returns>
		public override readonly int GetHashCode()
		{
			int num = 17;
			num = num * 31 + X.GetHashCode();
			return num * 31 + Y.GetHashCode();
		}

		/// <summary>
		/// 返回表示当前FPVector2实例的字符串
		/// </summary>
		/// <returns>当前FPVector2实例的字符串表示</returns>
		public override readonly string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "({0}, {1})", X.AsFloat, Y.AsFloat);
		}

		/// <summary>
		/// 计算两个向量之间的距离
		/// </summary>
		/// <param name="a">第一向量</param>
		/// <param name="b">第二向量</param>
		/// <returns>两个向量之间的距离</returns>
		public static FP Distance(FPVector2 a, FPVector2 b)
		{
			long num = a.X.RawValue - b.X.RawValue;
			long num2 = a.Y.RawValue - b.Y.RawValue;
			FP result = default(FP);
			result.RawValue = FPMath.SqrtRaw((num * num + 32768 >> 16) + (num2 * num2 + 32768 >> 16));
			return result;
		}

		/// <summary>
		/// 计算两个向量之间距离的平方
		/// </summary>
		/// <param name="a">第一向量</param>
		/// <param name="b">第二向量</param>
		/// <returns>两个向量之间距离的平方</returns>
		public static FP DistanceSquared(FPVector2 a, FPVector2 b)
		{
			long num = a.X.RawValue - b.X.RawValue;
			long num2 = a.Y.RawValue - b.Y.RawValue;
			FP result = default(FP);
			result.RawValue = (num * num + 32768 >> 16) + (num2 * num2 + 32768 >> 16);
			return result;
		}

		/// <summary>
		/// 计算两个向量的点积
		/// </summary>
		/// <param name="a">第一个向量</param>
		/// <param name="b">第二个向量</param>
		/// <returns>两个向量的点积</returns>
		public static FP Dot(FPVector2 a, FPVector2 b)
		{
			long num = a.X.RawValue * b.X.RawValue + 32768 >> 16;
			long num2 = a.Y.RawValue * b.Y.RawValue + 32768 >> 16;
			FP result = default(FP);
			result.RawValue = num + num2;
			return result;
		}

		/// <summary>
		/// 限制向量的模长
		/// </summary>
		/// <param name="vector">待限制长度的向量</param>
		/// <param name="maxLength">给定向量允许的最大长度</param>
		/// <returns>限制长度后的向量</returns>
		public static FPVector2 ClampMagnitude(FPVector2 vector, FP maxLength)
		{
			long num = (vector.X.RawValue * vector.X.RawValue + 32768 >> 16) + (vector.Y.RawValue * vector.Y.RawValue + 32768 >> 16);
			if (num <= maxLength.RawValue * maxLength.RawValue + 32768 >> 16)
			{
				return vector;
			}
			long num2 = FPMath.SqrtRaw(num);
			if (num2 <= FP.Epsilon.RawValue)
			{
				return default(FPVector2);
			}
			vector.X.RawValue = (vector.X.RawValue << 16) / num2;
			vector.Y.RawValue = (vector.Y.RawValue << 16) / num2;
			vector.X.RawValue = vector.X.RawValue * maxLength.RawValue + 32768 >> 16;
			vector.Y.RawValue = vector.Y.RawValue * maxLength.RawValue + 32768 >> 16;
			return vector;
		}

		/// <summary>
		/// 将 <paramref name="vectors" /> 中的每个向量旋转 <paramref name="radians" /> 弧度
		/// </summary>
		/// 旋转是逆时针
		/// <param name="vectors"></param>
		/// <param name="radians"></param>
		public static void Rotate(FPVector2[] vectors, FP radians)
		{
			for (int i = 0; i < vectors.Length; i++)
			{
				vectors[i] = Rotate(vectors[i], radians);
			}
		}

		/// <summary>
		/// 将 <paramref name="vector" /> 旋转 <paramref name="radians" /> 弧度
		/// </summary>
		/// 旋转是逆时针
		/// <param name="vector"></param>
		/// <param name="radians"></param>
		public static FPVector2 Rotate(FPVector2 vector, FP radians)
		{
			FPMath.SinCosRaw(radians, out var sinRaw, out var cosRaw);
			long rawValue = (vector.X.RawValue * cosRaw + 32768 >> 16) - (vector.Y.RawValue * sinRaw + 32768 >> 16);
			long rawValue2 = (vector.X.RawValue * sinRaw + 32768 >> 16) + (vector.Y.RawValue * cosRaw + 32768 >> 16);
			vector.X.RawValue = rawValue;
			vector.Y.RawValue = rawValue2;
			return vector;
		}

		/// <summary>
		/// 旋转 <paramref name="vectors" /> 中的每个向量 <paramref name="sin" /> 为角度正弦值 <paramref name="cos" /> 为角度余弦值
		/// </summary>
		/// 旋转是执行的逆时针
		/// <param name="vectors"></param>
		/// <param name="sin"></param>
		/// <param name="cos"></param>
		public static void Rotate(FPVector2[] vectors, FP sin, FP cos)
		{
			for (int i = 0; i < vectors.Length; i++)
			{
				vectors[i] = Rotate(vectors[i], sin, cos);
			}
		}

		/// <summary>
		/// 旋转 <paramref name="vector" /> <paramref name="sin" /> 为角度正弦值 <paramref name="cos" /> 为角度余弦值
		/// </summary>
		/// 旋转是执行的逆时针
		/// <param name="vector"></param>
		/// <param name="sin"></param>
		/// <param name="cos"></param>
		/// <returns></returns>
		public static FPVector2 Rotate(FPVector2 vector, FP sin, FP cos)
		{
			long rawValue = (vector.X.RawValue * cos.RawValue + 32768 >> 16) - (vector.Y.RawValue * sin.RawValue + 32768 >> 16);
			long rawValue2 = (vector.X.RawValue * sin.RawValue + 32768 >> 16) + (vector.Y.RawValue * cos.RawValue + 32768 >> 16);
			vector.X.RawValue = rawValue;
			vector.Y.RawValue = rawValue2;
			return vector;
		}

		/// <summary>
		/// 两个向量的垂直点积这是二维叉积对应于三维叉积的形式
		/// </summary>
		/// <param name="a">第一个向量</param>
		/// <param name="b">第二个向量</param>
		/// <returns>两个向量的叉积</returns>
		public static FP Cross(FPVector2 a, FPVector2 b)
		{
			FP result = default(FP);
			result.RawValue = (a.X.RawValue * b.Y.RawValue + 32768 >> 16) - (a.Y.RawValue * b.X.RawValue + 32768 >> 16);
			return result;
		}

		internal static long CrossRaw(FPVector2 a, FPVector2 b)
		{
			return (a.X.RawValue * b.Y.RawValue + 32768 >> 16) - (a.Y.RawValue * b.X.RawValue + 32768 >> 16);
		}

		/// <summary>
		/// 根据法线定义的直线反射向量
		/// </summary>
		/// <param name="vector">待反射的向量</param>
		/// <param name="normal">定义反射面的法线 应为归一化状态</param>
		/// <returns></returns>
		public static FPVector2 Reflect(FPVector2 vector, FPVector2 normal)
		{
			FP fP = default(FP);
			fP.RawValue = (vector.X.RawValue * normal.X.RawValue + 32768 >> 15) + (vector.Y.RawValue * normal.Y.RawValue + 32768 >> 15);
			vector.X.RawValue -= fP.RawValue * normal.X.RawValue + 32768 >> 16;
			vector.Y.RawValue -= fP.RawValue * normal.Y.RawValue + 32768 >> 16;
			return vector;
		}

		/// <summary>
		/// 将 <paramref name="value" /> 的每个分量限制在 <paramref name="min" /> 与 <paramref name="max" /> 之间
		/// </summary>
		/// <param name="value"></param>
		/// <param name="min"></param>
		/// <param name="max"></param>
		/// <returns></returns>
		public static FPVector2 Clamp(FPVector2 value, FPVector2 min, FPVector2 max)
		{
			return new FPVector2(FPMath.Clamp(value.X, min.X, max.X), FPMath.Clamp(value.Y, min.Y, max.Y));
		}

		/// <summary>
		/// 根据 <paramref name="t" /> 在 <paramref name="start" /> 与 <paramref name="end" /> 之间进行线性插值
		/// 参数 <paramref name="t" /> 会限制在 [0 1] 范围内
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static FPVector2 Lerp(FPVector2 start, FPVector2 end, FP t)
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
			return start;
		}

		/// <summary>
		/// 根据 <paramref name="t" /> 在 <paramref name="start" /> 与 <paramref name="end" /> 之间进行线性插值
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static FPVector2 LerpUnclamped(FPVector2 start, FPVector2 end, FP t)
		{
			start.X.RawValue += (end.X.RawValue - start.X.RawValue) * t.RawValue + 32768 >> 16;
			start.Y.RawValue += (end.Y.RawValue - start.Y.RawValue) * t.RawValue + 32768 >> 16;
			return start;
		}

		/// <summary>
		/// 返回由两个向量各分量较大值组成的向量
		/// </summary>
		/// <param name="value1">第一个值</param>
		/// <param name="value2">第二个值</param>
		/// <returns>由两个向量各分量较大值组成的向量</returns>
		public static FPVector2 Max(FPVector2 value1, FPVector2 value2)
		{
			return new FPVector2(FPMath.Max(value1.X, value2.X), FPMath.Max(value1.Y, value2.Y));
		}

		/// <summary>
		/// 返回由所有向量各分量最大值组成的向量
		/// <paramref name="vectors" /> 为 <see langword="null" /> 或空集合时返回 <see cref="P:Photon.Deterministic.FPVector2.Zero" />
		/// </summary>
		/// <param name="vectors"></param>
		/// <returns></returns>
		public static FPVector2 Max(params FPVector2[] vectors)
		{
			if (vectors == null || vectors.Length == 0)
			{
				return default(FPVector2);
			}
			FPVector2 result = vectors[0];
			for (int i = 1; i < vectors.Length; i++)
			{
				result.X.RawValue = Math.Max(result.X.RawValue, vectors[i].X.RawValue);
				result.Y.RawValue = Math.Max(result.Y.RawValue, vectors[i].Y.RawValue);
			}
			return result;
		}

		/// <summary>
		/// 返回由两个向量各分量较小值组成的向量
		/// </summary>
		/// <param name="value1">第一个值</param>
		/// <param name="value2">第二个值</param>
		/// <returns>由两个向量各分量较小值组成的向量</returns>
		public static FPVector2 Min(FPVector2 value1, FPVector2 value2)
		{
			return new FPVector2(FPMath.Min(value1.X, value2.X), FPMath.Min(value1.Y, value2.Y));
		}

		/// <summary>
		/// 返回由所有向量中最小 x 与 y 分量组成的向量
		/// <paramref name="vectors" /> 为 <see langword="null" /> 或空集合时返回 <see cref="P:Photon.Deterministic.FPVector2.Zero" />
		/// </summary>
		/// <param name="vectors"></param>
		/// <returns></returns>
		public static FPVector2 Min(params FPVector2[] vectors)
		{
			if (vectors == null || vectors.Length == 0)
			{
				return default(FPVector2);
			}
			FPVector2 result = vectors[0];
			for (int i = 1; i < vectors.Length; i++)
			{
				result.X.RawValue = Math.Min(result.X.RawValue, vectors[i].X.RawValue);
				result.Y.RawValue = Math.Min(result.Y.RawValue, vectors[i].Y.RawValue);
			}
			return result;
		}

		/// <summary>
		/// 将两个向量的对应分量相乘
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static FPVector2 Scale(FPVector2 a, FPVector2 b)
		{
			FPVector2 result = default(FPVector2);
			result.X = a.X * b.X;
			result.Y = a.Y * b.Y;
			return result;
		}

		/// <summary>
		/// 返回<paramref name="a" />和<paramref name="b" />之间的角度单位为度
		/// <remarks>
		/// 另请参阅<see cref="M:Photon.Deterministic.FPVector2.Radians(Photon.Deterministic.FPVector2,Photon.Deterministic.FPVector2)" /><seealso cref="M:Photon.Deterministic.FPVector2.RadiansSigned(Photon.Deterministic.FPVector2,Photon.Deterministic.FPVector2)" /><seealso cref="M:Photon.Deterministic.FPVector2.RadiansSkipNormalize(Photon.Deterministic.FPVector2,Photon.Deterministic.FPVector2)" /><seealso cref="M:Photon.Deterministic.FPVector2.RadiansSignedSkipNormalize(Photon.Deterministic.FPVector2,Photon.Deterministic.FPVector2)" />
		/// </remarks>
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static FP Angle(FPVector2 a, FPVector2 b)
		{
			long num = (a.X.RawValue * a.X.RawValue + 32768 >> 16) + (a.Y.RawValue * a.Y.RawValue + 32768 >> 16);
			if (num == 0L)
			{
				return default(FP);
			}
			num = FPMath.SqrtRaw(num);
			a.X.RawValue = (a.X.RawValue << 16) / num;
			a.Y.RawValue = (a.Y.RawValue << 16) / num;
			num = (b.X.RawValue * b.X.RawValue + 32768 >> 16) + (b.Y.RawValue * b.Y.RawValue + 32768 >> 16);
			if (num == 0L)
			{
				return default(FP);
			}
			num = FPMath.SqrtRaw(num);
			b.X.RawValue = (b.X.RawValue << 16) / num;
			b.Y.RawValue = (b.Y.RawValue << 16) / num;
			num = (a.X.RawValue * b.X.RawValue + 32768 >> 16) + (a.Y.RawValue * b.Y.RawValue + 32768 >> 16);
			num = ((num < -65536) ? FPLut.acos_lut[0] : ((num <= 65536) ? FPLut.acos_lut[num + 65536] : FPLut.acos_lut[131072]));
			a.X.RawValue = num * FP.Rad2Deg.RawValue + 32768 >> 16;
			return a.X;
		}

		/// <summary>
		/// 返回顺时针旋转 90 度后的向量
		/// </summary>
		/// <param name="vector"></param>
		/// <returns></returns>
		public static FPVector2 CalculateRight(FPVector2 vector)
		{
			return new FPVector2(vector.Y, -vector.X);
		}

		/// <summary>
		/// 返回逆时针旋转 90 度后的向量
		/// </summary>
		/// <param name="vector"></param>
		/// <returns></returns>
		public static FPVector2 CalculateLeft(FPVector2 vector)
		{
			return new FPVector2(-vector.Y, vector.X);
		}

		/// <summary>
		/// 当此向量位于 <paramref name="vector" /> 右侧时返回 <see langword="true" />
		/// </summary>
		/// <param name="vector"></param>
		/// <returns></returns>
		public readonly bool IsRightOf(FPVector2 vector)
		{
			return Dot(CalculateRight(vector), this) > FP._0;
		}

		/// <summary>
		/// 当此向量位于 <paramref name="vector" /> 左侧时返回 <see langword="true" />
		/// </summary>
		/// <param name="vector"></param>
		/// <returns></returns>
		public readonly bool IsLeftOf(FPVector2 vector)
		{
			return Dot(CalculateLeft(vector), this) > FP._0;
		}

		/// <summary>
		/// 返回两个二维向量的行列式可用于检查两者之间的角度此方法等同于FPVector2.Cross
		/// 行列式等于0时Vector1和Vector2共线
		/// 行列式小于0时Vector1在Vector2左侧
		/// 行列式大于0时Vector1在Vector2右侧
		/// </summary>
		/// <param name="v1">向量一</param>
		/// <param name="v2">向量二</param>
		/// <returns>行列式</returns>
		public static FP Determinant(FPVector2 v1, FPVector2 v2)
		{
			return new FP
			{
				RawValue = (v1.X.RawValue * v2.Y.RawValue + 32768 >> 16) - (v1.Y.RawValue * v2.X.RawValue + 32768 >> 16)
			};
		}

		/// <summary>
		/// 返回两个向量之间的弧度
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static FP Radians(FPVector2 a, FPVector2 b)
		{
			FP value = Dot(Normalize(a), Normalize(b));
			return FPMath.Acos(FPMath.Clamp(value, -FP._1, FP._1));
		}

		/// <summary>
		/// 返回两个向量之间的弧度 假定向量已归一化
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static FP RadiansSkipNormalize(FPVector2 a, FPVector2 b)
		{
			return FPMath.Acos(FPMath.Clamp(Dot(a, b), -FP._1, FP._1));
		}

		/// <summary>
		/// 返回两个向量之间的有符号弧度
		/// 当 <paramref name="b" /> 位于 <paramref name="a" /> 右侧时结果为负值
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static FP RadiansSigned(FPVector2 a, FPVector2 b)
		{
			FP fP = Radians(a, b);
			FP fP2 = FPMath.Sign(a.X * b.Y - a.Y * b.X);
			return fP * fP2;
		}

		/// <summary>
		/// 返回两个向量之间的有符号弧度
		/// 当 <paramref name="b" /> 位于 <paramref name="a" /> 右侧时结果为负值 假定向量已归一化
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static FP RadiansSignedSkipNormalize(FPVector2 a, FPVector2 b)
		{
			FP fP = RadiansSkipNormalize(a, b);
			FP fP2 = FPMath.Sign(a.X * b.Y - a.Y * b.X);
			return fP * fP2;
		}

		/// <summary>
		/// 在 <paramref name="start" /> 与 <paramref name="end" /> 之间进行平滑插值
		/// 对每个分量调用 <see cref="M:Photon.Deterministic.FPMath.SmoothStep(Photon.Deterministic.FP,Photon.Deterministic.FP,Photon.Deterministic.FP)" />
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static FPVector2 SmoothStep(FPVector2 start, FPVector2 end, FP t)
		{
			return new FPVector2(FPMath.SmoothStep(start.X, end.X, t), FPMath.SmoothStep(start.Y, end.Y, t));
		}

		/// <summary>
		/// 对每个分量调用 <see cref="M:Photon.Deterministic.FPMath.Hermite(Photon.Deterministic.FP,Photon.Deterministic.FP,Photon.Deterministic.FP,Photon.Deterministic.FP,Photon.Deterministic.FP)" />
		/// </summary>
		/// <param name="value1"></param>
		/// <param name="tangent1"></param>
		/// <param name="value2"></param>
		/// <param name="tangent2"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static FPVector2 Hermite(FPVector2 value1, FPVector2 tangent1, FPVector2 value2, FPVector2 tangent2, FP t)
		{
			FPVector2 result = default(FPVector2);
			result.X = FPMath.Hermite(value1.X, tangent1.X, value2.X, tangent2.X, t);
			result.Y = FPMath.Hermite(value1.Y, tangent1.Y, value2.Y, tangent2.Y, t);
			return result;
		}

		/// <summary>
		/// 对每个分量调用 <see cref="M:Photon.Deterministic.FPMath.Barycentric(Photon.Deterministic.FP,Photon.Deterministic.FP,Photon.Deterministic.FP,Photon.Deterministic.FP,Photon.Deterministic.FP)" />
		/// </summary>
		/// <param name="value1"></param>
		/// <param name="value2"></param>
		/// <param name="value3"></param>
		/// <param name="t1"></param>
		/// <param name="t2"></param>
		/// <returns></returns>
		public static FPVector2 Barycentric(FPVector2 value1, FPVector2 value2, FPVector2 value3, FP t1, FP t2)
		{
			return new FPVector2(FPMath.Barycentric(value1.X, value2.X, value3.X, t1, t2), FPMath.Barycentric(value1.Y, value2.Y, value3.Y, t1, t2));
		}

		/// <summary>
		/// 对每个分量调用 <see cref="M:Photon.Deterministic.FPMath.CatmullRom(Photon.Deterministic.FP,Photon.Deterministic.FP,Photon.Deterministic.FP,Photon.Deterministic.FP,Photon.Deterministic.FP)" />
		/// </summary>
		/// <param name="value1"></param>
		/// <param name="value2"></param>
		/// <param name="value3"></param>
		/// <param name="value4"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static FPVector2 CatmullRom(FPVector2 value1, FPVector2 value2, FPVector2 value3, FPVector2 value4, FP t)
		{
			return new FPVector2(FPMath.CatmullRom(value1.X, value2.X, value3.X, value4.X, t), FPMath.CatmullRom(value1.Y, value2.Y, value3.Y, value4.Y, t));
		}

		/// <summary>
		/// 如果 <paramref name="vertices" /> 定义的多边形为凸多边形则返回 <see langword="true" />
		/// </summary>
		/// <param name="vertices"></param>
		/// <returns></returns>
		public static bool IsPolygonConvex(FPVector2[] vertices)
		{
			bool flag = false;
			bool flag2 = false;
			for (int i = 0; i < vertices.Length; i++)
			{
				int num = (i + 1) % vertices.Length;
				int num2 = (num + 1) % vertices.Length;
				FP fP = CrossProductLength(vertices[i], vertices[num], vertices[num2]);
				if (fP < 0)
				{
					flag = true;
				}
				else if (fP > 0)
				{
					flag2 = true;
				}
				if (flag && flag2)
				{
					return false;
				}
			}
			return true;
		}

		private static FP CrossProductLength(FPVector2 A, FPVector2 B, FPVector2 C)
		{
			FP fP = A.X - B.X;
			FP fP2 = A.Y - B.Y;
			FP fP3 = C.X - B.X;
			FP fP4 = C.Y - B.Y;
			return fP * fP4 - fP2 * fP3;
		}

		/// <summary>
		/// 检查多边形顶点是否按顺时针排列
		/// </summary>
		/// <param name="vertices">多边形的顶点</param>
		/// <returns>顶点按顺时针排列时返回 <see langword="true" /></returns>
		public static bool IsClockWise(FPVector2[] vertices)
		{
			FPVector2 fPVector = new FPVector2(vertices[1].X - vertices[0].X, vertices[1].Y - vertices[0].Y);
			FPVector2 fPVector2 = new FPVector2(vertices[2].X - vertices[1].X, vertices[2].Y - vertices[1].Y);
			return fPVector.X * fPVector2.Y - fPVector.Y * fPVector2.X < 0;
		}

		/// <summary>
		/// 检查多边形顶点是否按逆时针排列
		/// </summary>
		/// <param name="vertices">多边形的顶点</param>
		/// <returns>顶点按逆时针排列时返回 <see langword="true" /></returns>
		public static bool IsCounterClockWise(FPVector2[] vertices)
		{
			return !IsClockWise(vertices);
		}

		/// <summary>
		/// 检查多边形顶点是否按顺时针排列如果不是则调整为逆时针排列
		/// </summary>
		/// <param name="vertices">多边形的顶点</param>
		public static void MakeCounterClockWise(FPVector2[] vertices)
		{
			if (IsClockWise(vertices))
			{
				FlipWindingOrder(vertices);
			}
		}

		/// <summary>
		/// 当顶点按逆时针排列时反转绕序 确保顶点按顺时针排列
		/// </summary>
		/// <param name="vertices">数组的顶点</param>
		public static void MakeClockWise(FPVector2[] vertices)
		{
			if (IsCounterClockWise(vertices))
			{
				FlipWindingOrder(vertices);
			}
		}

		/// <summary>
		/// 反转数组中的顶点顺序 从而翻转多边形绕序
		/// </summary>
		/// <param name="vertices">数组的顶点表示多边形</param>
		public static void FlipWindingOrder(FPVector2[] vertices)
		{
			Array.Reverse(vertices);
		}

		/// <summary>
		/// 计算 <paramref name="vertices" /> 定义的多边形每条边的法线
		/// </summary>
		/// <param name="vertices"></param>
		/// <returns></returns>
		public static FPVector2[] CalculatePolygonNormals(FPVector2[] vertices)
		{
			FPVector2[] array = new FPVector2[vertices.Length];
			for (int i = 0; i < vertices.Length; i++)
			{
				int num = ((i + 1 < vertices.Length) ? (i + 1) : 0);
				FPVector2 fPVector = vertices[num] - vertices[i];
				if (fPVector.X.RawValue == 0L)
				{
					_ = fPVector.Y.RawValue;
				}
				array[i] = new FPVector2(fPVector.Y, -fPVector.X);
				array[i] = array[i].Normalized;
			}
			return array;
		}

		/// <summary>
		/// 如果 <paramref name="vertices" /> 定义的多边形所有法线均非零则返回 <see langword="true" />
		/// </summary>
		/// <param name="vertices"></param>
		/// <returns></returns>
		public static bool PolygonNormalsAreValid(FPVector2[] vertices)
		{
			for (int i = 0; i < vertices.Length; i++)
			{
				int num = (i + 1) % vertices.Length;
				if (vertices[num].X.RawValue == vertices[i].X.RawValue && vertices[num].Y.RawValue == vertices[i].Y.RawValue)
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// 平移<paramref name="vertices" />定义的多边形使0 0成为其中心
		/// </summary>
		/// <param name="vertices"></param>
		/// <returns></returns>
		public static FPVector2[] RecenterPolygon(FPVector2[] vertices)
		{
			FPVector2 fPVector = CalculatePolygonCentroid(vertices);
			FPVector2[] array = (FPVector2[])vertices.Clone();
			for (int i = 0; i < vertices.Length; i++)
			{
				array[i] = vertices[i] - fPVector;
			}
			return array;
		}

		/// <summary>
		/// 返回 <paramref name="vertices" /> 定义的多边形面积
		/// </summary>
		/// <param name="vertices"></param>
		/// <returns></returns>
		public static FP CalculatePolygonArea(FPVector2[] vertices)
		{
			FP _ = FP._0;
			for (int i = 0; i < vertices.Length; i++)
			{
				FPVector2 fPVector = vertices[(i + 1) % vertices.Length];
				_ += vertices[i].X * fPVector.Y - vertices[i].Y * fPVector.X;
			}
			return _ / 2;
		}

		/// <summary>
		/// 返回 <paramref name="vertices" /> 定义的多边形质心
		/// </summary>
		/// <param name="vertices"></param>
		/// <returns></returns>
		public static FPVector2 CalculatePolygonCentroid(FPVector2[] vertices)
		{
			FPVector2 result = default(FPVector2);
			FP _ = FP._0;
			FP _2 = FP._0;
			FP _3 = FP._0;
			FP _4 = FP._0;
			FP _5 = FP._0;
			FP _6 = FP._0;
			for (int i = 0; i < vertices.Length; i++)
			{
				_2 = vertices[i].X;
				_3 = vertices[i].Y;
				_4 = vertices[(i + 1) % vertices.Length].X;
				_5 = vertices[(i + 1) % vertices.Length].Y;
				_6 = _2 * _5 - _4 * _3;
				_ += _6;
				result.X += (_2 + _4) * _6;
				result.Y += (_3 + _5) * _6;
			}
			if (_ == FP._0)
			{
				throw new ArgumentException("Not a valid polygon");
			}
			_ *= FP._0_50;
			result.X /= FP._6 * _;
			result.Y /= FP._6 * _;
			return result;
		}

		/// <summary>
		/// 计算 <paramref name="vertices" /> 定义的多边形转动惯量因子
		/// </summary>
		/// <remarks>计算刚体质量转动惯量时将因子乘以刚体质量</remarks>
		/// <param name="vertices">2D顶点定义多边形</param>
		/// <returns>多边形的转动惯量因子</returns>
		public static FP CalculatePolygonInertiaFactor(FPVector2[] vertices)
		{
			FP result = default(FP);
			long num = 0L;
			for (int i = 0; i < vertices.Length; i++)
			{
				FPVector2 fPVector = vertices[i];
				FPVector2 fPVector2 = vertices[(i + 1) % vertices.Length];
				long num2 = (fPVector.X.RawValue * fPVector2.Y.RawValue + 32768 >> 16) - (fPVector.Y.RawValue * fPVector2.X.RawValue + 32768 >> 16);
				num += num2;
				long num3 = (fPVector.X.RawValue * fPVector.X.RawValue + 32768 >> 16) + (fPVector.Y.RawValue * fPVector.Y.RawValue + 32768 >> 16);
				long num4 = (fPVector2.X.RawValue * fPVector2.X.RawValue + 32768 >> 16) + (fPVector2.Y.RawValue * fPVector2.Y.RawValue + 32768 >> 16);
				long num5 = (fPVector.X.RawValue * fPVector2.X.RawValue + 32768 >> 16) + (fPVector.Y.RawValue * fPVector2.Y.RawValue + 32768 >> 16);
				result.RawValue += num2 * (num3 + num4 + num5) + 32768 >> 16;
			}
			result.RawValue = (result.RawValue << 16) / (num * FP._6.RawValue + 32768 >> 16);
			return result;
		}

		/// <summary>
		/// 计算 <paramref name="vertices" /> 定义的多边形在 <paramref name="localDir" /> 方向上的支撑点
		/// <remarks>支撑点是形状在给定方向上最远的点</remarks>
		/// <remarks>支撑点和方向均使用多边形局部空间</remarks>
		/// <remarks>多边形顶点应按逆时针排列</remarks>
		/// </summary>
		/// <param name="vertices">2D顶点定义多边形</param>
		/// <param name="localDir">将在局部空间中计算支撑点的方向</param>
		/// <returns>支撑点 在局部空间</returns>
		public static FPVector2 CalculatePolygonLocalSupport(FPVector2[] vertices, ref FPVector2 localDir)
		{
			FPVector2 fPVector = vertices[0];
			FPVector2 result = fPVector;
			long rawValue = Dot(fPVector, localDir).RawValue;
			fPVector = vertices[1];
			long rawValue2 = Dot(fPVector, localDir).RawValue;
			if (rawValue2 > rawValue)
			{
				result = fPVector;
				rawValue = rawValue2;
				for (int i = 2; i < vertices.Length; i++)
				{
					fPVector = vertices[i];
					rawValue2 = Dot(fPVector, localDir).RawValue;
					if (rawValue2 <= rawValue)
					{
						break;
					}
					result = fPVector;
					rawValue = rawValue2;
				}
			}
			else
			{
				fPVector = vertices[^1];
				rawValue2 = Dot(fPVector, localDir).RawValue;
				if (rawValue2 > rawValue)
				{
					result = fPVector;
					rawValue = rawValue2;
					for (int num = vertices.Length - 2; num > 1; num--)
					{
						fPVector = vertices[num];
						rawValue2 = Dot(fPVector, localDir).RawValue;
						if (rawValue2 <= rawValue)
						{
							break;
						}
						result = fPVector;
						rawValue = rawValue2;
					}
				}
			}
			return result;
		}

		/// <summary>
		/// 计算多边形在给定方向上的局部支撑点
		/// </summary>
		/// <param name="vertices">组成多边形的顶点数组</param>
		/// <param name="verticesCount">数值的顶点在多边形</param>
		/// <param name="localDir">用于查找局部支撑点的方向</param>
		/// <returns>多边形在给定方向上的局部支撑点</returns>
		public unsafe static FPVector2 CalculatePolygonLocalSupport(FPVector2* vertices, int verticesCount, ref FPVector2 localDir)
		{
			FPVector2 fPVector = *vertices;
			FPVector2 result = fPVector;
			long rawValue = Dot(fPVector, localDir).RawValue;
			fPVector = vertices[1];
			long rawValue2 = Dot(fPVector, localDir).RawValue;
			if (rawValue2 > rawValue)
			{
				result = fPVector;
				rawValue = rawValue2;
				for (int i = 2; i < verticesCount; i++)
				{
					fPVector = vertices[i];
					rawValue2 = Dot(fPVector, localDir).RawValue;
					if (rawValue2 <= rawValue)
					{
						break;
					}
					result = fPVector;
					rawValue = rawValue2;
				}
			}
			else
			{
				fPVector = vertices[verticesCount - 1];
				rawValue2 = Dot(fPVector, localDir).RawValue;
				if (rawValue2 > rawValue)
				{
					result = fPVector;
					rawValue = rawValue2;
					for (int num = verticesCount - 2; num > 1; num--)
					{
						fPVector = vertices[num];
						rawValue2 = Dot(fPVector, localDir).RawValue;
						if (rawValue2 <= rawValue)
						{
							break;
						}
						result = fPVector;
						rawValue = rawValue2;
					}
				}
			}
			return result;
		}

		/// <summary>
		/// 返回由<paramref name="vertices" />定义的居中多边形半径
		/// </summary>
		/// <param name="vertices"></param>
		/// <returns></returns>
		public static FP CalculatePolygonRadius(FPVector2[] vertices)
		{
			FP fP = FP._0;
			for (int i = 0; i < vertices.Length; i++)
			{
				FP magnitude = vertices[i].Magnitude;
				if (magnitude > fP)
				{
					fP = magnitude;
				}
			}
			return fP;
		}

		/// <summary>
		/// 计算从 <paramref name="from" /> 向 <paramref name="to" /> 移动后的位置 移动距离不超过 <paramref name="maxDelta" />
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <param name="maxDelta"></param>
		/// <returns></returns>值
		public static FPVector2 MoveTowards(FPVector2 from, FPVector2 to, FP maxDelta)
		{
			FPVector2 fPVector = to - from;
			FP magnitude = fPVector.Magnitude;
			if (magnitude.RawValue <= maxDelta.RawValue || magnitude.RawValue == 0L)
			{
				return to;
			}
			return from + fPVector / magnitude * maxDelta;
		}

		/// <summary>
		/// 如果两个向量完全相等则返回 <see langword="true" />
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FPVector2 a, FPVector2 b)
		{
			if (a.X.RawValue == b.X.RawValue)
			{
				return a.Y.RawValue == b.Y.RawValue;
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
		public static bool operator !=(FPVector2 a, FPVector2 b)
		{
			if (a.X.RawValue == b.X.RawValue)
			{
				return a.Y.RawValue != b.Y.RawValue;
			}
			return true;
		}

		/// <summary>
		/// 取反每个分量的<paramref name="v" />向量
		/// </summary>
		/// <param name="v"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPVector2 operator -(FPVector2 v)
		{
			v.X.RawValue = -v.X.RawValue;
			v.Y.RawValue = -v.Y.RawValue;
			return v;
		}

		/// <summary>
		/// 加上两个向量
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPVector2 operator +(FPVector2 a, FPVector2 b)
		{
			a.X.RawValue += b.X.RawValue;
			a.Y.RawValue += b.Y.RawValue;
			return a;
		}

		/// <summary>
		/// 从 <paramref name="a" /> 中减去 <paramref name="b" />
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPVector2 operator -(FPVector2 a, FPVector2 b)
		{
			a.X.RawValue -= b.X.RawValue;
			a.Y.RawValue -= b.Y.RawValue;
			return a;
		}

		/// <summary>
		/// 乘以每个分量的<paramref name="v" />乘以<paramref name="s" />
		/// </summary>
		/// <param name="v"></param>
		/// <param name="s"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPVector2 operator *(FPVector2 v, FP s)
		{
			v.X.RawValue = v.X.RawValue * s.RawValue + 32768 >> 16;
			v.Y.RawValue = v.Y.RawValue * s.RawValue + 32768 >> 16;
			return v;
		}

		/// <summary>
		/// 乘以每个分量的<paramref name="v" />乘以<paramref name="s" />
		/// </summary>
		/// <param name="v"></param>
		/// <param name="s"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPVector2 operator *(FP s, FPVector2 v)
		{
			v.X.RawValue = v.X.RawValue * s.RawValue + 32768 >> 16;
			v.Y.RawValue = v.Y.RawValue * s.RawValue + 32768 >> 16;
			return v;
		}

		/// <summary>
		/// 乘以每个分量的<paramref name="v" />乘以<paramref name="s" />
		/// </summary>
		/// <param name="v"></param>
		/// <param name="s"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPVector2 operator *(FPVector2 v, int s)
		{
			v.X.RawValue = v.X.RawValue * s;
			v.Y.RawValue = v.Y.RawValue * s;
			return v;
		}

		/// <summary>
		/// 乘以每个分量的<paramref name="v" />乘以<paramref name="s" />
		/// </summary>
		/// <param name="v"></param>
		/// <param name="s"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPVector2 operator *(int s, FPVector2 v)
		{
			v.X.RawValue = v.X.RawValue * s;
			v.Y.RawValue = v.Y.RawValue * s;
			return v;
		}

		/// <summary>
		/// 将 <paramref name="v" /> 的每个分量除以 <paramref name="s" />
		/// </summary>
		/// <param name="v"></param>
		/// <param name="s"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPVector2 operator /(FPVector2 v, FP s)
		{
			v.X.RawValue = (v.X.RawValue << 16) / s.RawValue;
			v.Y.RawValue = (v.Y.RawValue << 16) / s.RawValue;
			return v;
		}

		/// <summary>
		/// 将 <paramref name="v" /> 的每个分量除以 <paramref name="s" />
		/// </summary>
		/// <param name="v"></param>
		/// <param name="s"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPVector2 operator /(FPVector2 v, int s)
		{
			v.X.RawValue = v.X.RawValue / s;
			v.Y.RawValue = v.Y.RawValue / s;
			return v;
		}
	}
}

