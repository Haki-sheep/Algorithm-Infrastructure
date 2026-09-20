using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Photon.Deterministic
{
	/// <summary>
	/// 表示用于二维缩放和旋转的 2x2 列主序矩阵
	/// 每个元素均可通过 M行列 字段单独访问
	/// </summary>
	/// \ingroup MathAPI
	[Serializable]
	[StructLayout(LayoutKind.Explicit)]
	public struct FPMatrix2x2
	{
		/// <summary>
		/// 结构体在Frame数据缓冲区或栈中的内存大小作为值参数传递时适用
		/// 这与结构体占用的快照载荷无关快照载荷会按位打包并压缩
		/// </summary>
		public const int SIZE = 32;

		/// <summary>
		/// 2x2 矩阵第一行第一列的元素
		/// </summary>
		[FieldOffset(0)]
		public FP M00;

		/// <summary>
		/// 2x2 矩阵第二行第二列的元素
		/// </summary>
		[FieldOffset(8)]
		public FP M10;

		/// <summary>
		/// 2x2 矩阵第一行第二列的元素
		/// </summary>
		[FieldOffset(16)]
		public FP M01;

		/// <summary>
		/// 2x2 矩阵第二行第一列的元素
		/// </summary>
		[FieldOffset(24)]
		public FP M11;

		/// <summary>
		/// 所有元素均为 0 的矩阵
		/// </summary>
		public static FPMatrix2x2 Zero => default(FPMatrix2x2);

		/// <summary>
		/// 主对角线元素为 1 其余元素为 0 的矩阵
		/// </summary>
		public static FPMatrix2x2 Identity => new FPMatrix2x2
		{
			M00 = 
			{
				RawValue = 65536L
			},
			M11 = 
			{
				RawValue = 65536L
			}
		};

		/// <summary>
		/// 如果此矩阵等于 <see cref="P:Photon.Deterministic.FPMatrix2x2.Identity" /> 则返回 <see langword="true" />
		/// </summary>
		public readonly bool IsIdentity
		{
			get
			{
				if (M00.RawValue == 65536 && M11.RawValue == 65536)
				{
					return (M01.RawValue | M10.RawValue) == 0;
				}
				return false;
			}
		}

		/// <summary>
		/// 获取或设置指定索引对应的元素
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public FP this[int index]
		{
			readonly get
			{
				return index switch
				{
					0 => M00, 
					1 => M10, 
					2 => M01, 
					3 => M11, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}
			set
			{
				switch (index)
				{
				case 0:
					M00 = value;
					break;
				case 1:
					M10 = value;
					break;
				case 2:
					M01 = value;
					break;
				case 3:
					M11 = value;
					break;
				default:
					throw new ArgumentOutOfRangeException();
				}
			}
		}

		/// <summary>
		/// 尝试从矩阵中获取缩放值
		/// </summary>
		public readonly FPVector2 LossyScale
		{
			get
			{
				long x = (M00.RawValue * M00.RawValue + 32768 >> 16) + (M10.RawValue * M10.RawValue + 32768 >> 16);
				long x2 = (M01.RawValue * M01.RawValue + 32768 >> 16) + (M11.RawValue * M11.RawValue + 32768 >> 16);
				return new FPVector2(FP.FromRaw(FPMath.SqrtRaw(x) * FPMath.SignInt(Determinant)), FP.FromRaw(FPMath.SqrtRaw(x2)));
			}
		}

		/// <summary>
		/// 创建逆矩阵 行列式为 0 时矩阵不可逆并返回 <see cref="P:Photon.Deterministic.FPMatrix2x2.Zero" />
		/// </summary>
		public readonly FPMatrix2x2 Inverted
		{
			get
			{
				long num = (M00.RawValue * M11.RawValue + 32768 >> 16) - (M10.RawValue * M01.RawValue + 32768 >> 16);
				if (num == 0L)
				{
					return Zero;
				}
				long num2 = 4294967296L / num;
				FPMatrix2x2 result = default(FPMatrix2x2);
				result.M00.RawValue = M11.RawValue * num2 + 32768 >> 16;
				result.M01.RawValue = -(M01.RawValue * num2 + 32768 >> 16);
				result.M10.RawValue = -(M10.RawValue * num2 + 32768 >> 16);
				result.M11.RawValue = M00.RawValue * num2 + 32768 >> 16;
				return result;
			}
		}

		/// <summary>
		/// 计算此矩阵的行列式
		/// </summary>
		public readonly FP Determinant => FP.FromRaw((M00.RawValue * M11.RawValue + 32768 >> 16) - (M10.RawValue * M01.RawValue + 32768 >> 16));

		/// <summary>
		/// 根据两个行向量创建矩阵
		/// </summary>
		public static FPMatrix2x2 FromRows(FP m00, FP m01, FP m10, FP m11)
		{
			FPMatrix2x2 result = default(FPMatrix2x2);
			result.M00 = m00;
			result.M10 = m10;
			result.M01 = m01;
			result.M11 = m11;
			return result;
		}

		/// <summary>
		/// 根据行创建矩阵 第一个向量设置第一行 第二个向量设置第二行
		/// </summary>
		public static FPMatrix2x2 FromRows(FPVector2 row0, FPVector2 row1)
		{
			FPMatrix2x2 result = default(FPMatrix2x2);
			result.M00 = row0.X;
			result.M10 = row1.X;
			result.M01 = row0.Y;
			result.M11 = row1.Y;
			return result;
		}

		/// <summary>
		/// 根据列创建前两个值设置第一列后两个值设置第二列
		/// </summary>
		public static FPMatrix2x2 FromColumns(FP m00, FP m10, FP m01, FP m11)
		{
			FPMatrix2x2 result = default(FPMatrix2x2);
			result.M00 = m00;
			result.M10 = m10;
			result.M01 = m01;
			result.M11 = m11;
			return result;
		}

		/// <summary>
		/// 根据列创建矩阵 第一个向量设置第一列 第二个向量设置第二列
		/// </summary>
		public static FPMatrix2x2 FromColumns(FPVector2 column0, FPVector2 column1)
		{
			FPMatrix2x2 result = default(FPMatrix2x2);
			result.M00 = column0.X;
			result.M10 = column0.Y;
			result.M01 = column1.X;
			result.M11 = column1.Y;
			return result;
		}

		/// <summary>
		/// 创建旋转矩阵
		/// </summary>
		/// <param name="rotation">旋转在弧度</param>
		public static FPMatrix2x2 Rotate(FP rotation)
		{
			FPMatrix2x2 result = default(FPMatrix2x2);
			FPMath.SinCos(rotation, out result.M01, out result.M00);
			result.M10 = -result.M01;
			result.M11 = result.M00;
			return result;
		}

		/// <summary>
		/// 创建缩放矩阵
		/// </summary>
		public static FPMatrix2x2 Scale(FPVector2 scale)
		{
			return FromColumns(scale.X, 0, 0, scale.Y);
		}

		/// <summary>
		/// 使用此矩阵变换方向向量
		/// </summary>
		public readonly FPVector2 MultiplyVector(FPVector2 v)
		{
			FPVector2 result = default(FPVector2);
			result.X.RawValue = (M00.RawValue * v.X.RawValue + 32768 >> 16) + (M01.RawValue * v.Y.RawValue + 32768 >> 16);
			result.Y.RawValue = (M10.RawValue * v.X.RawValue + 32768 >> 16) + (M11.RawValue * v.Y.RawValue + 32768 >> 16);
			return result;
		}

		/// <summary>
		/// 使用指定序列化器将FPMatrix2x2实例序列化到字节流
		/// </summary>
		/// <param name="ptr">FPMatrix2x2实例的指针</param>
		/// <param name="serializer">序列化器用于写入数据</param>
		public unsafe static void Serialize(void* ptr, IDeterministicFrameSerializer serializer)
		{
			FP.Serialize(&((FPMatrix2x2*)ptr)->M00, serializer);
			FP.Serialize(&((FPMatrix2x2*)ptr)->M10, serializer);
			FP.Serialize(&((FPMatrix2x2*)ptr)->M01, serializer);
			FP.Serialize(&((FPMatrix2x2*)ptr)->M11, serializer);
		}

		/// <summary>
		/// 返回当前 FPMatrix2x2 对象的字符串表示
		/// </summary>
		/// <returns>
		/// 字符串格式为 (({0} {1}) ({2} {3})) 其中 {0} 到 {3} 分别表示 M00 M01 M10 M11 所有值均使用 InvariantCulture 格式化
		/// </returns>
		public override readonly string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "(({0}, {1}), ({2}, {3}))", M00.AsFloat, M01.AsFloat, M10.AsFloat, M11.AsFloat);
		}

		/// <summary>
		/// 计算FPMatrix2x2对象的哈希码
		/// </summary>
		/// <returns>当前实例的哈希码</returns>
		public override readonly int GetHashCode()
		{
			int num = 17;
			num = num * 31 + M00.GetHashCode();
			num = num * 31 + M10.GetHashCode();
			num = num * 31 + M01.GetHashCode();
			return num * 31 + M11.GetHashCode();
		}

		/// <summary>
		/// 将两个矩阵相加
		/// </summary>
		public static FPMatrix2x2 operator +(FPMatrix2x2 a, FPMatrix2x2 b)
		{
			a.M00.RawValue = a.M00.RawValue + b.M00.RawValue;
			a.M01.RawValue = a.M01.RawValue + b.M01.RawValue;
			a.M10.RawValue = a.M10.RawValue + b.M10.RawValue;
			a.M11.RawValue = a.M11.RawValue + b.M11.RawValue;
			return a;
		}

		/// <summary>
		/// 减去两个矩阵
		/// </summary>
		public static FPMatrix2x2 operator -(FPMatrix2x2 a, FPMatrix2x2 b)
		{
			a.M00.RawValue = a.M00.RawValue - b.M00.RawValue;
			a.M01.RawValue = a.M01.RawValue - b.M01.RawValue;
			a.M10.RawValue = a.M10.RawValue - b.M10.RawValue;
			a.M11.RawValue = a.M11.RawValue - b.M11.RawValue;
			return a;
		}

		/// <summary>
		/// 乘以两个矩阵
		/// </summary>
		public static FPMatrix2x2 operator *(FPMatrix2x2 a, FPMatrix2x2 b)
		{
			FPMatrix2x2 result = default(FPMatrix2x2);
			result.M00.RawValue = (a.M00.RawValue * b.M00.RawValue + 32768 >> 16) + (a.M01.RawValue * b.M10.RawValue + 32768 >> 16);
			result.M01.RawValue = (a.M00.RawValue * b.M01.RawValue + 32768 >> 16) + (a.M01.RawValue * b.M11.RawValue + 32768 >> 16);
			result.M10.RawValue = (a.M10.RawValue * b.M00.RawValue + 32768 >> 16) + (a.M11.RawValue * b.M10.RawValue + 32768 >> 16);
			result.M11.RawValue = (a.M10.RawValue * b.M01.RawValue + 32768 >> 16) + (a.M11.RawValue * b.M11.RawValue + 32768 >> 16);
			return result;
		}

		/// <summary>
		/// 将矩阵与向量相乘
		/// </summary>
		public static FPVector2 operator *(FPMatrix2x2 m, FPVector2 vector)
		{
			FPVector2 result = default(FPVector2);
			result.X.RawValue = (m.M00.RawValue * vector.X.RawValue + 32768 >> 16) + (m.M01.RawValue * vector.Y.RawValue + 32768 >> 16);
			result.Y.RawValue = (m.M10.RawValue * vector.X.RawValue + 32768 >> 16) + (m.M11.RawValue * vector.Y.RawValue + 32768 >> 16);
			return result;
		}

		/// <summary>
		/// 将矩阵与标量相乘
		/// </summary>
		public static FPMatrix2x2 operator *(FP a, FPMatrix2x2 m)
		{
			m.M00.RawValue = a.RawValue * m.M00.RawValue + 32768 >> 16;
			m.M01.RawValue = a.RawValue * m.M01.RawValue + 32768 >> 16;
			m.M10.RawValue = a.RawValue * m.M10.RawValue + 32768 >> 16;
			m.M11.RawValue = a.RawValue * m.M11.RawValue + 32768 >> 16;
			return m;
		}
	}
}

