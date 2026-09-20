using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Photon.Deterministic
{
	/// <summary>
	/// 定点数使用低16位表示小数部分使用高48位表示整数部分
	/// <para>提供数学运算以及不同数据类型之间转换的多种方法</para>
	/// <para>但是大多数内部代码和乘法运算符执行快速乘法
	/// 结果的整数部分最多使用32位且不会检测溢出
	/// 因此数值应保持在 <see cref="T:System.Int16" /> 范围内
	/// <seealso cref="P:Photon.Deterministic.FP.UseableMax" />
	/// <seealso cref="P:Photon.Deterministic.FP.UseableMin" /></para>
	/// </summary>
	/// \ingroup MathAPI
	/// <remarks>
	/// 小数部分的精度为5位
	/// 小数分数归一化因子为1E5
	/// FP对象的大小为8字节
	/// 原始值 1 等于 FPLut ONE
	/// 0的原始值为0
	/// 精度值等于FPLut PRECISION
	/// FP对象的位数等于长整数的大小即64位
	/// MulRound常量为0
	/// MulShift常量等于精度值
	/// MulShiftTrunc常量等于精度值
	/// UsesRoundedConstants常量根据PHOTONDETERMINISTIC_FP_OLD_CONSTANTS的值为<see langword="true" />或<see langword="false" />
	/// </remarks>
	/// <seealso cref="T:Photon.Deterministic.FPLut" />
	/// <see langword="true" />
	[Serializable]
	[StructLayout(LayoutKind.Explicit)]
	public struct FP : IEquatable<FP>, IComparable<FP>
	{
		/// <summary>
		/// 保存<see cref="T:Photon.Deterministic.FP" />常量在原始 (长整数) 形式
		/// </summary>
		public static class Raw
		{
			/// <summary>
			/// 最小 FP 单位不等于 0
			/// <para>最接近的双精度值 1.52587890625E-05</para>
			/// </summary>
			public const long SmallestNonZero = 1L;

			/// <summary>
			/// FP最小值但是超出<see cref="F:Photon.Deterministic.FP.Raw.UseableMin" />和<see cref="F:Photon.Deterministic.FP.Raw.UseableMax" />(包含边界的) 可以溢出当相乘
			/// <para>最接近的双精度值 -140737488355328</para>
			/// </summary>
			public const long MinValue = long.MinValue;

			/// <summary>
			/// FP最大值但是超出<see cref="F:Photon.Deterministic.FP.Raw.UseableMin" />和<see cref="F:Photon.Deterministic.FP.Raw.UseableMax" />(包含边界的) 可以溢出当相乘
			/// <para>最接近的双精度值 140737488355328</para>
			/// </summary>
			public const long MaxValue = long.MaxValue;

			/// <summary>
			/// 表示与自身相乘且不会溢出超出长整数范围的最大负FP数值
			/// <para>最接近的双精度值 -32768</para>
			/// </summary>
			public const long UseableMin = -2147483648L;

			/// <summary>
			/// 表示与自身相乘且不会溢出超出长整数范围的最大FP数值
			/// <para>最接近的双精度值 32767.9999847412</para>
			/// </summary>
			public const long UseableMax = 2147483647L;

			/// <summary>
			/// Pi数值
			/// <para>最接近的双精度值 3.14158630371094</para>
			/// </summary>
			public const long Pi = 205887L;

			/// <summary>
			/// 1/Pi
			/// <para>最接近的双精度值 0.318313598632813</para>
			/// </summary>
			public const long PiInv = 20861L;

			/// <summary>
			/// 2 * Pi
			/// <para>最接近的双精度值 6.28318786621094</para>
			/// </summary>
			public const long PiTimes2 = 411775L;

			/// <summary>
			/// Pi / 2
			/// <para>最接近的双精度值 1.57080078125</para>
			/// </summary>
			public const long PiOver2 = 102944L;

			/// <summary>
			/// 2 / Pi
			/// <para>最接近的双精度值 0.636627197265625</para>
			/// </summary>
			public const long PiOver2Inv = 41722L;

			/// <summary>
			/// Pi / 4
			/// <para>最接近的双精度值 0.785400390625</para>
			/// </summary>
			public const long PiOver4 = 51472L;

			/// <summary>
			/// 3 * Pi / 4
			/// <para>最接近的双精度值 2.356201171875</para>
			/// </summary>
			public const long Pi3Over4 = 154416L;

			/// <summary>
			/// 4 * Pi / 3
			/// <para>最接近的双精度值 4.18879699707031</para>
			/// </summary>
			public const long Pi4Over3 = 274517L;

			/// <summary>
			/// 角度转弧度的转换常量
			/// <para>最接近的双精度值 0.0174560546875</para>
			/// </summary>
			public const long Deg2Rad = 1144L;

			/// <summary>
			/// 弧度转角度的转换常量
			/// <para>最接近的双精度值 57.2957763671875</para>
			/// </summary>
			public const long Rad2Deg = 3754936L;

			/// <summary>
			/// 表示数值0
			/// <para>最接近的双精度值 0</para>
			/// </summary>
			public const long _0 = 0L;

			/// <summary>
			/// 表示数值1
			/// <para>最接近的双精度值 1</para>
			/// </summary>
			public const long _1 = 65536L;

			/// <summary>
			/// 表示数值2
			/// <para>最接近的双精度值 2</para>
			/// </summary>
			public const long _2 = 131072L;

			/// <summary>
			/// 表示数值3
			/// <para>最接近的双精度值 3</para>
			/// </summary>
			public const long _3 = 196608L;

			/// <summary>
			/// 表示数值4
			/// <para>最接近的双精度值 4</para>
			/// </summary>
			public const long _4 = 262144L;

			/// <summary>
			/// 表示数值5
			/// <para>最接近的双精度值 5</para>
			/// </summary>
			public const long _5 = 327680L;

			/// <summary>
			/// 表示数值6
			/// <para>最接近的双精度值 6</para>
			/// </summary>
			public const long _6 = 393216L;

			/// <summary>
			/// 表示数值7
			/// <para>最接近的双精度值 7</para>
			/// </summary>
			public const long _7 = 458752L;

			/// <summary>
			/// 表示数值8
			/// <para>最接近的双精度值 8</para>
			/// </summary>
			public const long _8 = 524288L;

			/// <summary>
			/// 表示数值9
			/// <para>最接近的双精度值 9</para>
			/// </summary>
			public const long _9 = 589824L;

			/// <summary>
			/// 表示数值10
			/// <para>最接近的双精度值 10</para>
			/// </summary>
			public const long _10 = 655360L;

			/// <summary>
			/// 表示数值99
			/// <para>最接近的双精度值 99</para>
			/// </summary>
			public const long _99 = 6488064L;

			/// <summary>
			/// 表示数值100
			/// <para>最接近的双精度值 100</para>
			/// </summary>
			public const long _100 = 6553600L;

			/// <summary>
			/// 表示数值180
			/// <para>最接近的双精度值 180</para>
			/// </summary>
			public const long _180 = 11796480L;

			/// <summary>
			/// 表示数值200
			/// <para>最接近的双精度值 200</para>
			/// </summary>
			public const long _200 = 13107200L;

			/// <summary>
			/// 表示数值360
			/// <para>最接近的双精度值 360</para>
			/// </summary>
			public const long _360 = 23592960L;

			/// <summary>
			/// 表示数值1000
			/// <para>最接近的双精度值 1000</para>
			/// </summary>
			public const long _1000 = 65536000L;

			/// <summary>
			/// 表示数值10000
			/// <para>最接近的双精度值 10000</para>
			/// </summary>
			public const long _10000 = 655360000L;

			/// <summary>
			/// 表示数值0.01
			/// <para>最接近的双精度值 0.0099945068359375</para>
			/// </summary>
			public const long _0_01 = 655L;

			/// <summary>
			/// 表示数值0.02
			/// <para>最接近的双精度值 0.0200042724609375</para>
			/// </summary>
			public const long _0_02 = 1311L;

			/// <summary>
			/// 表示数值0.03
			/// <para>最接近的双精度值 0.029998779296875</para>
			/// </summary>
			public const long _0_03 = 1966L;

			/// <summary>
			/// 表示数值0.04
			/// <para>最接近的双精度值 0.0399932861328125</para>
			/// </summary>
			public const long _0_04 = 2621L;

			/// <summary>
			/// 表示数值0.05
			/// <para>最接近的双精度值 0.0500030517578125</para>
			/// </summary>
			public const long _0_05 = 3277L;

			/// <summary>
			/// 表示数值0.10
			/// <para>最接近的双精度值 0.100006103515625</para>
			/// </summary>
			public const long _0_10 = 6554L;

			/// <summary>
			/// 表示数值0.20
			/// <para>最接近的双精度值 0.199996948242188</para>
			/// </summary>
			public const long _0_20 = 13107L;

			/// <summary>
			/// 表示数值0.25
			/// <para>最接近的双精度值 0.25</para>
			/// </summary>
			public const long _0_25 = 16384L;

			/// <summary>
			/// 表示数值0.50
			/// <para>最接近的双精度值 0.5</para>
			/// </summary>
			public const long _0_50 = 32768L;

			/// <summary>
			/// 表示数值0.75
			/// <para>最接近的双精度值 0.75</para>
			/// </summary>
			public const long _0_75 = 49152L;

			/// <summary>
			/// 表示数值0.33
			/// <para>最接近的双精度值 0.333328247070313</para>
			/// </summary>
			public const long _0_33 = 21845L;

			/// <summary>
			/// 表示数值0.99
			/// <para>最接近的双精度值 0.990005493164063</para>
			/// </summary>
			public const long _0_99 = 64881L;

			/// <summary>
			/// 表示数值 -1
			/// <para>最接近的双精度值 -1</para>
			/// </summary>
			public const long Minus_1 = -65536L;

			/// <summary>
			/// FP常量表示360度在弧度
			/// <para>最接近的双精度值 6.28318786621094</para>
			/// </summary>
			public const long Rad_360 = 411775L;

			/// <summary>
			/// 表示弧度制180度的FP常量
			/// <para>最接近的双精度值 3.14158630371094</para>
			/// </summary>
			public const long Rad_180 = 205887L;

			/// <summary>
			/// 表示弧度制90度的FP常量
			/// <para>最接近的双精度值 1.57080078125</para>
			/// </summary>
			public const long Rad_90 = 102944L;

			/// <summary>
			/// 表示弧度制45度的FP常量
			/// <para>最接近的双精度值 0.785400390625</para>
			/// </summary>
			public const long Rad_45 = 51472L;

			/// <summary>
			/// 表示弧度制22.5度的FP常量
			/// <para>最接近的双精度值 0.3927001953125</para>
			/// </summary>
			public const long Rad_22_50 = 25736L;

			/// <summary>
			/// 表示数值1.01
			/// <para>最接近的双精度值 1.00999450683594</para>
			/// </summary>
			public const long _1_01 = 66191L;

			/// <summary>
			/// 表示数值1.02
			/// <para>最接近的双精度值 1.02000427246094</para>
			/// </summary>
			public const long _1_02 = 66847L;

			/// <summary>
			/// 表示数值1.03
			/// <para>最接近的双精度值 1.02999877929688</para>
			/// </summary>
			public const long _1_03 = 67502L;

			/// <summary>
			/// 表示数值1.04
			/// <para>最接近的双精度值 1.03999328613281</para>
			/// </summary>
			public const long _1_04 = 68157L;

			/// <summary>
			/// 表示数值1.05
			/// <para>最接近的双精度值 1.05000305175781</para>
			/// </summary>
			public const long _1_05 = 68813L;

			/// <summary>
			/// 表示数值1.10
			/// <para>最接近的双精度值 1.10000610351563</para>
			/// </summary>
			public const long _1_10 = 72090L;

			/// <summary>
			/// 表示数值1.20
			/// <para>最接近的双精度值 1.19999694824219</para>
			/// </summary>
			public const long _1_20 = 78643L;

			/// <summary>
			/// 表示数值1.25
			/// <para>最接近的双精度值 1.25</para>
			/// </summary>
			public const long _1_25 = 81920L;

			/// <summary>
			/// 表示数值1.50
			/// <para>最接近的双精度值 1.5</para>
			/// </summary>
			public const long _1_50 = 98304L;

			/// <summary>
			/// 表示数值1.75
			/// <para>最接近的双精度值 1.75</para>
			/// </summary>
			public const long _1_75 = 114688L;

			/// <summary>
			/// 表示数值1.33
			/// <para>最接近的双精度值 1.33332824707031</para>
			/// </summary>
			public const long _1_33 = 87381L;

			/// <summary>
			/// 表示数值1.99
			/// <para>最接近的双精度值 1.99000549316406</para>
			/// </summary>
			public const long _1_99 = 130417L;

			/// <summary>
			/// 表示 ε 值的FP常量EN1
			/// <para>最接近的双精度值 0.100006103515625</para>
			/// </summary>
			public const long EN1 = 6554L;

			/// <summary>
			/// 表示 ε 值的FP常量EN2
			/// <para>最接近的双精度值 0.0099945068359375</para>
			/// </summary>
			public const long EN2 = 655L;

			/// <summary>
			/// 表示 ε 值的FP常量EN3
			/// <para>最接近的双精度值 0.001007080078125</para>
			/// </summary>
			public const long EN3 = 66L;

			/// <summary>
			/// 表示 ε 值的FP常量EN4
			/// <para>最接近的双精度值 0.0001068115234375</para>
			/// </summary>
			public const long EN4 = 7L;

			/// <summary>
			/// 表示 ε 值的FP常量EN5
			/// <para>最接近的双精度值 1.52587890625E-05</para>
			/// </summary>
			public const long EN5 = 1L;

			/// <summary>
			/// 表示 ε<see cref="F:Photon.Deterministic.FP.Raw.EN3" />的FP常量
			/// <para>最接近的双精度值 0.001007080078125</para>
			/// </summary>
			public const long Epsilon = 66L;

			/// <summary>
			/// 表示欧拉数常量的FP常量
			/// <para>最接近的双精度值 2.71827697753906</para>
			/// </summary>
			public const long E = 178145L;

			/// <summary>
			/// 表示Log(E) 的FP常量
			/// <para>最接近的双精度值 1.44268798828125</para>
			/// </summary>
			public const long Log2_E = 94548L;

			/// <summary>
			/// 表示Log(10) 的FP常量
			/// <para>最接近的双精度值 3.32192993164063</para>
			/// </summary>
			public const long Log2_10 = 217706L;
		}

		/// <summary>
		/// 比较<see cref="T:Photon.Deterministic.FP" />的值
		/// </summary>
		public class Comparer : Comparer<FP>
		{
			/// <summary>
			/// 一个全局FP比较器实例
			/// </summary>
			public static readonly Comparer Instance = new Comparer();

			private Comparer()
			{
			}

			/// <summary>
			/// 比较两个 FP 实例并返回整数 表示第一个实例小于 等于或大于第二个实例
			/// </summary>
			/// <param name="x">第一个待比较的实例</param>
			/// <param name="y">第二个待比较的实例</param>
			/// <returns>
			/// <returns>有符号整数 表示 <paramref name="x" /> 与 <paramref name="y" /> 的相对顺序 具体含义见下表</returns>
			/// 小于零表示 x 小于 y
			/// - 零 x等于y
			/// 大于零表示 x 大于 y
			/// </returns>
			public override int Compare(FP x, FP y)
			{
				return x.RawValue.CompareTo(y.RawValue);
			}
		}

		/// <summary>
		/// 相等比较器用于<see cref="T:Photon.Deterministic.FP" />值
		/// </summary>
		public class EqualityComparer : IEqualityComparer<FP>
		{
			/// <summary>
			/// 一个全局FP相等比较器实例
			/// </summary>
			public static readonly EqualityComparer Instance = new EqualityComparer();

			private EqualityComparer()
			{
			}

			bool IEqualityComparer<FP>.Equals(FP x, FP y)
			{
				return x.RawValue == y.RawValue;
			}

			int IEqualityComparer<FP>.GetHashCode(FP num)
			{
				return num.RawValue.GetHashCode();
			}
		}

		/// <summary>
		/// 以字节为单位的结构体大小
		/// <para>Size 常量用于表示该结构体占用的字节数</para>
		/// </summary>
		public const int SIZE = 8;

		internal const int DecimalFractionDigits = 5;

		internal const double DecimalFractionNormalizer = 100000.0;

		private const int FRACTIONS_COUNT = 5;

		/// <summary>
		/// 定点数数值 1
		/// </summary>
		public const long RAW_ONE = 65536L;

		/// <summary>
		/// 保存 FP 零值原始表示的常量
		/// </summary>
		public const long RAW_ZERO = 0L;

		/// <summary>
		/// 表示定点数计算使用的精度
		/// </summary>
		/// <remarks>
		/// 精度常量决定定点数的小数位数
		/// </remarks>
		public const int Precision = 16;

		/// <summary>
		/// 定点数占用的位数 即 64 位
		/// </summary>
		public const int Bits = 64;

		/// <summary>
		/// 定点数乘法使用的舍入常量
		/// </summary>
		public const long MulRound = 32768L;

		/// <summary>
		/// 定点数乘法使用的位移量
		/// </summary>
		public const int MulShift = 16;

		public const int MulShiftTrunc = 16;

		internal const bool UsesRoundedConstants = true;

		/// <summary>
		/// 定点数的原始整数值
		/// </summary>
		[FieldOffset(0)]
		public long RawValue;

		/// <summary>
		/// 最小 FP 单位不等于 0
		/// <para>最接近的双精度值 1.52587890625E-05</para>
		/// </summary>
		public unsafe static FP SmallestNonZero
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 1L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// FP最小值但是超出<see cref="P:Photon.Deterministic.FP.UseableMin" />和<see cref="P:Photon.Deterministic.FP.UseableMax" />(包含边界的) 可以溢出当相乘
		/// <para>最接近的双精度值 -140737488355328</para>
		/// </summary>
		public unsafe static FP MinValue
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = long.MinValue;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// FP最大值但是超出<see cref="P:Photon.Deterministic.FP.UseableMin" />和<see cref="P:Photon.Deterministic.FP.UseableMax" />(包含边界的) 可以溢出当相乘
		/// <para>最接近的双精度值 140737488355328</para>
		/// </summary>
		public unsafe static FP MaxValue
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = long.MaxValue;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示与自身相乘且不会溢出超出长整数范围的最大负FP数值
		/// <para>最接近的双精度值 -32768</para>
		/// </summary>
		public unsafe static FP UseableMin
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = -2147483648L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示与自身相乘且不会溢出超出长整数范围的最大FP数值
		/// <para>最接近的双精度值 32767.9999847412</para>
		/// </summary>
		public unsafe static FP UseableMax
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 2147483647L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// Pi数值
		/// <para>最接近的双精度值 3.14158630371094</para>
		/// </summary>
		public unsafe static FP Pi
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 205887L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 1/Pi
		/// <para>最接近的双精度值 0.318313598632813</para>
		/// </summary>
		public unsafe static FP PiInv
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 20861L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 2 * Pi
		/// <para>最接近的双精度值 6.28318786621094</para>
		/// </summary>
		public unsafe static FP PiTimes2
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 411775L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// Pi / 2
		/// <para>最接近的双精度值 1.57080078125</para>
		/// </summary>
		public unsafe static FP PiOver2
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 102944L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 2 / Pi
		/// <para>最接近的双精度值 0.636627197265625</para>
		/// </summary>
		public unsafe static FP PiOver2Inv
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 41722L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// Pi / 4
		/// <para>最接近的双精度值 0.785400390625</para>
		/// </summary>
		public unsafe static FP PiOver4
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 51472L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 3 * Pi / 4
		/// <para>最接近的双精度值 2.356201171875</para>
		/// </summary>
		public unsafe static FP Pi3Over4
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 154416L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 4 * Pi / 3
		/// <para>最接近的双精度值 4.18879699707031</para>
		/// </summary>
		public unsafe static FP Pi4Over3
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 274517L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 角度转弧度的转换常量
		/// <para>最接近的双精度值 0.0174560546875</para>
		/// </summary>
		public unsafe static FP Deg2Rad
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 1144L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 弧度转角度的转换常量
		/// <para>最接近的双精度值 57.2957763671875</para>
		/// </summary>
		public unsafe static FP Rad2Deg
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 3754936L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值0
		/// <para>最接近的双精度值 0</para>
		/// </summary>
		public unsafe static FP _0
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 0L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值1
		/// <para>最接近的双精度值 1</para>
		/// </summary>
		public unsafe static FP _1
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 65536L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值2
		/// <para>最接近的双精度值 2</para>
		/// </summary>
		public unsafe static FP _2
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 131072L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值3
		/// <para>最接近的双精度值 3</para>
		/// </summary>
		public unsafe static FP _3
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 196608L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值4
		/// <para>最接近的双精度值 4</para>
		/// </summary>
		public unsafe static FP _4
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 262144L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值5
		/// <para>最接近的双精度值 5</para>
		/// </summary>
		public unsafe static FP _5
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 327680L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值6
		/// <para>最接近的双精度值 6</para>
		/// </summary>
		public unsafe static FP _6
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 393216L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值7
		/// <para>最接近的双精度值 7</para>
		/// </summary>
		public unsafe static FP _7
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 458752L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值8
		/// <para>最接近的双精度值 8</para>
		/// </summary>
		public unsafe static FP _8
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 524288L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值9
		/// <para>最接近的双精度值 9</para>
		/// </summary>
		public unsafe static FP _9
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 589824L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值10
		/// <para>最接近的双精度值 10</para>
		/// </summary>
		public unsafe static FP _10
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 655360L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值99
		/// <para>最接近的双精度值 99</para>
		/// </summary>
		public unsafe static FP _99
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 6488064L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值100
		/// <para>最接近的双精度值 100</para>
		/// </summary>
		public unsafe static FP _100
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 6553600L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值180
		/// <para>最接近的双精度值 180</para>
		/// </summary>
		public unsafe static FP _180
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 11796480L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值200
		/// <para>最接近的双精度值 200</para>
		/// </summary>
		public unsafe static FP _200
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 13107200L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值360
		/// <para>最接近的双精度值 360</para>
		/// </summary>
		public unsafe static FP _360
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 23592960L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值1000
		/// <para>最接近的双精度值 1000</para>
		/// </summary>
		public unsafe static FP _1000
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 65536000L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值10000
		/// <para>最接近的双精度值 10000</para>
		/// </summary>
		public unsafe static FP _10000
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 655360000L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值0.01
		/// <para>最接近的双精度值 0.0099945068359375</para>
		/// </summary>
		public unsafe static FP _0_01
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 655L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值0.02
		/// <para>最接近的双精度值 0.0200042724609375</para>
		/// </summary>
		public unsafe static FP _0_02
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 1311L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值0.03
		/// <para>最接近的双精度值 0.029998779296875</para>
		/// </summary>
		public unsafe static FP _0_03
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 1966L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值0.04
		/// <para>最接近的双精度值 0.0399932861328125</para>
		/// </summary>
		public unsafe static FP _0_04
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 2621L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值0.05
		/// <para>最接近的双精度值 0.0500030517578125</para>
		/// </summary>
		public unsafe static FP _0_05
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 3277L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值0.10
		/// <para>最接近的双精度值 0.100006103515625</para>
		/// </summary>
		public unsafe static FP _0_10
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 6554L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值0.20
		/// <para>最接近的双精度值 0.199996948242188</para>
		/// </summary>
		public unsafe static FP _0_20
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 13107L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值0.25
		/// <para>最接近的双精度值 0.25</para>
		/// </summary>
		public unsafe static FP _0_25
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 16384L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值0.50
		/// <para>最接近的双精度值 0.5</para>
		/// </summary>
		public unsafe static FP _0_50
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 32768L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值0.75
		/// <para>最接近的双精度值 0.75</para>
		/// </summary>
		public unsafe static FP _0_75
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 49152L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值0.33
		/// <para>最接近的双精度值 0.333328247070313</para>
		/// </summary>
		public unsafe static FP _0_33
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 21845L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值0.99
		/// <para>最接近的双精度值 0.990005493164063</para>
		/// </summary>
		public unsafe static FP _0_99
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 64881L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值 -1
		/// <para>最接近的双精度值 -1</para>
		/// </summary>
		public unsafe static FP Minus_1
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = -65536L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// FP常量表示360度在弧度
		/// <para>最接近的双精度值 6.28318786621094</para>
		/// </summary>
		public unsafe static FP Rad_360
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 411775L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示弧度制180度的FP常量
		/// <para>最接近的双精度值 3.14158630371094</para>
		/// </summary>
		public unsafe static FP Rad_180
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 205887L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示弧度制90度的FP常量
		/// <para>最接近的双精度值 1.57080078125</para>
		/// </summary>
		public unsafe static FP Rad_90
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 102944L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示弧度制45度的FP常量
		/// <para>最接近的双精度值 0.785400390625</para>
		/// </summary>
		public unsafe static FP Rad_45
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 51472L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示弧度制22.5度的FP常量
		/// <para>最接近的双精度值 0.3927001953125</para>
		/// </summary>
		public unsafe static FP Rad_22_50
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 25736L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值1.01
		/// <para>最接近的双精度值 1.00999450683594</para>
		/// </summary>
		public unsafe static FP _1_01
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 66191L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值1.02
		/// <para>最接近的双精度值 1.02000427246094</para>
		/// </summary>
		public unsafe static FP _1_02
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 66847L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值1.03
		/// <para>最接近的双精度值 1.02999877929688</para>
		/// </summary>
		public unsafe static FP _1_03
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 67502L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值1.04
		/// <para>最接近的双精度值 1.03999328613281</para>
		/// </summary>
		public unsafe static FP _1_04
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 68157L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值1.05
		/// <para>最接近的双精度值 1.05000305175781</para>
		/// </summary>
		public unsafe static FP _1_05
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 68813L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值1.10
		/// <para>最接近的双精度值 1.10000610351563</para>
		/// </summary>
		public unsafe static FP _1_10
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 72090L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值1.20
		/// <para>最接近的双精度值 1.19999694824219</para>
		/// </summary>
		public unsafe static FP _1_20
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 78643L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值1.25
		/// <para>最接近的双精度值 1.25</para>
		/// </summary>
		public unsafe static FP _1_25
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 81920L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值1.50
		/// <para>最接近的双精度值 1.5</para>
		/// </summary>
		public unsafe static FP _1_50
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 98304L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值1.75
		/// <para>最接近的双精度值 1.75</para>
		/// </summary>
		public unsafe static FP _1_75
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 114688L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值1.33
		/// <para>最接近的双精度值 1.33332824707031</para>
		/// </summary>
		public unsafe static FP _1_33
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 87381L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示数值1.99
		/// <para>最接近的双精度值 1.99000549316406</para>
		/// </summary>
		public unsafe static FP _1_99
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 130417L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示 ε 值的FP常量EN1
		/// <para>最接近的双精度值 0.100006103515625</para>
		/// </summary>
		public unsafe static FP EN1
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 6554L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示 ε 值的FP常量EN2
		/// <para>最接近的双精度值 0.0099945068359375</para>
		/// </summary>
		public unsafe static FP EN2
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 655L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示 ε 值的FP常量EN3
		/// <para>最接近的双精度值 0.001007080078125</para>
		/// </summary>
		public unsafe static FP EN3
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 66L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示 ε 值的FP常量EN4
		/// <para>最接近的双精度值 0.0001068115234375</para>
		/// </summary>
		public unsafe static FP EN4
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 7L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示 ε 值的FP常量EN5
		/// <para>最接近的双精度值 1.52587890625E-05</para>
		/// </summary>
		public unsafe static FP EN5
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 1L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示 ε<see cref="P:Photon.Deterministic.FP.EN3" />的FP常量
		/// <para>最接近的双精度值 0.001007080078125</para>
		/// </summary>
		public unsafe static FP Epsilon
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 66L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示欧拉数常量的FP常量
		/// <para>最接近的双精度值 2.71827697753906</para>
		/// </summary>
		public unsafe static FP E
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 178145L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示Log(E) 的FP常量
		/// <para>最接近的双精度值 1.44268798828125</para>
		/// </summary>
		public unsafe static FP Log2_E
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 94548L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 表示Log(10) 的FP常量
		/// <para>最接近的双精度值 3.32192993164063</para>
		/// </summary>
		public unsafe static FP Log2_10
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				long num = 217706L;
				return *(FP*)(&num);
			}
		}

		/// <summary>
		/// 返回整数部分作为长整数
		/// </summary>
		public readonly long AsLong
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return RawValue >> 16;
			}
		}

		/// <summary>
		/// 返回整数部分作为int
		/// </summary>
		public readonly int AsInt
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return (int)(RawValue >> 16);
			}
		}

		/// <summary>
		/// 返回整数部分作为int
		/// </summary>
		public readonly short AsShort
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return (short)(RawValue >> 16);
			}
		}

		/// <summary>
		/// 转换为单精度浮点
		/// </summary>
		public readonly float AsFloat
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return (float)RawValue / 65536f;
			}
		}

		/// <summary>
		/// 转换为双精度浮点返回值并不精确而是误差最小的值
		/// 根据 FP 的精度返回有效数字位数
		/// </summary>
		public readonly double AsRoundedDouble
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				(int, ulong, uint) decimalParts = GetDecimalParts();
				return (double)decimalParts.Item1 * ((double)decimalParts.Item2 + (double)decimalParts.Item3 / 100000.0);
			}
		}

		/// <summary>
		/// 转换为双精度浮点
		/// </summary>
		public readonly double AsDouble
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return (double)RawValue / 65536.0;
			}
		}

		/// <summary>
		/// 使用给定序列化器序列化指针指向的值
		/// </summary>
		/// <param name="ptr">指向待序列化 FP 对象的指针</param>
		/// <param name="serializer">序列化器用于序列化</param>
		public unsafe static void Serialize(void* ptr, IDeterministicFrameSerializer serializer)
		{
			serializer.Stream.Serialize(&((FP*)ptr)->RawValue);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal FP(long v)
		{
			RawValue = v;
		}

		/// <summary>
		/// 比较当前 FP 实例与另一个实例并返回整数 表示当前实例小于 等于或大于另一个实例
		/// </summary>
		/// <param name="other">其他待比较的实例</param>
		/// <returns>有符号整数 表示当前实例与另一个实例的相对顺序</returns>
		public readonly int CompareTo(FP other)
		{
			return RawValue.CompareTo(other.RawValue);
		}

		/// <summary>
		/// 判断当前 FP 实例是否与另一个实例相等
		/// </summary>
		/// <param name="other">与当前实例比较的 FP 实例</param>
		/// <returns>相等时返回 <see langword="true" /> 否则返回 <see langword="false" /></returns>
		public readonly bool Equals(FP other)
		{
			return RawValue == other.RawValue;
		}

		/// <summary>
		/// 判断当前 FP 实例是否与指定对象相等
		/// </summary>
		/// <param name="obj">与当前 FP 实例比较的对象</param>
		/// <returns>
		/// 对象与当前 FP 实例相等时返回 <see langword="true" /> 否则返回 <see langword="false" />
		/// </returns>
		public override readonly bool Equals(object obj)
		{
			if (obj is FP)
			{
				return RawValue == ((FP)obj).RawValue;
			}
			return false;
		}

		/// <summary>
		/// 计算当前 FP 实例的哈希码
		/// </summary>
		/// <returns>
		/// 32位有符号整数哈希码
		/// </returns>
		public override readonly int GetHashCode()
		{
			return RawValue.GetHashCode();
		}

		/// <summary>
		/// 返回当前 FP 值的字符串表示
		/// </summary>
		/// <returns>
		/// 表示以下对象的字符串当前FP值
		/// </returns>
		public override readonly string ToString()
		{
			var (num, value, num2) = GetDecimalParts();
			if (num2 == 0)
			{
				return (RawValue >> 16).ToString();
			}
			StringBuilder stringBuilder = new StringBuilder();
			if (num < 0)
			{
				stringBuilder.Append('-');
			}
			stringBuilder.Append(value);
			stringBuilder.Append('.');
			if (num2 < 10000)
			{
				stringBuilder.Append('0');
				if (num2 < 1000)
				{
					stringBuilder.Append('0');
					if (num2 < 100)
					{
						stringBuilder.Append('0');
						if (num2 < 10)
						{
							stringBuilder.Append('0');
						}
					}
				}
			}
			if (num2 % 10000 == 0)
			{
				num2 /= 10000;
			}
			else if (num2 % 1000 == 0)
			{
				num2 /= 1000;
			}
			else if (num2 % 100 == 0)
			{
				num2 /= 100;
			}
			else if (num2 % 10 == 0)
			{
				num2 /= 10;
			}
			stringBuilder.Append(num2);
			return stringBuilder.ToString();
		}

		/// <summary>
		/// 使用旧版格式将当前 FP 对象转换为等价字符串
		/// </summary>
		/// <returns>
		/// 使用旧版格式生成当前 FP 对象的字符串表示
		/// </returns>
		[Obsolete]
		public string ToStringLegacy()
		{
			return AsFloat.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// 返回 <see cref="T:Photon.Deterministic.FP" /> 的字符串表示
		/// </summary>
		/// <returns>FP 的字符串表示</returns>
		public readonly string ToString(string format)
		{
			return AsDouble.ToString(format, CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// 使用自定义格式返回 <see cref="T:Photon.Deterministic.FP" /> 的字符串表示
		/// </summary>
		/// <returns>FP 的字符串表示</returns>
		public readonly string ToStringInternal()
		{
			long num = Math.Abs(RawValue);
			string text = $"{num >> 16}.{(num % 65536).ToString(CultureInfo.InvariantCulture).PadLeft(5, '0')}";
			if (RawValue < 0)
			{
				return "-" + text;
			}
			return text;
		}

		/// <summary>
		/// 将双精度浮点值转换为FP实例并舍入到最接近的可表示FP值
		/// </summary>
		/// <param name="value">待转换并舍入的双精度浮点值</param>
		/// <returns>舍入后双精度浮点值对应的 FP 值</returns>
		public static FP FromRoundedDouble_UNSAFE(double value)
		{
			return new FP((long)Math.Round(value * 65536.0));
		}

		/// <summary>
		/// 将双精度浮点值转换为FP实例并向零舍入
		/// 若要舍入到最接近的可表示 FP 值 请使用 <see cref="M:Photon.Deterministic.FP.FromRoundedDouble_UNSAFE(System.Double)" />
		/// 此方法具有不确定性 因此标记为不安全
		/// </summary>
		/// <param name="value">待转换的双精度浮点值</param>
		/// <returns>表示转换后数值的 FP 实例</returns>
		public static FP FromDouble_UNSAFE(double value)
		{
			return new FP((long)(value * 65536.0));
		}

		/// <summary>
		/// 将单精度浮点值转换为FP实例并舍入到最接近的可表示FP值
		/// 此方法具有不确定性 因此标记为不安全
		/// </summary>
		/// <param name="value">待转换的值</param>
		/// <returns>转换后的值</returns>
		public static FP FromRoundedFloat_UNSAFE(float value)
		{
			return new FP((long)Math.Round(value * 65536f));
		}

		/// <summary>
		/// 将单精度浮点值转换为FP实例并向零舍入
		/// 若要舍入到最接近的可表示 FP 值 请使用 <see cref="M:Photon.Deterministic.FP.FromRoundedFloat_UNSAFE(System.Single)" />
		/// 此方法具有不确定性 因此标记为不安全
		/// </summary>
		/// <param name="value">待转换的值</param>
		/// <returns>转换后的值</returns>
		public static FP FromFloat_UNSAFE(float value)
		{
			return new FP(checked((long)(value * 65536f)));
		}

		/// <summary>
		/// 将原始整数值转换为FP实例
		/// </summary>
		/// <param name="value">待转换的原始整数值</param>
		/// <returns>与原始整数表示相同数值的新 FP 实例</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP FromRaw(long value)
		{
			FP result = default(FP);
			result.RawValue = value;
			return result;
		}

		/// <summary>
		/// 根据单精度浮点值的字符串表示创建 FP 实例
		/// 此方法具有不确定性 因此标记为不安全
		/// </summary>
		/// <param name="value">单精度浮点值的字符串表示</param>
		/// <returns>以下对象的实例FP表示单精度浮点值</returns>
		[Obsolete("Use FromString instead.")]
		public static FP FromString_UNSAFE(string value)
		{
			return FromFloat_UNSAFE((float)double.Parse(value, CultureInfo.InvariantCulture));
		}

		/// <summary>
		/// 将定点数的字符串表示转换为FP结构体实例
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		/// <exception cref="T:System.FormatException"></exception>
		public static FP FromString(string value)
		{
			if (value == null)
			{
				return _0;
			}
			value = value.Trim();
			if (value.Length == 0)
			{
				return _0;
			}
			bool flag = false;
			if (flag = value[0] == '-')
			{
				value = value.Substring(1);
			}
			bool flag2 = value[0] == '.';
			string[] array = value.Split(new char[1] { '.' }, StringSplitOptions.RemoveEmptyEntries);
			long num = 0L;
			num = array.Length switch
			{
				1 => (!flag2) ? ParseInteger(array[0]) : ParseFractions(array[0]), 
				2 => checked(ParseInteger(array[0]) + ParseFractions(array[1])), 
				_ => throw new FormatException(value), 
			};
			if (flag)
			{
				return new FP(-num);
			}
			return new FP(num);
		}

		private static long ParseInteger(string format)
		{
			return long.Parse(format) * 65536;
		}

		private static long ParseFractions(string format)
		{
			long num;
			switch (format.Length)
			{
			case 0:
				return 0L;
			case 1:
				num = 10L;
				break;
			case 2:
				num = 100L;
				break;
			case 3:
				num = 1000L;
				break;
			case 4:
				num = 10000L;
				break;
			case 5:
				num = 100000L;
				break;
			case 6:
				num = 1000000L;
				break;
			case 7:
				num = 10000000L;
				break;
			default:
			{
				if (format.Length > 14)
				{
					format = format.Substring(0, 14);
				}
				num = 100000000L;
				for (int i = 8; i < format.Length; i++)
				{
					num *= 10;
				}
				break;
			}
			}
			long num2 = long.Parse(format);
			return (num2 * 65536 + num / 2) / num;
		}

		internal static long RawMultiply(FP x, FP y)
		{
			return x.RawValue * y.RawValue + 32768 >> 16;
		}

		internal static long RawMultiply(FP x, FP y, FP z)
		{
			y.RawValue = x.RawValue * y.RawValue + 32768 >> 16;
			return y.RawValue * z.RawValue + 32768 >> 16;
		}

		internal static long RawMultiply(FP x, FP y, FP z, FP a)
		{
			y.RawValue = x.RawValue * y.RawValue + 32768 >> 16;
			z.RawValue = y.RawValue * z.RawValue + 32768 >> 16;
			return z.RawValue * a.RawValue + 32768 >> 16;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP MulTruncate(FP x, FP y)
		{
			return FromRaw(x.RawValue * y.RawValue >> 16);
		}

		internal readonly (int Sign, ulong Integer, uint Fraction) GetDecimalParts()
		{
			int item;
			ulong num;
			if (RawValue < 0)
			{
				item = -1;
				num = (ulong)(-RawValue);
			}
			else
			{
				item = 1;
				num = (ulong)RawValue;
			}
			uint num2 = (uint)(num & 0xFFFF);
			ulong item2 = num >> 16;
			if (num2 == 0)
			{
				return (Sign: item, Integer: item2, Fraction: 0u);
			}
			uint num3 = (uint)((ulong)((long)((num2 << 1) - 1) * 762939453125L) / 10000000000uL);
			uint num4 = (uint)((ulong)((long)((num2 << 1) + 1) * 762939453125L) / 10000000000uL);
			uint num5 = num3 + num4 >> 1;
			uint num6 = 0u;
			uint num7 = num3 / 10000;
			uint num8 = num4 / 10000;
			if (num7 == num8)
			{
				uint num9 = num3 / 1000;
				uint num10 = num4 / 1000;
				num6 = ((num9 != num10) ? ((num5 + 500) / 1000 * 10) : ((num5 + 50) / 100));
			}
			else
			{
				uint num11 = num3 / 100000;
				uint num12 = num4 / 100000;
				if (num11 == num12)
				{
					num6 = (num5 + 5000) / 10000 * 100;
				}
				else
				{
					uint num13 = num3 / 1000000;
					uint num14 = num4 / 1000000;
					num6 = ((num13 != num14) ? ((num5 + 500000) / 1000000 * 10000) : ((num5 + 50000) / 100000 * 1000));
				}
			}
			return (Sign: item, Integer: item2, Fraction: num6);
		}

		/// <summary>
		/// 取反值
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator -(FP a)
		{
			a.RawValue = -a.RawValue;
			return a;
		}

		/// <c>FP.Operators.cs</c>
		/// <summary>
		/// 返回给定值的绝对值
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator +(FP a)
		{
			a.RawValue = a.RawValue;
			return a;
		}

		/// <summary>
		/// 重载运算符 用于将两个 FP 值相加
		/// </summary>
		/// <param name="a">第一个FP值</param>
		/// <param name="b">第二个FP值</param>
		/// <returns>两个 FP 值之和</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator +(FP a, FP b)
		{
			a.RawValue += b.RawValue;
			return a;
		}

		/// <summary>
		/// 将整数值与 FP 值相加
		/// </summary>
		/// <param name="a">FP值</param>
		/// <param name="b">待相加的整数值</param>
		/// <returns>整数值与 FP 值相加的结果</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator +(FP a, int b)
		{
			a.RawValue += (long)b << 16;
			return a;
		}

		/// <summary>
		/// 重载加法运算符 用于将整数值与 FP 值相加
		/// </summary>
		/// <param name="a">整数值</param>
		/// <param name="b">FP值</param>
		/// <returns>整数值与 FP 值相加的结果</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator +(int a, FP b)
		{
			b.RawValue = ((long)a << 16) + b.RawValue;
			return b;
		}

		/// <summary>
		/// 减去两个FP (定点数) 值
		/// </summary>
		/// <param name="a">第一个FP值</param>
		/// <param name="b">第二个FP值</param>
		/// <returns>第一个 FP 值减去第二个 FP 值的结果</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator -(FP a, FP b)
		{
			a.RawValue -= b.RawValue;
			return a;
		}

		/// <summary>
		/// 从 FP 值中减去整数值
		/// </summary>
		/// <param name="a">FP值</param>
		/// <param name="b">整数值</param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator -(FP a, int b)
		{
			a.RawValue -= (long)b << 16;
			return a;
		}

		/// <summary>
		/// 重载运算符 用于计算整数值减去 FP 值
		/// </summary>
		/// <param name="a">作为被减数的整数值</param>
		/// <param name="b">作为减数的 FP 值</param>
		/// <returns>整数值减去 FP 值的结果</returns>
		/// <remarks>
		/// 先将整数值左移 FP 精度位数 再减去 FP 的原始值
		/// 将计算结果作为新的 FP 值返回
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator -(int a, FP b)
		{
			b.RawValue = ((long)a << 16) - b.RawValue;
			return b;
		}

		/// <summary>
		/// 重载运算符 用于将两个 FP 值相乘
		/// </summary>
		/// <param name="a">第一个FP值</param>
		/// <param name="b">第二个FP值</param>
		/// <returns>乘积</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator *(FP a, FP b)
		{
			a.RawValue = a.RawValue * b.RawValue + 32768 >> 16;
			return a;
		}

		/// <summary>
		/// 表示将浮点值与整数值相乘的运算符
		/// </summary>
		/// <param name="a">浮点值</param>
		/// <param name="b">整数值</param>
		/// <returns>浮点值与整数值相乘的结果</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator *(FP a, int b)
		{
			a.RawValue *= b;
			return a;
		}

		/// <summary>
		/// 将整数值与 FP 值相乘
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator *(int a, FP b)
		{
			b.RawValue = a * b.RawValue;
			return b;
		}

		/// <summary>
		/// 对两个 FP 定点数值执行除法
		/// </summary>
		/// <param name="a">被除数</param>
		/// <param name="b">除数</param>
		/// <returns>除法运算结果</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator /(FP a, FP b)
		{
			a.RawValue = (a.RawValue << 16) / b.RawValue;
			return a;
		}

		/// <summary>
		/// 将 FP 值除以整数值
		/// </summary>
		/// <param name="a">第一个FP值</param>
		/// <param name="b">第二个Int32值</param>
		/// <returns>除法结果</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator /(FP a, int b)
		{
			a.RawValue /= b;
			return a;
		}

		/// <summary>
		/// 此运算符接收整数值 <c>a</c> 和 FP 值 <c>b</c> 并返回 <c>a</c> 除以 <c>b</c> 的结果
		/// </summary>
		/// <param name="a">作为被除数的整数值</param>
		/// <param name="b">作为除数的 FP 值</param>
		/// <returns><c>a</c> 除以 <c>b</c> 得到的 FP 值</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator /(int a, FP b)
		{
			b.RawValue = ((long)a << 32) / b.RawValue;
			return b;
		}

		/// <summary>
		/// 将 FP 值除以高精度除数
		/// </summary>
		/// <param name="a">FP值</param>
		/// <param name="b">HighPrecisionDivisor值</param>
		/// <returns>结果</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator /(FP a, FPHighPrecisionDivisor b)
		{
			a.RawValue = FPHighPrecisionDivisor.RawDiv(a.RawValue, b.RawValue);
			return a;
		}

		/// <summary>
		/// 取模运算符用于FP值
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator %(FP a, FP b)
		{
			a.RawValue %= b.RawValue;
			return a;
		}

		/// <summary>
		/// 取模运算符用于FP和整数值
		/// </summary>
		/// <param name="a">FP值</param>
		/// <param name="b">整数值</param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator %(FP a, int b)
		{
			a.RawValue %= (long)b << 16;
			return a;
		}

		/// <summary>
		/// 取模运算符用于整数和FP值
		/// </summary>
		/// <param name="a">整数值</param>
		/// <param name="b">FP值</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator %(int a, FP b)
		{
			b.RawValue = ((long)a << 16) % b.RawValue;
			return b;
		}

		/// <summary>
		/// 取模运算符用于FP和高精度除数
		/// </summary>
		/// <param name="a">FP值</param>
		/// <param name="b">高精度除数</param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP operator %(FP a, FPHighPrecisionDivisor b)
		{
			a.RawValue = FPHighPrecisionDivisor.RawMod(a.RawValue, b.RawValue);
			return a;
		}

		/// <summary>
		/// 使用运算符比较两个FP值
		/// </summary>
		/// <param name="a">第一个FP值</param>
		/// <param name="b">第二个FP值</param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FP a, FP b)
		{
			return a.RawValue < b.RawValue;
		}

		/// <summary>
		/// 比较 FP 值与整数值
		/// </summary>
		/// <param name="a">FP值</param>
		/// <param name="b">整数值</param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FP a, int b)
		{
			return a.RawValue < (long)b << 16;
		}

		/// <summary>
		/// 比较整数值与 FP 值
		/// </summary>
		/// <param name="a">整数值</param>
		/// <param name="b">FP值</param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(int a, FP b)
		{
			return (long)a << 16 < b.RawValue;
		}

		/// <summary>
		/// 使用运算符比较两个FP值
		/// </summary>
		/// <param name="a">第一个FP值</param>
		/// <param name="b">第二个FP值</param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FP a, FP b)
		{
			return a.RawValue <= b.RawValue;
		}

		/// <summary>
		/// 定义FP定点值与整数值之间小于等于比较运算符的代码
		/// </summary>
		/// <param name="a">待比较的 FP 值</param>
		/// <param name="b">待比较的整数值</param>
		/// <returns>FP 值小于或等于整数值时返回 <see langword="true" /> 否则返回 <see langword="false" /></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FP a, int b)
		{
			return a.RawValue <= (long)b << 16;
		}

		/// <summary>
		/// 定义整数值与FP定点值之间小于等于比较运算符的代码
		/// </summary>
		/// <param name="a">待比较的整数值</param>
		/// <param name="b">待比较的 FP 值</param>
		/// <returns>整数值小于或等于 FP 值时返回 <see langword="true" /> 否则返回 <see langword="false" /></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(int a, FP b)
		{
			return (long)a << 16 <= b.RawValue;
		}

		/// <summary>
		/// 使用运算符比较两个FP值
		/// </summary>
		/// <param name="a">第一个FP值</param>
		/// <param name="b">第二个FP值</param>
		/// <returns>第一个值大于第二个值时返回 <see langword="true" /> 否则返回 <see langword="false" /></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FP a, FP b)
		{
			return a.RawValue > b.RawValue;
		}

		/// <summary>
		/// 比较 FP 值与整数值
		/// </summary>
		/// <param name="a">FP值</param>
		/// <param name="b">整数值</param>
		/// <returns>FP 值大于整数值时返回 <see langword="true" /> 否则返回 <see langword="false" /></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FP a, int b)
		{
			return a.RawValue > (long)b << 16;
		}

		/// <summary>
		/// 比较整数值与 FP 值
		/// </summary>
		/// <param name="a">整数值</param>
		/// <param name="b">FP值</param>
		/// <returns>整数值大于 FP 值时返回 <see langword="true" /> 否则返回 <see langword="false" /></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(int a, FP b)
		{
			return (long)a << 16 > b.RawValue;
		}

		/// <summary>
		/// 使用运算符比较两个FP值
		/// </summary>
		/// <param name="a">第一个FP值</param>
		/// <param name="b">第二个FP值</param>
		/// <returns>第一个值大于或等于第二个值时返回 <see langword="true" /> 否则返回 <see langword="false" /></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FP a, FP b)
		{
			return a.RawValue >= b.RawValue;
		}

		/// <summary>
		/// 比较 FP 值与整数值
		/// </summary>
		/// <param name="a">FP值</param>
		/// <param name="b">整数值</param>
		/// <returns>FP 值大于或等于整数值时返回 <see langword="true" /> 否则返回 <see langword="false" /></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FP a, int b)
		{
			return a.RawValue >= (long)b << 16;
		}

		/// <summary>
		/// 比较整数值与 FP 值
		/// </summary>
		/// <param name="a">整数值</param>
		/// <param name="b">FP值</param>
		/// <returns>整数值大于或等于 FP 值时返回 <see langword="true" /> 否则返回 <see langword="false" /></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(int a, FP b)
		{
			return (long)a << 16 >= b.RawValue;
		}

		/// <summary>
		/// 比较两个FP值是否相等
		/// </summary>
		/// <param name="a">第一个FP值</param>
		/// <param name="b">第二个FP值</param>
		/// <returns>两个值相等时返回 <see langword="true" /> 否则返回 <see langword="false" /></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FP a, FP b)
		{
			return a.RawValue == b.RawValue;
		}

		/// <summary>
		/// 比较 FP 值与整数值是否相等
		/// </summary>
		/// <param name="a">FP值</param>
		/// <param name="b">整数值</param>
		/// <returns>两个值相等时返回 <see langword="true" /> 否则返回 <see langword="false" /></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FP a, int b)
		{
			return a.RawValue == (long)b << 16;
		}

		/// <summary>
		/// 比较整数值与 FP 值是否相等
		/// </summary>
		/// <param name="a">整数值</param>
		/// <param name="b">FP值</param>
		/// <returns>两个值相等时返回 <see langword="true" /> 否则返回 <see langword="false" /></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(int a, FP b)
		{
			return (long)a << 16 == b.RawValue;
		}

		/// <summary>
		/// 比较两个FP值是否不相等
		/// </summary>
		/// <param name="a">第一个FP值</param>
		/// <param name="b">第二个FP值</param>
		/// <returns>两个值不相等时返回 <see langword="true" /> 否则返回 <see langword="false" /></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FP a, FP b)
		{
			return a.RawValue != b.RawValue;
		}

		/// <summary>
		/// 比较 FP 值与整数值是否不相等
		/// </summary>
		/// <param name="a">FP值</param>
		/// <param name="b">整数值</param>
		/// <returns>两个值不相等时返回 <see langword="true" /> 否则返回 <see langword="false" /></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FP a, int b)
		{
			return a.RawValue != (long)b << 16;
		}

		/// <summary>
		/// 比较整数值与 FP 值是否不相等
		/// </summary>
		/// <param name="a">整数值</param>
		/// <param name="b">FP值</param>
		/// <returns>两个值不相等时返回 <see langword="true" /> 否则返回 <see langword="false" /></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(int a, FP b)
		{
			return (long)a << 16 != b.RawValue;
		}

		/// <summary>
		/// 将整数值转换为FP值
		/// </summary>
		/// <param name="value">待转换的整数值</param>
		/// <returns>FP值</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator FP(int value)
		{
			FP result = default(FP);
			result.RawValue = (long)value << 16;
			return result;
		}

		/// <summary>
		/// 将整数值转换为FP值
		/// </summary>
		/// <param name="value">待转换的整数值</param>
		/// <returns>FP值</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator FP(uint value)
		{
			FP result = default(FP);
			result.RawValue = (long)((ulong)value << 16);
			return result;
		}

		/// <summary>
		/// 将整数值转换为FP值
		/// </summary>
		/// <param name="value">待转换的整数值</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator FP(short value)
		{
			FP result = default(FP);
			result.RawValue = (long)value << 16;
			return result;
		}

		/// <summary>
		/// 将整数值转换为FP值
		/// </summary>
		/// <param name="value">待转换的整数值</param>
		/// <returns>FP值</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator FP(ushort value)
		{
			FP result = default(FP);
			result.RawValue = (long)((ulong)value << 16);
			return result;
		}

		/// <summary>
		/// 将有符号字节值隐式转换为 FP
		/// </summary>
		/// <param name="value">待转换的有符号字节值</param>
		/// <returns>表示转换后数值的 FP 实例</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator FP(sbyte value)
		{
			FP result = default(FP);
			result.RawValue = (long)value << 16;
			return result;
		}

		/// <summary>
		/// 将字节值隐式转换为 FP 定点数值
		/// </summary>
		/// <param name="value">待转换的字节值</param>
		/// <returns>字节值转换得到的 FP 值</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator FP(byte value)
		{
			FP result = default(FP);
			result.RawValue = (long)((ulong)value << 16);
			return result;
		}

		/// <summary>
		/// 将整数值转换为FP值
		/// </summary>
		/// <param name="value">待转换的整数值</param>
		/// <returns>FP值</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator int(FP value)
		{
			return (int)(value.RawValue >> 16);
		}

		/// <summary>
		/// 将FP值转换为整数值
		/// </summary>
		/// <param name="value">待转换的 FP 值</param>
		/// <returns>整数值</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator long(FP value)
		{
			return value.RawValue >> 16;
		}

		/// <summary>
		/// 将FP值转换为单精度浮点值
		/// </summary>
		/// <param name="value">待转换的 FP 值</param>
		/// <returns>单精度浮点值</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator float(FP value)
		{
			return (float)value.RawValue / 65536f;
		}

		/// <summary>
		/// 将FP值转换为双精度浮点值
		/// </summary>
		/// <param name="value">待转换的 FP 值</param>
		/// <returns>双精度浮点值</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator double(FP value)
		{
			return (double)value.RawValue / 65536.0;
		}

		/// <summary>
		/// 尝试将单精度浮点值转换为 FP 时主动抛出异常
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		/// <exception cref="T:System.InvalidOperationException"></exception>
		[Obsolete("Don't cast from float to FP", true)]
		public static implicit operator FP(float value)
		{
			throw new InvalidOperationException();
		}

		/// <summary>
		/// 尝试将双精度浮点值转换为 FP 时主动抛出异常
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		/// <exception cref="T:System.InvalidOperationException"></exception>
		[Obsolete("Don't cast from double to FP", true)]
		public static implicit operator FP(double value)
		{
			throw new InvalidOperationException();
		}
	}
}

