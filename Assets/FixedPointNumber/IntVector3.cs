using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Photon.Deterministic
{
	/// <summary>
	/// 表示使用整数分量的三维向量
	/// </summary>
	/// \ingroup MathApi
	[Serializable]
	[StructLayout(LayoutKind.Explicit)]
	public struct IntVector3 : IEquatable<IntVector3>
	{
		/// <summary>
		/// 用于比较 IntVector3 对象是否相等
		/// </summary>
		public class EqualityComparer : IEqualityComparer<IntVector3>
		{
			/// <summary>
			/// 全局相等比较器实例
			/// </summary>
			public static readonly EqualityComparer Instance = new EqualityComparer();

			private EqualityComparer()
			{
			}

			public bool Equals(IntVector3 x, IntVector3 y)
			{
				return x == y;
			}

			public int GetHashCode(IntVector3 obj)
			{
				return obj.GetHashCode();
			}
		}

		/// <summary>
		/// 该结构体占用的内存大小
		/// </summary>
		public const int SIZE = 12;

		/// <summary>
		/// 向量的 x 分量
		/// </summary>
		[FieldOffset(0)]
		public int X;

		/// <summary>
		/// 向量的 y 分量
		/// </summary>
		[FieldOffset(4)]
		public int Y;

		/// <summary>
		/// 向量的 z 分量
		/// </summary>
		[FieldOffset(8)]
		public int Z;

		/// <summary>
		/// 所有分量均为 int MaxValue 的向量
		/// </summary>
		public static IntVector3 MaxValue => new IntVector3(int.MaxValue, int.MaxValue, int.MaxValue);

		/// <summary>
		/// 所有分量均为 int MinValue 的向量
		/// </summary>
		public static IntVector3 MinValue => new IntVector3(int.MinValue, int.MinValue, int.MinValue);

		/// <summary>
		/// 表示 x 为 0 y 为 1 z 为 0 的上方向向量
		/// </summary>
		public static IntVector3 Up => new IntVector3(0, 1, 0);

		/// <summary>
		/// 表示向下方向使用坐标 (0, -1, 0)
		/// </summary>
		public static IntVector3 Down => new IntVector3(0, -1, 0);

		/// <summary>
		/// 表示使用整数分量的三维向量
		/// </summary>
		/// <remarks>
		/// 此结构体属于 <see cref="N:Photon.Deterministic" /> 命名空间
		/// </remarks>
		public static IntVector3 Left => new IntVector3(-1, 0, 0);

		/// <summary>
		/// 右方向向量 (1, 0, 0)
		/// </summary>
		/// <value>右方向向量</value>
		public static IntVector3 Right => new IntVector3(1, 0, 0);

		/// <summary>
		/// 所有分量均为 1 的向量
		/// </summary>
		public static IntVector3 One => new IntVector3(1, 1, 1);

		/// <summary>
		/// 所有分量均为 0 的零向量
		/// </summary>
		public static IntVector3 Zero => new IntVector3(0, 0, 0);

		/// <summary>
		/// 计算 IntVector3 的模长
		/// </summary>
		/// <value>
		/// IntVector3的模长
		/// </value>
		public readonly FP Magnitude
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return FPMath.Sqrt(X * X + Y * Y + Z * Z);
			}
		}

		/// <summary>
		/// 获取向量模长的平方
		/// </summary>
		/// <value>向量模长的平方</value>
		public readonly int SqrMagnitude
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return X * X + Y * Y + Z * Z;
			}
		}

		public readonly IntVector3 XXX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = X;
				result.Y = X;
				result.Z = X;
				return result;
			}
		}

		public readonly IntVector3 XXY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = X;
				result.Y = X;
				result.Z = Y;
				return result;
			}
		}

		public readonly IntVector3 XXZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = X;
				result.Y = X;
				result.Z = Z;
				return result;
			}
		}

		public readonly IntVector3 XYX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = X;
				result.Y = Y;
				result.Z = X;
				return result;
			}
		}

		public readonly IntVector3 XYY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = X;
				result.Y = Y;
				result.Z = Y;
				return result;
			}
		}

		public readonly IntVector3 XYZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = X;
				result.Y = Y;
				result.Z = Z;
				return result;
			}
		}

		public readonly IntVector3 XZX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = X;
				result.Y = Z;
				result.Z = X;
				return result;
			}
		}

		public readonly IntVector3 XZY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = X;
				result.Y = Z;
				result.Z = Y;
				return result;
			}
		}

		public readonly IntVector3 XZZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = X;
				result.Y = Z;
				result.Z = Z;
				return result;
			}
		}

		public readonly IntVector2 XX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector2 result = default(IntVector2);
				result.X = X;
				result.Y = X;
				return result;
			}
		}

		public readonly IntVector2 XY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector2 result = default(IntVector2);
				result.X = X;
				result.Y = Y;
				return result;
			}
		}

		public readonly IntVector2 XZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector2 result = default(IntVector2);
				result.X = X;
				result.Y = Z;
				return result;
			}
		}

		public readonly IntVector3 YYY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Y;
				result.Y = Y;
				result.Z = Y;
				return result;
			}
		}

		public readonly IntVector3 YYZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Y;
				result.Y = Y;
				result.Z = Z;
				return result;
			}
		}

		public readonly IntVector3 YYX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Y;
				result.Y = Y;
				result.Z = X;
				return result;
			}
		}

		public readonly IntVector3 YZY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Y;
				result.Y = Z;
				result.Z = Y;
				return result;
			}
		}

		public readonly IntVector3 YZZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Y;
				result.Y = Z;
				result.Z = Z;
				return result;
			}
		}

		public readonly IntVector3 YZX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Y;
				result.Y = Z;
				result.Z = X;
				return result;
			}
		}

		public readonly IntVector3 YXY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Y;
				result.Y = X;
				result.Z = Y;
				return result;
			}
		}

		public readonly IntVector3 YXZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Y;
				result.Y = X;
				result.Z = Z;
				return result;
			}
		}

		public readonly IntVector3 YXX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Y;
				result.Y = X;
				result.Z = X;
				return result;
			}
		}

		public readonly IntVector2 YY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector2 result = default(IntVector2);
				result.X = Y;
				result.Y = Y;
				return result;
			}
		}

		public readonly IntVector2 YZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector2 result = default(IntVector2);
				result.X = Y;
				result.Y = Z;
				return result;
			}
		}

		public readonly IntVector2 YX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector2 result = default(IntVector2);
				result.X = Y;
				result.Y = X;
				return result;
			}
		}

		public readonly IntVector3 ZZZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Z;
				result.Y = Z;
				result.Z = Z;
				return result;
			}
		}

		public readonly IntVector3 ZZX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Z;
				result.Y = Z;
				result.Z = X;
				return result;
			}
		}

		public readonly IntVector3 ZZY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Z;
				result.Y = Z;
				result.Z = Y;
				return result;
			}
		}

		public readonly IntVector3 ZXZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Z;
				result.Y = X;
				result.Z = Z;
				return result;
			}
		}

		public readonly IntVector3 ZXX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Z;
				result.Y = X;
				result.Z = X;
				return result;
			}
		}

		public readonly IntVector3 ZXY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Z;
				result.Y = X;
				result.Z = Y;
				return result;
			}
		}

		public readonly IntVector3 ZYZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Z;
				result.Y = Y;
				result.Z = Z;
				return result;
			}
		}

		public readonly IntVector3 ZYX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Z;
				result.Y = Y;
				result.Z = X;
				return result;
			}
		}

		public readonly IntVector3 ZYY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector3 result = default(IntVector3);
				result.X = Z;
				result.Y = Y;
				result.Z = Y;
				return result;
			}
		}

		public readonly IntVector2 ZZ
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector2 result = default(IntVector2);
				result.X = Z;
				result.Y = Z;
				return result;
			}
		}

		public readonly IntVector2 ZX
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector2 result = default(IntVector2);
				result.X = Z;
				result.Y = X;
				return result;
			}
		}

		public readonly IntVector2 ZY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				IntVector2 result = default(IntVector2);
				result.X = Z;
				result.Y = Y;
				return result;
			}
		}

		/// <summary>
		/// 使用给定分量创建新的 IntVector3
		/// </summary>
		public IntVector3(int x, int y, int z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		/// <summary>
		/// 返回向量的哈希码
		/// </summary>
		public override readonly int GetHashCode()
		{
			int num = 17;
			num = num * 31 + X.GetHashCode();
			num = num * 31 + Y.GetHashCode();
			return num * 31 + Z.GetHashCode();
		}

		/// <summary>
		/// 返回表示以下对象的字符串IntVector3
		/// </summary>
		/// <returns>IntVector3的字符串表示</returns>
		public override readonly string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "({0}, {1}, {2})", X, Y, Z);
		}

		/// <summary>
		/// 使用IDeterministicFrameSerializer将IntVector3序列化到流
		/// 序列化器处于写入模式时 将向量的 x y z 分量写入流
		/// 序列化器处于读取模式时 从流中读取 x y z 分量并写入向量
		/// </summary>
		/// <param name="ptr">正在序列化的IntVector3指针</param>
		/// <param name="serializer">用于序列化的IDeterministicFrameSerializer</param>
		public unsafe static void Serialize(void* ptr, IDeterministicFrameSerializer serializer)
		{
			if (serializer.Writing)
			{
				serializer.Stream.WriteInt(((IntVector3*)ptr)->X);
				serializer.Stream.WriteInt(((IntVector3*)ptr)->Y);
				serializer.Stream.WriteInt(((IntVector3*)ptr)->Z);
			}
			else
			{
				((IntVector3*)ptr)->X = serializer.Stream.ReadInt();
				((IntVector3*)ptr)->Y = serializer.Stream.ReadInt();
				((IntVector3*)ptr)->Z = serializer.Stream.ReadInt();
			}
		}

		/// <summary>
		/// 计算两个 IntVector3 点之间的距离
		/// </summary>
		/// <param name="a">第一个IntVector3点</param>
		/// <param name="b">第二个IntVector3点</param>
		/// <returns>两个IntVector3点之间的距离</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP Distance(IntVector3 a, IntVector3 b)
		{
			return (a - b).Magnitude;
		}

		/// <summary>
		/// 将IntVector3值限制在最小和最大IntVector3值之间
		/// </summary>
		/// <param name="value">待限制的IntVector3值</param>
		/// <param name="min">最小IntVector3值</param>
		/// <param name="max">最大IntVector3值</param>
		/// <returns>限制后的IntVector3值</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector3 Clamp(IntVector3 value, IntVector3 min, IntVector3 max)
		{
			return new IntVector3(FPMath.Clamp(value.X, min.X, max.X), FPMath.Clamp(value.Y, min.Y, max.Y), FPMath.Clamp(value.Z, min.Z, max.Z));
		}

		/// <summary>
		/// 返回由输入向量的最小分量组成的新IntVector3
		/// </summary>
		/// <param name="a">第一个IntVector3</param>
		/// <param name="b">第二个IntVector3</param>
		/// <returns>由两个输入向量的较小分量组成的新 IntVector3</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector3 Min(IntVector3 a, IntVector3 b)
		{
			return new IntVector3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
		}

		/// <summary>
		/// 返回由输入向量的最大分量组成的新IntVector3
		/// </summary>
		/// <param name="a">第一个IntVector3</param>
		/// <param name="b">第二个IntVector3</param>
		/// <returns>由两个输入向量的较大分量组成的新 IntVector3</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector3 Max(IntVector3 a, IntVector3 b)
		{
			return new IntVector3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
		}

		/// <summary>
		/// 将给定 FPVector3 的各分量舍入到最接近的整数
		/// </summary>
		/// <param name="v">待舍入的向量</param>
		/// <returns>由舍入后整数分量组成的新 IntVector3</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector3 RoundToInt(FPVector3 v)
		{
			return new IntVector3(FPMath.RoundToInt(v.X), FPMath.RoundToInt(v.Y), FPMath.RoundToInt(v.Z));
		}

		/// <summary>
		/// 返回最大整数小于或等于指定浮点数值
		/// </summary>
		/// <param name="v">待向下取整的值</param>
		/// <returns>最大整数小于或等于指定数值</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector3 FloorToInt(FPVector3 v)
		{
			return new IntVector3(FPMath.FloorToInt(v.X), FPMath.FloorToInt(v.Y), FPMath.FloorToInt(v.Z));
		}

		/// <summary>
		/// 返回由输入向量各分量向上取整结果组成的新IntVector3
		/// </summary>
		/// <param name="v">输入的向量</param>
		/// <returns>由向上取整后分量组成的新 IntVector3</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector3 CeilToInt(FPVector3 v)
		{
			return new IntVector3(FPMath.CeilToInt(v.X), FPMath.CeilToInt(v.Y), FPMath.CeilToInt(v.Z));
		}

		/// <summary>
		/// 将IntVector3对象转换为FPVector3对象
		/// </summary>
		/// <param name="v">待转换的IntVector3对象</param>
		/// <returns>根据 IntVector3 各分量创建的新 FPVector3</returns>
		public static implicit operator FPVector3(IntVector3 v)
		{
			return new FPVector3(v.X, v.Y, v.Z);
		}

		/// <summary>
		/// 将FPVector3对象转换为IntVector3对象
		/// </summary>
		public static explicit operator IntVector3(FPVector3 v)
		{
			return new IntVector3(v.X.AsInt, v.Y.AsInt, v.Z.AsInt);
		}

		/// <summary>
		/// 如果两个向量完全相等则返回 <see langword="true" />
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(IntVector3 a, IntVector3 b)
		{
			if (a.X == b.X && a.Y == b.Y)
			{
				return a.Z == b.Z;
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
		public static bool operator !=(IntVector3 a, IntVector3 b)
		{
			if (a.X == b.X && a.Y == b.Y)
			{
				return a.Z != b.Z;
			}
			return true;
		}

		/// <summary>
		/// 取反每个分量的<paramref name="v" />向量
		/// </summary>
		/// <param name="v"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector3 operator -(IntVector3 v)
		{
			v.X = -v.X;
			v.Y = -v.Y;
			v.Z = -v.Z;
			return v;
		}

		/// <summary>
		/// 乘以每个分量的<paramref name="v" />乘以<paramref name="s" />
		/// </summary>
		/// <param name="v"></param>
		/// <param name="s"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector3 operator *(IntVector3 v, int s)
		{
			v.X *= s;
			v.Y *= s;
			v.Z *= s;
			return v;
		}

		/// <summary>
		/// 乘以每个分量的<paramref name="v" />乘以<paramref name="s" />
		/// </summary>
		/// <param name="v"></param>
		/// <param name="s"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector3 operator *(int s, IntVector3 v)
		{
			v.X *= s;
			v.Y *= s;
			v.Z *= s;
			return v;
		}

		/// <summary>
		/// 将 <paramref name="v" /> 的每个分量除以 <paramref name="s" />
		/// </summary>
		/// <param name="v"></param>
		/// <param name="s"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector3 operator /(IntVector3 v, int s)
		{
			v.X /= s;
			v.Y /= s;
			v.Z /= s;
			return v;
		}

		/// <summary>
		/// 从 <paramref name="a" /> 中减去 <paramref name="b" />
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector3 operator -(IntVector3 a, IntVector3 b)
		{
			return new IntVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
		}

		/// <summary>
		/// 加上两个向量
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector3 operator +(IntVector3 a, IntVector3 b)
		{
			return new IntVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
		}

		/// <summary>
		/// 如果向量与另一个向量完全相等则返回true
		/// </summary>
		public readonly bool Equals(IntVector3 other)
		{
			if (X == other.X && Y == other.Y)
			{
				return Z == other.Z;
			}
			return false;
		}

		/// <inheritdoc cref="M:Photon.Deterministic.IntVector3.Equals(Photon.Deterministic.IntVector3)" />
		public override readonly bool Equals(object obj)
		{
			if (obj is IntVector3 other)
			{
				return Equals(other);
			}
			return false;
		}
	}
}

