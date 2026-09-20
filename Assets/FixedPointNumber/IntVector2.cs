using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace Photon.Deterministic
{
	/// <summary>
	/// 表示使用整数分量的二维向量
	/// </summary>
	/// \ingroup MathApi
	[Serializable]
	[StructLayout(LayoutKind.Explicit)]
	public struct IntVector2 : IEquatable<IntVector2>
	{
		/// <summary>
		/// 用于比较 IntVector2 对象是否相等
		/// </summary>
		public class EqualityComparer : IEqualityComparer<IntVector2>
		{
			/// <summary>
			/// 全局相等比较器实例
			/// </summary>
			public static readonly EqualityComparer Instance = new EqualityComparer();
			private EqualityComparer()
			{
			}
			/// <summary>
			/// 判断是否当前实例等于指定对象
			/// </summary>
			public bool Equals(IntVector2 x, IntVector2 y)
			{
				return x == y;
			}
			/// <summary>
			/// 计算哈希码用于IntVector2
			/// </summary>
			/// <returns>
			/// 计算得到的哈希码
			/// </returns>
			public int GetHashCode(IntVector2 obj)
			{
				return obj.GetHashCode();
			}
		}
		/// <summary>
		/// 结构体占用的内存大小
		/// </summary>
		public const int SIZE = 8;
		/// <summary>向量的 x 分量</summary>
		[FieldOffset(0)]
		public int X;
		/// <summary>向量的 y 分量</summary>
		[FieldOffset(4)]
		public int Y;
		/// <summary>
		/// 分量为 (0,0)
		/// </summary>
		public static IntVector2 Zero => new IntVector2(0, 0);
		/// <summary>
		/// 分量为 (1,1)
		/// </summary>
		public static IntVector2 One => new IntVector2(1, 1);
		/// <summary>
		/// 分量为 (1,0)
		/// </summary>
		public static IntVector2 Right => new IntVector2(1, 0);
		/// <summary>
		/// 分量为 (-1,0)
		/// </summary>
		public static IntVector2 Left => new IntVector2(-1, 0);
		/// <summary>
		/// 分量为 (0,1)
		/// </summary>
		public static IntVector2 Up => new IntVector2(0, 1);
		/// <summary>
		/// 分量为 (0,-1)
		/// </summary>
		public static IntVector2 Down => new IntVector2(0, -1);
		/// <summary>
		/// 分量为 (int MaxValue int MaxValue)
		/// </summary>
		public static IntVector2 MaxValue => new IntVector2(int.MaxValue, int.MaxValue);
		/// <summary>
		/// 分量为 (int MinValue int MinValue)
		/// </summary>
		public static IntVector2 MinValue => new IntVector2(int.MinValue, int.MinValue);
		/// <summary>
		/// 返回向量 (x 0, y)
		/// </summary>
		public readonly IntVector3 XOY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return new IntVector3(X, 0, Y);
			}
		}
		/// <summary>
		/// 返回向量 (x y 0)
		/// </summary>
		public readonly IntVector3 XYO
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return new IntVector3(X, Y, 0);
			}
		}
		/// <summary>
		/// 返回向量 (0, x y)
		/// </summary>
		public readonly IntVector3 OXY
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return new IntVector3(0, X, Y);
			}
		}
		/// <summary>
		/// 获取向量的模长
		/// </summary>
		public readonly FP Magnitude
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return FPMath.Sqrt(X * X + Y * Y);
			}
		}
		/// <summary>
		/// 获取向量模长的平方
		/// </summary>
		public readonly int SqrMagnitude
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return X * X + Y * Y;
			}
		}
		/// <summary>
		/// 按 X X X 分量顺序创建新的 IntVector3
		/// </summary>
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
		/// <summary>
		/// 按 X X Y 分量顺序创建新的 IntVector3
		/// </summary>
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
		/// <summary>
		/// 按 X Y X 分量顺序创建新的 IntVector3
		/// </summary>
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
		/// <summary>
		/// 按 X Y Y 分量顺序创建新的 IntVector3
		/// </summary>
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
		/// <summary>
		/// 使用此向量的 X X 分量创建 IntVector2
		/// </summary>
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
		/// <summary>
		/// 按 X Y 分量顺序创建新的 IntVector2
		/// </summary>
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
		/// <summary>
		/// 按 Y Y Y 分量顺序创建新的 IntVector3
		/// </summary>
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
		/// <summary>
		/// 按 Y Y X 分量顺序创建新的 IntVector3
		/// </summary>
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
		/// <summary>
		/// 按 Y X Y 分量顺序创建新的 IntVector3
		/// </summary>
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
		/// <summary>
		/// 按 Y X X 分量顺序创建新的 IntVector3
		/// </summary>
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
		/// <summary>
		/// 按 Y Y 分量顺序创建新的 IntVector2
		/// </summary>
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
		/// <summary>
		/// 按 Y X 分量顺序创建新的 IntVector2
		/// </summary>
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
		/// <summary>
		/// 初始化 IntVector2 结构体的新实例
		/// </summary>
		/// <param name="x">向量的 x 坐标</param>
		/// <param name="y">向量的 y 坐标</param>
		public IntVector2(int x, int y)
		{
			X = x;
			Y = y;
		}
		/// <summary>
		/// 返回此实例的哈希码
		/// </summary>
		public override readonly int GetHashCode()
		{
			int num = 17;
			num = num * 31 + X.GetHashCode();
			return num * 31 + Y.GetHashCode();
		}
		/// <summary>
		/// 返回当前 IntVector2 对象的字符串表示
		/// </summary>
		/// <returns>表示当前IntVector2的字符串</returns>
		public override readonly string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "({0}, {1})", X, Y);
		}
		/// <summary>
		/// 使用提供的IDeterministicFrameSerializer对象序列化或反序列化IntVector2结构体
		/// </summary>
		/// <param name="ptr">用于序列化或反序列化IntVector2结构体的指针</param>
		/// <param name="serializer">用于序列化或反序列化的IDeterministicFrameSerializer对象</param>
		public unsafe static void Serialize(void* ptr, IDeterministicFrameSerializer serializer)
		{
			if (serializer.Writing)
			{
				serializer.Stream.WriteInt(((IntVector2*)ptr)->X);
				serializer.Stream.WriteInt(((IntVector2*)ptr)->Y);
			}
			else
			{
				((IntVector2*)ptr)->X = serializer.Stream.ReadInt();
				((IntVector2*)ptr)->Y = serializer.Stream.ReadInt();
			}
		}
		/// <summary>
		/// 将IntVector2值限制在最小值和最大值之间
		/// </summary>
		/// <param name="value">待限制的IntVector2值</param>
		/// <param name="min">限制范围的最小IntVector2值</param>
		/// <param name="max">限制范围的最大IntVector2值</param>
		/// <returns>限制后的IntVector2值</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector2 Clamp(IntVector2 value, IntVector2 min, IntVector2 max)
		{
			return new IntVector2(FPMath.Clamp(value.X, min.X, max.X), FPMath.Clamp(value.Y, min.Y, max.Y));
		}
		/// <summary>
		/// 计算两个 IntVector2 点之间的距离
		/// </summary>
		/// <param name="a">第一个IntVector2点</param>
		/// <param name="b">第二个IntVector2点</param>
		/// <returns>两个IntVector2点之间的距离</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP Distance(IntVector2 a, IntVector2 b)
		{
			return (a - b).Magnitude;
		}
		/// <summary>
		/// 返回由输入向量的最大分量组成的新IntVector2
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector2 Max(IntVector2 a, IntVector2 b)
		{
			return new IntVector2(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
		}
		/// <summary>
		/// 返回由输入向量的最小分量组成的新IntVector2
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector2 Min(IntVector2 a, IntVector2 b)
		{
			return new IntVector2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y));
		}
		/// <summary>
		/// 将 FPVector2 的分量舍入到最接近的整数并返回新的 IntVector2
		/// </summary>
		/// <param name="v">待舍入的FPVector2</param>
		/// <returns>由输入向量舍入后的分量组成的新 IntVector2</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector2 RoundToInt(FPVector2 v)
		{
			return new IntVector2(FPMath.RoundToInt(v.X), FPMath.RoundToInt(v.Y));
		}
		/// <summary>
		/// 返回最大整数小于或等于指定浮点数值
		/// </summary>
		/// <param name="v">浮点数</param>
		/// <returns>最大整数小于或等于<paramref name="v" /></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector2 FloorToInt(FPVector2 v)
		{
			return new IntVector2(FPMath.FloorToInt(v.X), FPMath.FloorToInt(v.Y));
		}
		/// <summary>
		/// 返回由给定FPVector2各分量向上取整结果组成的新IntVector2
		/// </summary>
		/// <param name="v">待向上取整的FPVector2</param>
		/// <returns>由输入向量各分量向上取整结果组成的新 IntVector2</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector2 CeilToInt(FPVector2 v)
		{
			return new IntVector2(FPMath.CeilToInt(v.X), FPMath.CeilToInt(v.Y));
		}
		/// <summary>
		/// 将 IntVector2 隐式转换为 FPVector2
		/// 新 FPVector2 的 x 与 y 坐标
		/// 取自输入的 IntVector2 实例
		/// </summary>
		/// <param name="v">待转换的IntVector2实例</param>
		/// <returns>使用输入IntVector2实例的X和Y坐标创建的新FPVector2实例</returns>
		public static implicit operator FPVector2(IntVector2 v)
		{
			return new FPVector2(v.X, v.Y);
		}
		/// <summary>
		/// 将FPVector2实例转换为IntVector2实例
		/// </summary>
		public static explicit operator IntVector2(FPVector2 v)
		{
			return new IntVector2(v.X.AsInt, v.Y.AsInt);
		}
		/// <summary>
		/// 加上两个IntVector2实例
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector2 operator +(IntVector2 a, IntVector2 b)
		{
			return new IntVector2(a.X + b.X, a.Y + b.Y);
		}
		/// <summary>
		/// 从第一个 IntVector2 中减去第二个 IntVector2
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector2 operator -(IntVector2 a, IntVector2 b)
		{
			return new IntVector2(a.X - b.X, a.Y - b.Y);
		}
		/// <summary>
		/// 将 IntVector2 与整数标量相乘
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector2 operator *(IntVector2 a, int d)
		{
			return new IntVector2(a.X * d, a.Y * d);
		}
		/// <summary>
		/// 将 IntVector2 与整数标量相乘
		/// </summary>
		/// <param name="d"></param>
		/// <param name="a"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector2 operator *(int d, IntVector2 a)
		{
			return new IntVector2(a.X * d, a.Y * d);
		}
		/// <summary>
		/// 将 IntVector2 除以整数标量
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector2 operator /(IntVector2 a, int d)
		{
			return new IntVector2(a.X / d, a.Y / d);
		}
		/// <summary>
		/// 比较两个IntVector2实例是否相等
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(IntVector2 lhs, IntVector2 rhs)
		{
			if (lhs.X == rhs.X)
			{
				return lhs.Y == rhs.Y;
			}
			return false;
		}
		/// <summary>
		/// 比较两个IntVector2实例是否不相等
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(IntVector2 lhs, IntVector2 rhs)
		{
			return !(lhs == rhs);
		}
		/// <summary>
		/// 取 IntVector2 的相反数
		/// </summary>
		/// <param name="a"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntVector2 operator -(IntVector2 a)
		{
			return new IntVector2(-a.X, -a.Y);
		}
		/// <summary>
		/// 判断指定IntVector2是否与当前IntVector2相等
		/// </summary>
		/// <param name="other">用于与当前IntVector2比较的IntVector2</param>
		/// <returns>指定IntVector2与当前IntVector2相等时为true否则为false</returns>
		public readonly bool Equals(IntVector2 other)
		{
			if (X == other.X)
			{
				return Y == other.Y;
			}
			return false;
		}
		/// <summary>
		/// 判断指定对象是否与当前IntVector2相等
		/// </summary>
		/// <param name="obj">用于与当前IntVector2比较的对象</param>
		/// <returns>指定对象与当前IntVector2相等时为true否则为false</returns>
		public override readonly bool Equals(object obj)
		{
			if (obj is IntVector2 other)
			{
				return Equals(other);
			}
			return false;
		}
	}
}

