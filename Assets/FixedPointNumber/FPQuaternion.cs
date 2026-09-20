using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Photon.Deterministic
{
	/// <summary>
	/// 表示旋转方向的四元数
	/// </summary>
	/// \ingroup MathAPI
	[Serializable]
	[StructLayout(LayoutKind.Explicit)]
	public struct FPQuaternion
	{
		/// <summary>
		/// 结构体在Frame数据缓冲区或栈中的内存大小作为值参数传递时适用
		/// 这与结构体占用的快照载荷无关快照载荷会按位打包并压缩
		/// </summary>
		public const int SIZE = 32;

		/// <summary>四元数的 x 分量</summary>
		[FieldOffset(0)]
		public FP X;

		/// <summary>四元数的 y 分量</summary>
		[FieldOffset(8)]
		public FP Y;

		/// <summary>四元数的 z 分量</summary>
		[FieldOffset(16)]
		public FP Z;

		/// <summary>四元数的 w 分量</summary>
		[FieldOffset(24)]
		public FP W;

		/// <summary>
		/// 表示无旋转的四元数
		/// </summary>
		public static FPQuaternion Identity => new FPQuaternion
		{
			W = 
			{
				RawValue = 65536L
			}
		};

		/// <summary>
		/// 返回模长为 1 的四元数 大多数 API 接收并返回归一化四元数
		/// 除非手动修改分量 否则通常无需再次归一化四元数
		/// </summary>
		/// <seealso cref="M:Photon.Deterministic.FPQuaternion.Normalize(Photon.Deterministic.FPQuaternion)" />
		public readonly FPQuaternion Normalized => Normalize(this);

		/// <summary>
		/// 返回当前四元数的逆 如果四元数已归一化则优先使用 <see cref="P:Photon.Deterministic.FPQuaternion.Conjugated" />
		/// </summary>
		/// <seealso cref="M:Photon.Deterministic.FPQuaternion.Inverse(Photon.Deterministic.FPQuaternion)" />
		public readonly FPQuaternion Inverted => Inverse(this);

		/// <summary>
		/// 返回此四元数的共轭 对于归一化四元数 共轭表示其逆旋转
		/// 对于归一化四元数 应使用此属性代替 <see cref="P:Photon.Deterministic.FPQuaternion.Inverted" />
		/// </summary>
		/// <seealso cref="M:Photon.Deterministic.FPQuaternion.Conjugate(Photon.Deterministic.FPQuaternion)" />
		public readonly FPQuaternion Conjugated => Conjugate(this);

		private readonly long MagnitudeSqrRaw => (X.RawValue * X.RawValue + 32768 >> 16) + (Y.RawValue * Y.RawValue + 32768 >> 16) + (Z.RawValue * Z.RawValue + 32768 >> 16) + (W.RawValue * W.RawValue + 32768 >> 16);

		/// <summary>
		/// 返回此四元数模长的平方
		/// </summary>
		public readonly FP MagnitudeSqr => FP.FromRaw(MagnitudeSqrRaw);

		/// <summary>
		/// 返回此四元数的模长
		/// </summary>
		public readonly FP Magnitude => FP.FromRaw(FPMath.SqrtRaw(MagnitudeSqrRaw));

		/// <summary>
		/// 按Z X Y轴顺序返回一种可能的欧拉角表示
		/// </summary>
		public readonly FPVector3 AsEuler => ToEulerZXY(this);

		/// <summary>
		/// 使用提供的序列化器序列化给定FPQuaternion实例
		/// </summary>
		/// <param name="ptr">FPQuaternion实例的指针</param>
		/// <param name="serializer">使用的序列化器</param>
		public unsafe static void Serialize(void* ptr, IDeterministicFrameSerializer serializer)
		{
			FP.Serialize(&((FPQuaternion*)ptr)->X, serializer);
			FP.Serialize(&((FPQuaternion*)ptr)->Y, serializer);
			FP.Serialize(&((FPQuaternion*)ptr)->Z, serializer);
			FP.Serialize(&((FPQuaternion*)ptr)->W, serializer);
		}

		/// <summary>
		/// 创建新的 FPQuaternion 实例
		/// </summary>
		/// <param name="x">x分量</param>
		/// <param name="y">y分量</param>
		/// <param name="z">z分量</param>
		/// <param name="w">w分量</param>
		public FPQuaternion(FP x, FP y, FP z, FP w)
		{
			X = x;
			Y = y;
			Z = z;
			W = w;
		}

		/// <summary>
		/// 返回表示以下对象的字符串FPQuaternion在格式 (x y z w)
		/// </summary>
		/// <returns>FPQuaternion的字符串表示</returns>
		public override readonly string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "({0:f1}, {1:f1}, {2:f1}, {3:f1})", X.AsFloat, Y.AsFloat, Z.AsFloat, W.AsFloat);
		}

		/// <summary>
		/// 返回当前 FPQuaternion 对象的哈希码
		/// </summary>
		/// <returns>当前FPQuaternion对象的哈希码</returns>
		public override readonly int GetHashCode()
		{
			int num = 17;
			num = num * 31 + X.GetHashCode();
			num = num * 31 + Y.GetHashCode();
			num = num * 31 + Z.GetHashCode();
			return num * 31 + W.GetHashCode();
		}

		/// <summary>
		/// 计算两个四元数的乘积 可用于组合两次旋转
		/// 在情况的<see cref="T:Photon.Deterministic.FPMatrix4x4" />最右侧操作数获取应用第一
		/// 此方法等价于以下伪代码
		/// <code>
		/// FPQuaternion 结果
		/// result.x = (left.w * right.x) + (left.x * right.w) + (left.y * right.z) - (left.z * right.y);
		/// result.y = (left.w * right.y) - (left.x * right.z) + (left.y * right.w) + (left.z * right.x);
		/// result.z = (left.w * right.z) + (left.x * right.y) - (left.y * right.x) + (left.z * right.w);
		/// result.w = (left.w * right.w) - (left.x * right.x) - (left.y * right.y) - (left.z * right.z);
		/// return result;
		/// </code>
		/// </summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static FPQuaternion Product(FPQuaternion left, FPQuaternion right)
		{
			long rawValue = left.X.RawValue;
			long rawValue2 = left.Y.RawValue;
			long rawValue3 = left.Z.RawValue;
			long rawValue4 = left.W.RawValue;
			long rawValue5 = right.X.RawValue;
			long rawValue6 = right.Y.RawValue;
			long rawValue7 = right.Z.RawValue;
			long rawValue8 = right.W.RawValue;
			FPQuaternion result = default(FPQuaternion);
			result.X.RawValue = (rawValue4 * rawValue5 + 32768 >> 16) + (rawValue * rawValue8 + 32768 >> 16) + (rawValue2 * rawValue7 + 32768 >> 16) - (rawValue3 * rawValue6 + 32768 >> 16);
			result.Y.RawValue = (rawValue4 * rawValue6 + 32768 >> 16) + (rawValue2 * rawValue8 + 32768 >> 16) + (rawValue3 * rawValue5 + 32768 >> 16) - (rawValue * rawValue7 + 32768 >> 16);
			result.Z.RawValue = (rawValue4 * rawValue7 + 32768 >> 16) + (rawValue3 * rawValue8 + 32768 >> 16) + (rawValue * rawValue6 + 32768 >> 16) - (rawValue2 * rawValue5 + 32768 >> 16);
			result.W.RawValue = (rawValue4 * rawValue8 + 32768 >> 16) - (rawValue * rawValue5 + 32768 >> 16) - (rawValue2 * rawValue6 + 32768 >> 16) - (rawValue3 * rawValue7 + 32768 >> 16);
			return result;
		}

		/// <summary>
		/// 返回共轭四元数 此方法等价于以下伪代码
		/// <code>
		/// return new FPQuaternion(-value.X, -value.Y, -value.Z, value.W);
		/// </code>
		/// 当 <paramref name="value" /> 已归一化时 可使用共轭代替逆四元数
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FPQuaternion Conjugate(FPQuaternion value)
		{
			value.X.RawValue = -value.X.RawValue;
			value.Y.RawValue = -value.Y.RawValue;
			value.Z.RawValue = -value.Z.RawValue;
			return value;
		}

		/// <summary>
		/// 检查四元数是否为单位四元数
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsIdentity(FPQuaternion value)
		{
			if (value.X.RawValue == 0L && value.Y.RawValue == 0L && value.Z.RawValue == 0L)
			{
				return value.W.RawValue == 65536;
			}
			return false;
		}

		/// <summary>
		/// 检查四元数是否为无效的零四元数
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsZero(FPQuaternion value)
		{
			if (value.X.RawValue == 0L && value.Y.RawValue == 0L && value.Z.RawValue == 0L)
			{
				return value.W.RawValue == 0;
			}
			return false;
		}

		/// <summary>
		/// 返回两个旋转的点积 此方法等价于以下伪代码
		/// <code>
		/// 返回 a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;
		/// </code>
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static FP Dot(FPQuaternion a, FPQuaternion b)
		{
			FP result = default(FP);
			result.RawValue = (a.W.RawValue * b.W.RawValue + 32768 >> 16) + (a.X.RawValue * b.X.RawValue + 32768 >> 16) + (a.Y.RawValue * b.Y.RawValue + 32768 >> 16) + (a.Z.RawValue * b.Z.RawValue + 32768 >> 16);
			return result;
		}

		/// <summary>
		/// 创建从 <paramref name="fromVector" /> 旋转到 <paramref name="toVector" /> 的四元数 并在内部归一化向量
		/// 如果两个向量已归一化或模长接近 1 使用 <see cref="M:Photon.Deterministic.FPQuaternion.FromToRotationSkipNormalize(Photon.Deterministic.FPVector3,Photon.Deterministic.FPVector3)" /> 性能更好
		/// </summary>
		/// <param name="fromVector"></param>
		/// <param name="toVector"></param>
		/// <returns></returns>
		public static FPQuaternion FromToRotation(FPVector3 fromVector, FPVector3 toVector)
		{
			long num = (fromVector.X.RawValue * fromVector.X.RawValue + 32768 >> 16) + (fromVector.Y.RawValue * fromVector.Y.RawValue + 32768 >> 16) + (fromVector.Z.RawValue * fromVector.Z.RawValue + 32768 >> 16);
			if (num == 0L)
			{
				return Identity;
			}
			long num2 = (toVector.X.RawValue * toVector.X.RawValue + 32768 >> 16) + (toVector.Y.RawValue * toVector.Y.RawValue + 32768 >> 16) + (toVector.Z.RawValue * toVector.Z.RawValue + 32768 >> 16);
			if (num2 == 0L)
			{
				return Identity;
			}
			long num3 = 4294967296L / FPMath.SqrtRaw(num);
			long num4 = 4294967296L / FPMath.SqrtRaw(num2);
			FPVector3 fromVector2 = default(FPVector3);
			fromVector2.X.RawValue = fromVector.X.RawValue * num3 + 32768 >> 16;
			fromVector2.Y.RawValue = fromVector.Y.RawValue * num3 + 32768 >> 16;
			fromVector2.Z.RawValue = fromVector.Z.RawValue * num3 + 32768 >> 16;
			FPVector3 toVector2 = default(FPVector3);
			toVector2.X.RawValue = toVector.X.RawValue * num4 + 32768 >> 16;
			toVector2.Y.RawValue = toVector.Y.RawValue * num4 + 32768 >> 16;
			toVector2.Z.RawValue = toVector.Z.RawValue * num4 + 32768 >> 16;
			return FromToRotationSkipNormalize(fromVector2, toVector2);
		}

		/// <summary>
		/// 创建从 <paramref name="fromVector" /> 旋转到 <paramref name="toVector" /> 的四元数 不在内部归一化向量
		/// 如果无法确定两个向量是否已归一化 请使用 <see cref="M:Photon.Deterministic.FPQuaternion.FromToRotation(Photon.Deterministic.FPVector3,Photon.Deterministic.FPVector3)" />
		/// </summary>
		/// <param name="fromVector"></param>
		/// <param name="toVector"></param>
		/// <returns></returns>
		public static FPQuaternion FromToRotationSkipNormalize(FPVector3 fromVector, FPVector3 toVector)
		{
			FPVector3 fPVector = default(FPVector3);
			fPVector.X.RawValue = fromVector.X.RawValue + toVector.X.RawValue;
			fPVector.Y.RawValue = fromVector.Y.RawValue + toVector.Y.RawValue;
			fPVector.Z.RawValue = fromVector.Z.RawValue + toVector.Z.RawValue;
			if (Math.Abs(fPVector.X.RawValue) <= 2 && Math.Abs(fPVector.Y.RawValue) <= 2 && Math.Abs(fPVector.Z.RawValue) <= 2)
			{
				long num = fromVector.X.RawValue * fromVector.X.RawValue + 32768 >> 16;
				FPVector3 b = default(FPVector3);
				if (num == 65536)
				{
					b.X.RawValue = fromVector.Z.RawValue;
					b.Y.RawValue = 0L;
					b.Z.RawValue = -fromVector.X.RawValue;
				}
				else
				{
					b.X.RawValue = 0L;
					b.Y.RawValue = -fromVector.Z.RawValue;
					b.Z.RawValue = fromVector.Y.RawValue;
				}
				b = FPVector3.Cross(fromVector, b);
				return RadianAxis(FP.Pi, b);
			}
			FPQuaternion value = default(FPQuaternion);
			value.X.RawValue = (fromVector.Y.RawValue * toVector.Z.RawValue + 32768 >> 16) - (fromVector.Z.RawValue * toVector.Y.RawValue + 32768 >> 16);
			value.Y.RawValue = (fromVector.Z.RawValue * toVector.X.RawValue + 32768 >> 16) - (fromVector.X.RawValue * toVector.Z.RawValue + 32768 >> 16);
			value.Z.RawValue = (fromVector.X.RawValue * toVector.Y.RawValue + 32768 >> 16) - (fromVector.Y.RawValue * toVector.X.RawValue + 32768 >> 16);
			value.W.RawValue = 65536 + (fromVector.X.RawValue * toVector.X.RawValue + 32768 >> 16) + (fromVector.Y.RawValue * toVector.Y.RawValue + 32768 >> 16) + (fromVector.Z.RawValue * toVector.Z.RawValue + 32768 >> 16);
			return NormalizeSmall(value);
		}

		/// <summary>
		/// 根据 <paramref name="t" /> 在 <paramref name="a" /> 与 <paramref name="b" /> 之间插值并归一化结果 参数 <paramref name="t" /> 会限制在 [0 1] 范围内
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPQuaternion Lerp(FPQuaternion a, FPQuaternion b, FP t)
		{
			if (t.RawValue < 0)
			{
				t.RawValue = 0L;
			}
			else if (t.RawValue > 65536)
			{
				t.RawValue = 65536L;
			}
			return LerpUnclamped(a, b, t);
		}

		/// <summary>
		/// 根据 <paramref name="t" /> 在 <paramref name="a" /> 与 <paramref name="b" /> 之间插值并归一化结果
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static FPQuaternion LerpUnclamped(FPQuaternion a, FPQuaternion b, FP t)
		{
			FP fP = default(FP);
			fP.RawValue = (a.W.RawValue * b.W.RawValue + 32768 >> 16) + (a.X.RawValue * b.X.RawValue + 32768 >> 16) + (a.Y.RawValue * b.Y.RawValue + 32768 >> 16) + (a.Z.RawValue * b.Z.RawValue + 32768 >> 16);
			if (fP.RawValue < 0)
			{
				b.X.RawValue = -b.X.RawValue;
				b.Y.RawValue = -b.Y.RawValue;
				b.Z.RawValue = -b.Z.RawValue;
				b.W.RawValue = -b.W.RawValue;
				fP.RawValue = -fP.RawValue;
			}
			long num = 65536 - t.RawValue;
			a.X.RawValue = a.X.RawValue * num + 32768 >> 16;
			a.Y.RawValue = a.Y.RawValue * num + 32768 >> 16;
			a.Z.RawValue = a.Z.RawValue * num + 32768 >> 16;
			a.W.RawValue = a.W.RawValue * num + 32768 >> 16;
			b.X.RawValue = b.X.RawValue * t.RawValue + 32768 >> 16;
			b.Y.RawValue = b.Y.RawValue * t.RawValue + 32768 >> 16;
			b.Z.RawValue = b.Z.RawValue * t.RawValue + 32768 >> 16;
			b.W.RawValue = b.W.RawValue * t.RawValue + 32768 >> 16;
			a.X.RawValue = a.X.RawValue + b.X.RawValue;
			a.Y.RawValue = a.Y.RawValue + b.Y.RawValue;
			a.Z.RawValue = a.Z.RawValue + b.Z.RawValue;
			a.W.RawValue = a.W.RawValue + b.W.RawValue;
			return Normalize(a);
		}

		/// <summary>
		/// 创建欧拉旋转 <paramref name="roll" /> 绕 z 轴 <paramref name="pitch" /> 绕 x 轴 <paramref name="yaw" /> 绕 y 轴 单位为弧度
		/// </summary>
		/// <param name="yaw">偏航角在弧度</param>
		/// <param name="pitch">俯仰角在弧度</param>
		/// <param name="roll">滚转角在弧度</param>
		/// <returns></returns>
		public static FPQuaternion CreateFromYawPitchRoll(FP yaw, FP pitch, FP roll)
		{
			FP rad = default(FP);
			rad.RawValue = roll.RawValue / 2;
			FP rad2 = default(FP);
			rad2.RawValue = pitch.RawValue / 2;
			FP rad3 = default(FP);
			rad3.RawValue = yaw.RawValue / 2;
			FPMath.SinCosRaw(rad, out var sinRaw, out var cosRaw);
			FPMath.SinCosRaw(rad2, out var sinRaw2, out var cosRaw2);
			FPMath.SinCosRaw(rad3, out var sinRaw3, out var cosRaw3);
			FPQuaternion result = default(FPQuaternion);
			result.X.RawValue = ((cosRaw3 * sinRaw2 + 32768 >> 16) * cosRaw + 32768 >> 16) + ((sinRaw3 * cosRaw2 + 32768 >> 16) * sinRaw + 32768 >> 16);
			result.Y.RawValue = ((sinRaw3 * cosRaw2 + 32768 >> 16) * cosRaw + 32768 >> 16) - ((cosRaw3 * sinRaw2 + 32768 >> 16) * sinRaw + 32768 >> 16);
			result.Z.RawValue = ((cosRaw3 * cosRaw2 + 32768 >> 16) * sinRaw + 32768 >> 16) - ((sinRaw3 * sinRaw2 + 32768 >> 16) * cosRaw + 32768 >> 16);
			result.W.RawValue = ((cosRaw3 * cosRaw2 + 32768 >> 16) * cosRaw + 32768 >> 16) + ((sinRaw3 * sinRaw2 + 32768 >> 16) * sinRaw + 32768 >> 16);
			return result;
		}

		/// <summary>
		/// 返回旋转 <paramref name="a" /> 与 <paramref name="b" /> 之间的夹角 单位为度
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FP Angle(FPQuaternion a, FPQuaternion b)
		{
			return FP.FromRaw(AngleRadians(a, b).RawValue * 3754936 + 32768 >> 16);
		}

		/// <summary>
		/// 返回旋转 <paramref name="a" /> 与 <paramref name="b" /> 之间的夹角 单位为弧度
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static FP AngleRadians(FPQuaternion a, FPQuaternion b)
		{
			FP fP = default(FP);
			fP.RawValue = (a.X.RawValue * a.X.RawValue + 32768 >> 16) + (a.Y.RawValue * a.Y.RawValue + 32768 >> 16) + (a.Z.RawValue * a.Z.RawValue + 32768 >> 16) + (a.W.RawValue * a.W.RawValue + 32768 >> 16);
			long num = 4294967296L / fP.RawValue;
			a.X.RawValue = -a.X.RawValue;
			a.Y.RawValue = -a.Y.RawValue;
			a.Z.RawValue = -a.Z.RawValue;
			a.X.RawValue = a.X.RawValue * num + 32768 >> 16;
			a.Y.RawValue = a.Y.RawValue * num + 32768 >> 16;
			a.Z.RawValue = a.Z.RawValue * num + 32768 >> 16;
			a.W.RawValue = a.W.RawValue * num + 32768 >> 16;
			long rawValue = b.X.RawValue;
			long rawValue2 = b.Y.RawValue;
			long rawValue3 = b.Z.RawValue;
			long rawValue4 = b.W.RawValue;
			long rawValue5 = a.X.RawValue;
			long rawValue6 = a.Y.RawValue;
			long rawValue7 = a.Z.RawValue;
			long rawValue8 = a.W.RawValue;
			FPQuaternion fPQuaternion = default(FPQuaternion);
			fPQuaternion.X.RawValue = (rawValue5 * rawValue4 + 32768 >> 16) + (rawValue * rawValue8 + 32768 >> 16) + (rawValue6 * rawValue3 + 32768 >> 16) - (rawValue7 * rawValue2 + 32768 >> 16);
			fPQuaternion.Y.RawValue = (rawValue6 * rawValue4 + 32768 >> 16) + (rawValue2 * rawValue8 + 32768 >> 16) + (rawValue7 * rawValue + 32768 >> 16) - (rawValue5 * rawValue3 + 32768 >> 16);
			fPQuaternion.Z.RawValue = (rawValue7 * rawValue4 + 32768 >> 16) + (rawValue3 * rawValue8 + 32768 >> 16) + (rawValue5 * rawValue2 + 32768 >> 16) - (rawValue6 * rawValue + 32768 >> 16);
			fPQuaternion.W.RawValue = (rawValue8 * rawValue4 + 32768 >> 16) - ((rawValue5 * rawValue + 32768 >> 16) + (rawValue6 * rawValue2 + 32768 >> 16) + (rawValue7 * rawValue3 + 32768 >> 16));
			FP result = default(FP);
			result.RawValue = FPMath.Acos(fPQuaternion.W).RawValue << 1;
			if (result.RawValue > 205887)
			{
				result.RawValue = 411775 - result.RawValue;
			}
			return result;
		}

		/// <summary>
		/// 已弃用请使用接收前方向的重载此重载使用FPVector3.Up作为上方向且不进行正交归一化
		/// 或接收前方向和上方向的重载并可选择进行正交归一化
		/// </summary>
		[Obsolete("Use one of the overloads that receive either only a forward direction (uses FPVector3.Up as up direction, not ortho-normalized), OR forward and up directions, which can be optionally ortho-normalized.")]
		public static FPQuaternion LookRotation(FPVector3 forward, bool orthoNormalize)
		{
			return LookRotation(forward);
		}

		/// <summary>
		/// 使用指定 <paramref name="forward" /> 方向和 <see cref="P:Photon.Deterministic.FPVector3.Up" /> 创建旋转
		/// </summary>
		/// <param name="forward"></param>
		/// <returns></returns>
		public static FPQuaternion LookRotation(FPVector3 forward)
		{
			return LookRotation(forward, FPVector3.Up);
		}

		/// <summary>
		/// 使用指定 <paramref name="forward" /> 与 <paramref name="up" /> 方向创建旋转
		/// </summary>
		/// <param name="forward"></param>
		/// <param name="up"></param>
		/// <param name="orthoNormalize"></param>
		/// <returns></returns>
		public static FPQuaternion LookRotation(FPVector3 forward, FPVector3 up, bool orthoNormalize = false)
		{
			forward = FPVector3.Normalize(forward, out var magnitude);
			if (magnitude.RawValue == 0L)
			{
				return Identity;
			}
			if (orthoNormalize)
			{
				up -= forward * FPVector3.Dot(up, forward);
				up = up.Normalized;
			}
			FPVector3 normalized = FPVector3.Cross(up, forward).Normalized;
			if (!orthoNormalize)
			{
				up = FPVector3.Cross(forward, normalized);
			}
			long num = Math.Abs(FPVector3.Dot(up, forward).RawValue);
			if (num >= 65536 || up.SqrMagnitude.RawValue == 0L)
			{
				return FromToRotationSkipNormalize(FPVector3.Forward, forward);
			}
			return FPMatrix3x3.FromColumns(normalized, up, forward).Rotation;
		}

		[Obsolete("SimpleLookAt is a cheaper version of LookRotation, but there are no extensive tests to ensure its correctness and equivalency to Unity's Quaternion.LookRotation as the latter has. We recommend using LookRotation instead.")]
		public static FPQuaternion SimpleLookAt(FPVector3 direction)
		{
			return SimpleLookAt(direction, FPVector3.Forward, FPVector3.Up);
		}

		[Obsolete("SimpleLookAt is a cheaper version of LookRotation, but there are no extensive tests to ensure its correctness and equivalency to Unity's Quaternion.LookRotation as the latter has. We recommend using LookRotation instead.")]
		public static FPQuaternion SimpleLookAt(FPVector3 direction, FPVector3 up)
		{
			return SimpleLookAt(direction, FPVector3.Forward, up);
		}

		[Obsolete("SimpleLookAt is a cheaper version of LookRotation, but there are no extensive tests to ensure its correctness and equivalency to Unity's Quaternion.LookRotation as the latter has. We recommend using LookRotation instead.")]
		public static FPQuaternion SimpleLookAt(FPVector3 direction, FPVector3 forward, FPVector3 up)
		{
			FPVector3 axis = FPVector3.Cross(forward, direction);
			if (axis.SqrMagnitude < FP.EN4)
			{
				axis = up;
			}
			FP value = FPVector3.Dot(forward, direction);
			FP angle = FPMath.Acos(value);
			return AngleAxis(angle, axis);
		}

		/// <summary>
		/// 根据 <paramref name="t" /> 在 <paramref name="from" /> 与 <paramref name="to" /> 之间进行球面插值并归一化结果 参数 <paramref name="t" /> 会限制在 [0 1] 范围内
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPQuaternion Slerp(FPQuaternion from, FPQuaternion to, FP t)
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
		/// 根据 <paramref name="t" /> 在 <paramref name="from" /> 与 <paramref name="to" /> 之间进行球面插值并归一化结果
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static FPQuaternion SlerpUnclamped(FPQuaternion from, FPQuaternion to, FP t)
		{
			FP value = default(FP);
			value.RawValue = (from.W.RawValue * to.W.RawValue + 32768 >> 16) + (from.X.RawValue * to.X.RawValue + 32768 >> 16) + (from.Y.RawValue * to.Y.RawValue + 32768 >> 16) + (from.Z.RawValue * to.Z.RawValue + 32768 >> 16);
			if (value.RawValue < 0)
			{
				to.X.RawValue = -to.X.RawValue;
				to.Y.RawValue = -to.Y.RawValue;
				to.Z.RawValue = -to.Z.RawValue;
				to.W.RawValue = -to.W.RawValue;
				value.RawValue = -value.RawValue;
			}
			if (value.RawValue >= 64881)
			{
				return LerpUnclamped(from, to, t);
			}
			FP rad = FPMath.Acos(value);
			FP rad2 = default(FP);
			rad2.RawValue = (65536 - t.RawValue) * rad.RawValue + 32768 >> 16;
			FP rad3 = default(FP);
			rad3.RawValue = t.RawValue * rad.RawValue + 32768 >> 16;
			FP fP = default(FP);
			fP.RawValue = 4294967296L / FPMath.Sin(rad).RawValue;
			rad2 = FPMath.Sin(rad2);
			rad3 = FPMath.Sin(rad3);
			from.X.RawValue = from.X.RawValue * rad2.RawValue + 32768 >> 16;
			from.Y.RawValue = from.Y.RawValue * rad2.RawValue + 32768 >> 16;
			from.Z.RawValue = from.Z.RawValue * rad2.RawValue + 32768 >> 16;
			from.W.RawValue = from.W.RawValue * rad2.RawValue + 32768 >> 16;
			to.X.RawValue = to.X.RawValue * rad3.RawValue + 32768 >> 16;
			to.Y.RawValue = to.Y.RawValue * rad3.RawValue + 32768 >> 16;
			to.Z.RawValue = to.Z.RawValue * rad3.RawValue + 32768 >> 16;
			to.W.RawValue = to.W.RawValue * rad3.RawValue + 32768 >> 16;
			from.X.RawValue += to.X.RawValue;
			from.Y.RawValue += to.Y.RawValue;
			from.Z.RawValue += to.Z.RawValue;
			from.W.RawValue += to.W.RawValue;
			from.X.RawValue = from.X.RawValue * fP.RawValue + 32768 >> 16;
			from.Y.RawValue = from.Y.RawValue * fP.RawValue + 32768 >> 16;
			from.Z.RawValue = from.Z.RawValue * fP.RawValue + 32768 >> 16;
			from.W.RawValue = from.W.RawValue * fP.RawValue + 32768 >> 16;
			return Normalize(from);
		}

		/// <summary>
		/// 将旋转 <paramref name="from" /> 朝 <paramref name="to" /> 旋转 每次最多转动 <paramref name="maxDegreesDelta" /> 度
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <param name="maxDegreesDelta"></param>
		/// <returns></returns>
		public static FPQuaternion RotateTowards(FPQuaternion from, FPQuaternion to, FP maxDegreesDelta)
		{
			FP value = default(FP);
			value.RawValue = (from.W.RawValue * to.W.RawValue + 32768 >> 16) + (from.X.RawValue * to.X.RawValue + 32768 >> 16) + (from.Y.RawValue * to.Y.RawValue + 32768 >> 16) + (from.Z.RawValue * to.Z.RawValue + 32768 >> 16);
			if (value.RawValue < 0)
			{
				to.X.RawValue = -to.X.RawValue;
				to.Y.RawValue = -to.Y.RawValue;
				to.Z.RawValue = -to.Z.RawValue;
				to.W.RawValue = -to.W.RawValue;
				value.RawValue = -value.RawValue;
			}
			FP fP = FPMath.Acos(value);
			FP fP2 = default(FP);
			fP2.RawValue = fP.RawValue << 1;
			maxDegreesDelta.RawValue = maxDegreesDelta.RawValue * FP.Deg2Rad.RawValue + 32768 >> 16;
			if (maxDegreesDelta.RawValue >= fP2.RawValue)
			{
				return to;
			}
			maxDegreesDelta.RawValue = (maxDegreesDelta.RawValue << 16) / fP2.RawValue;
			FP rad = (1 - maxDegreesDelta) * fP;
			FP rad2 = maxDegreesDelta * fP;
			rad = FPMath.Sin(rad);
			rad2 = FPMath.Sin(rad2);
			from.X.RawValue = from.X.RawValue * rad.RawValue + 32768 >> 16;
			from.Y.RawValue = from.Y.RawValue * rad.RawValue + 32768 >> 16;
			from.Z.RawValue = from.Z.RawValue * rad.RawValue + 32768 >> 16;
			from.W.RawValue = from.W.RawValue * rad.RawValue + 32768 >> 16;
			to.X.RawValue = to.X.RawValue * rad2.RawValue + 32768 >> 16;
			to.Y.RawValue = to.Y.RawValue * rad2.RawValue + 32768 >> 16;
			to.Z.RawValue = to.Z.RawValue * rad2.RawValue + 32768 >> 16;
			to.W.RawValue = to.W.RawValue * rad2.RawValue + 32768 >> 16;
			rad.RawValue = 4294967296L / FPMath.Sin(fP).RawValue;
			from.X.RawValue += to.X.RawValue;
			from.Y.RawValue += to.Y.RawValue;
			from.Z.RawValue += to.Z.RawValue;
			from.W.RawValue += to.W.RawValue;
			from.X.RawValue = from.X.RawValue * rad.RawValue + 32768 >> 16;
			from.Y.RawValue = from.Y.RawValue * rad.RawValue + 32768 >> 16;
			from.Z.RawValue = from.Z.RawValue * rad.RawValue + 32768 >> 16;
			from.W.RawValue = from.W.RawValue * rad.RawValue + 32768 >> 16;
			return from;
		}

		/// <summary>
		/// 创建欧拉旋转 <paramref name="z" /> 绕 z 轴 <paramref name="x" /> 绕 x 轴 <paramref name="y" /> 绕 y 轴 单位为度
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="z"></param>
		/// <returns></returns>
		public static FPQuaternion Euler(FP x, FP y, FP z)
		{
			x.RawValue = x.RawValue * FP.Deg2Rad.RawValue + 32768 >> 16;
			y.RawValue = y.RawValue * FP.Deg2Rad.RawValue + 32768 >> 16;
			z.RawValue = z.RawValue * FP.Deg2Rad.RawValue + 32768 >> 16;
			return CreateFromYawPitchRoll(y, x, z);
		}

		/// <summary>
		/// 根据 <paramref name="eulerAngles" /> 创建欧拉旋转 分别绕 z x y 轴旋转对应角度
		/// </summary>
		/// <param name="eulerAngles"></param>
		/// <returns></returns>
		public static FPQuaternion Euler(FPVector3 eulerAngles)
		{
			eulerAngles.X.RawValue = eulerAngles.X.RawValue * FP.Deg2Rad.RawValue + 32768 >> 16;
			eulerAngles.Y.RawValue = eulerAngles.Y.RawValue * FP.Deg2Rad.RawValue + 32768 >> 16;
			eulerAngles.Z.RawValue = eulerAngles.Z.RawValue * FP.Deg2Rad.RawValue + 32768 >> 16;
			return CreateFromYawPitchRoll(eulerAngles.Y, eulerAngles.X, eulerAngles.Z);
		}

		/// <summary>
		/// 创建绕 <paramref name="axis" /> 旋转 <paramref name="angle" /> 度的旋转
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="angle"></param>
		/// <param name="axis"></param>
		/// <returns></returns>
		public static FPQuaternion AngleAxis(FP angle, FPVector3 axis)
		{
			axis = FPVector3.Normalize(axis);
			FP rad = default(FP);
			rad.RawValue = (angle.RawValue * FP.Deg2Rad.RawValue + 32768 >> 16) / 2;
			FPMath.SinCos(rad, out var sin, out var cos);
			FPQuaternion result = default(FPQuaternion);
			result.X.RawValue = axis.X.RawValue * sin.RawValue + 32768 >> 16;
			result.Y.RawValue = axis.Y.RawValue * sin.RawValue + 32768 >> 16;
			result.Z.RawValue = axis.Z.RawValue * sin.RawValue + 32768 >> 16;
			result.W = cos;
			return result;
		}

		/// <summary>
		/// 创建绕 <paramref name="axis" /> 旋转 <paramref name="radians" /> 弧度的旋转
		/// </summary>
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// <param name="radians"></param>
		/// <param name="axis"></param>
		/// <returns></returns>
		public static FPQuaternion RadianAxis(FP radians, FPVector3 axis)
		{
			axis = FPVector3.Normalize(axis);
			FP rad = default(FP);
			rad.RawValue = radians.RawValue / 2;
			FPMath.SinCos(rad, out var sin, out var cos);
			FPQuaternion result = default(FPQuaternion);
			result.X.RawValue = axis.X.RawValue * sin.RawValue + 32768 >> 16;
			result.Y.RawValue = axis.Y.RawValue * sin.RawValue + 32768 >> 16;
			result.Z.RawValue = axis.Z.RawValue * sin.RawValue + 32768 >> 16;
			result.W = cos;
			return result;
		}

		/// <summary>
		/// 返回 <paramref name="value" /> 的逆旋转 如果输入已归一化则优先使用共轭
		/// 当 <paramref name="value" /> 已归一化时 调用 <see cref="M:Photon.Deterministic.FPQuaternion.Conjugate(Photon.Deterministic.FPQuaternion)" /> 会更快
		/// 当 <paramref name="value" /> 的模长接近 0 时返回原值
		/// <remarks><see cref="T:Photon.Deterministic.FPLut" />需要先初始化</remarks>
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FPQuaternion Inverse(FPQuaternion value)
		{
			long magnitudeSqrRaw = value.MagnitudeSqrRaw;
			if (magnitudeSqrRaw == 0L)
			{
				return value;
			}
			magnitudeSqrRaw = 4294967296L / FPMath.SqrtRaw(magnitudeSqrRaw);
			FPQuaternion result = default(FPQuaternion);
			result.X.RawValue = -value.X.RawValue * magnitudeSqrRaw + 32768 >> 16;
			result.Y.RawValue = -value.Y.RawValue * magnitudeSqrRaw + 32768 >> 16;
			result.Z.RawValue = -value.Z.RawValue * magnitudeSqrRaw + 32768 >> 16;
			result.W.RawValue = value.W.RawValue * magnitudeSqrRaw + 32768 >> 16;
			return result;
		}

		/// <summary>
		/// 将 <paramref name="value" /> 转换为方向相同且模长为 1 的四元数
		/// 当 <paramref name="value" /> 的模长接近 0 时返回 <see cref="P:Photon.Deterministic.FPQuaternion.Identity" />
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FPQuaternion Normalize(FPQuaternion value)
		{
			long magnitudeSqrRaw = value.MagnitudeSqrRaw;
			if (magnitudeSqrRaw == 0L)
			{
				return Identity;
			}
			magnitudeSqrRaw = 4294967296L / FPMath.SqrtRaw(magnitudeSqrRaw);
			FPQuaternion result = default(FPQuaternion);
			result.X.RawValue = value.X.RawValue * magnitudeSqrRaw + 32768 >> 16;
			result.Y.RawValue = value.Y.RawValue * magnitudeSqrRaw + 32768 >> 16;
			result.Z.RawValue = value.Z.RawValue * magnitudeSqrRaw + 32768 >> 16;
			result.W.RawValue = value.W.RawValue * magnitudeSqrRaw + 32768 >> 16;
			return result;
		}

		internal static FPQuaternion NormalizeSmall(FPQuaternion value)
		{
			ulong num = (ulong)(value.X.RawValue * value.X.RawValue + value.Y.RawValue * value.Y.RawValue + value.Z.RawValue * value.Z.RawValue + value.W.RawValue * value.W.RawValue);
			if (num == 0L)
			{
				return Identity;
			}
			FPMath.ExponentMantisaPair sqrtExponentMantissa = FPMath.GetSqrtExponentMantissa(num);
			long num2 = 17592186044416L / sqrtExponentMantissa.Mantissa;
			value.X.RawValue = value.X.RawValue * num2 >> 22 + sqrtExponentMantissa.Exponent - 8;
			value.Y.RawValue = value.Y.RawValue * num2 >> 22 + sqrtExponentMantissa.Exponent - 8;
			value.Z.RawValue = value.Z.RawValue * num2 >> 22 + sqrtExponentMantissa.Exponent - 8;
			value.W.RawValue = value.W.RawValue * num2 >> 22 + sqrtExponentMantissa.Exponent - 8;
			return value;
		}

		internal static FPVector3 ToEulerZXY(FPQuaternion value)
		{
			long rawValue = value.X.RawValue;
			long rawValue2 = value.Y.RawValue;
			long rawValue3 = value.Z.RawValue;
			long rawValue4 = value.W.RawValue;
			FP value2 = default(FP);
			value2.RawValue = (rawValue4 * rawValue >> 15) - (rawValue2 * rawValue3 >> 15);
			value2.RawValue = ((value2.RawValue > FP._1.RawValue) ? FP._1.RawValue : value2.RawValue);
			value2.RawValue = ((value2.RawValue < -FP._1.RawValue) ? (-FP._1.RawValue) : value2.RawValue);
			FPVector3 result = new FPVector3
			{
				X = FPMath.Asin(value2)
			};
			if (FPMath.Abs(value2).RawValue < 65533)
			{
				FP y = default(FP);
				y.RawValue = (rawValue * rawValue3 >> 15) + (rawValue4 * rawValue2 >> 15);
				FP x = default(FP);
				x.RawValue = FP._1.RawValue - (rawValue * rawValue >> 15) - (rawValue2 * rawValue2 >> 15);
				FP y2 = default(FP);
				y2.RawValue = (rawValue * rawValue2 >> 15) + (rawValue4 * rawValue3 >> 15);
				FP x2 = default(FP);
				x2.RawValue = FP._1.RawValue - (rawValue * rawValue >> 15) - (rawValue3 * rawValue3 >> 15);
				result.Y = FPMath.Atan2(y, x);
				result.Z = FPMath.Atan2(y2, x2);
			}
			else
			{
				FP y3 = default(FP);
				y3.RawValue = (rawValue4 * rawValue2 >> 15) - (rawValue * rawValue3 >> 15);
				FP x3 = default(FP);
				x3.RawValue = FP._1.RawValue - (rawValue2 * rawValue2 >> 15) - (rawValue3 * rawValue3 >> 15);
				result.Y = FPMath.Atan2(y3, x3);
				result.Z = 0;
			}
			result.X.RawValue = result.X.RawValue * FP.Rad2Deg.RawValue + 32768 >> 16;
			result.Y.RawValue = result.Y.RawValue * FP.Rad2Deg.RawValue + 32768 >> 16;
			result.Z.RawValue = result.Z.RawValue * FP.Rad2Deg.RawValue + 32768 >> 16;
			return result;
		}

		/// <summary>
		/// 计算两个四元数的乘积 与 Unity 四元数乘法完全等价
		/// 详情请参阅<see cref="M:Photon.Deterministic.FPQuaternion.Product(Photon.Deterministic.FPQuaternion,Photon.Deterministic.FPQuaternion)" />
		/// </summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPQuaternion operator *(FPQuaternion left, FPQuaternion right)
		{
			return Product(left, right);
		}

		/// <summary>
		/// 将四元数 <paramref name="left" /> 与标量 <paramref name="right" /> 相乘
		/// </summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static FPQuaternion operator *(FPQuaternion left, FP right)
		{
			left.X.RawValue = left.X.RawValue * right.RawValue + 32768 >> 16;
			left.Y.RawValue = left.Y.RawValue * right.RawValue + 32768 >> 16;
			left.Z.RawValue = left.Z.RawValue * right.RawValue + 32768 >> 16;
			left.W.RawValue = left.W.RawValue * right.RawValue + 32768 >> 16;
			return left;
		}

		/// <summary>
		/// 将标量 <paramref name="left" /> 与四元数 <paramref name="right" /> 相乘
		/// </summary>
		/// <param name="right"></param>
		/// <param name="left"></param>
		/// <returns></returns>
		public static FPQuaternion operator *(FP left, FPQuaternion right)
		{
			return right * left;
		}

		/// <summary>
		/// 将 <paramref name="right" /> 的各分量加到 <paramref name="left" />
		/// </summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static FPQuaternion operator +(FPQuaternion left, FPQuaternion right)
		{
			left.X.RawValue += right.X.RawValue;
			left.Y.RawValue += right.Y.RawValue;
			left.Z.RawValue += right.Z.RawValue;
			left.W.RawValue += right.W.RawValue;
			return left;
		}

		/// <summary>
		/// 从 <paramref name="left" /> 的每个分量中减去 <paramref name="right" /> 的对应分量
		/// </summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static FPQuaternion operator -(FPQuaternion left, FPQuaternion right)
		{
			left.X.RawValue -= right.X.RawValue;
			left.Y.RawValue -= right.Y.RawValue;
			left.Z.RawValue -= right.Z.RawValue;
			left.W.RawValue -= right.W.RawValue;
			return left;
		}

		/// <summary>
		/// 使用四元数 <paramref name="quat" /> 旋转点 <paramref name="point" />
		/// </summary>
		/// <param name="quat"></param>
		/// <param name="point"></param>
		/// <returns></returns>
		public static FPVector3 operator *(FPQuaternion quat, FPVector3 point)
		{
			long rawValue = quat.X.RawValue;
			long rawValue2 = quat.Y.RawValue;
			long rawValue3 = quat.Z.RawValue;
			long rawValue4 = quat.W.RawValue;
			long rawValue5 = point.X.RawValue;
			long rawValue6 = point.Y.RawValue;
			long rawValue7 = point.Z.RawValue;
			long rawValue8 = FP._1.RawValue;
			long num = rawValue << 1;
			long num2 = rawValue2 << 1;
			long num3 = rawValue3 << 1;
			long num4 = rawValue * num + 32768 >> 16;
			long num5 = rawValue2 * num2 + 32768 >> 16;
			long num6 = rawValue3 * num3 + 32768 >> 16;
			long num7 = rawValue * num2 + 32768 >> 16;
			long num8 = rawValue * num3 + 32768 >> 16;
			long num9 = rawValue2 * num3 + 32768 >> 16;
			long num10 = rawValue4 * num + 32768 >> 16;
			long num11 = rawValue4 * num2 + 32768 >> 16;
			long num12 = rawValue4 * num3 + 32768 >> 16;
			FPVector3 result = default(FPVector3);
			result.X.RawValue = ((rawValue8 - (num5 + num6)) * rawValue5 + 32768 >> 16) + ((num7 - num12) * rawValue6 + 32768 >> 16) + ((num8 + num11) * rawValue7 + 32768 >> 16);
			result.Y.RawValue = ((num7 + num12) * rawValue5 + 32768 >> 16) + ((rawValue8 - (num4 + num6)) * rawValue6 + 32768 >> 16) + ((num9 - num10) * rawValue7 + 32768 >> 16);
			result.Z.RawValue = ((num8 - num11) * rawValue5 + 32768 >> 16) + ((num9 + num10) * rawValue6 + 32768 >> 16) + ((rawValue8 - (num4 + num5)) * rawValue7 + 32768 >> 16);
			return result;
		}
	}
}

