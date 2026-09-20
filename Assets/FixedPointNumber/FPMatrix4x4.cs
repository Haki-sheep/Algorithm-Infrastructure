using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Photon.Deterministic
{
	/// <summary>
	/// 表示 4x4 列主序矩阵
	/// 每个元素均可通过 M行列 字段或索引器单独访问
	/// 索引器支持按行列或单一索引访问
	/// </summary>
	/// \ingroup MathAPI
	[Serializable]
	[StructLayout(LayoutKind.Explicit)]
	public struct FPMatrix4x4
	{
		/// <summary>
		/// 结构体在Frame数据缓冲区或栈中的内存大小作为值参数传递时适用
		/// 这与结构体占用的快照载荷无关快照载荷会按位打包并压缩
		/// </summary>
		public const int SIZE = 128;

		/// <summary>第一行 第一列</summary>
		[FieldOffset(0)]
		public FP M00;

		/// <summary>第二行 第一列</summary>
		[FieldOffset(8)]
		public FP M10;

		/// <summary>第三行 第一列</summary>
		[FieldOffset(16)]
		public FP M20;

		/// <summary>第四行 第一列</summary>
		[FieldOffset(24)]
		public FP M30;

		/// <summary>第一行 第二列</summary>
		[FieldOffset(32)]
		public FP M01;

		/// <summary>第二行 第二列</summary>
		[FieldOffset(40)]
		public FP M11;

		/// <summary>第三行 第二列</summary>
		[FieldOffset(48)]
		public FP M21;

		/// <summary>第四行 第二列</summary>
		[FieldOffset(56)]
		public FP M31;

		/// <summary>第一行 第三列</summary>
		[FieldOffset(64)]
		public FP M02;

		/// <summary>第二行 第三列</summary>
		[FieldOffset(72)]
		public FP M12;

		/// <summary>第三行 第三列</summary>
		[FieldOffset(80)]
		public FP M22;

		/// <summary>第四行 第三列</summary>
		[FieldOffset(88)]
		public FP M32;

		/// <summary>第一行 第四列</summary>
		[FieldOffset(96)]
		public FP M03;

		/// <summary>第二行 第四列</summary>
		[FieldOffset(104)]
		public FP M13;

		/// <summary>第三行 第四列</summary>
		[FieldOffset(112)]
		public FP M23;

		/// <summary>第四行 第四列</summary>
		[FieldOffset(120)]
		public FP M33;

		/// <summary>
		/// 所有元素均为 0 的矩阵
		/// </summary>
		public static FPMatrix4x4 Zero => default(FPMatrix4x4);

		/// <summary>
		/// 主对角线元素为 1 其余元素为 0 的矩阵
		/// </summary>
		public static FPMatrix4x4 Identity => new FPMatrix4x4
		{
			M00 = 
			{
				RawValue = 65536L
			},
			M11 = 
			{
				RawValue = 65536L
			},
			M22 = 
			{
				RawValue = 65536L
			},
			M33 = 
			{
				RawValue = 65536L
			}
		};

		/// <summary>
		/// 获取或设置指定行列对应的元素
		/// </summary>
		/// <param name="row"></param>
		/// <param name="column"></param>
		/// <returns></returns>
		public FP this[int row, int column]
		{
			readonly get
			{
				return this[row + column * 4];
			}
			set
			{
				this[row + column * 4] = value;
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
					2 => M20, 
					3 => M30, 
					4 => M01, 
					5 => M11, 
					6 => M21, 
					7 => M31, 
					8 => M02, 
					9 => M12, 
					10 => M22, 
					11 => M32, 
					12 => M03, 
					13 => M13, 
					14 => M23, 
					15 => M33, 
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
					M20 = value;
					break;
				case 3:
					M30 = value;
					break;
				case 4:
					M01 = value;
					break;
				case 5:
					M11 = value;
					break;
				case 6:
					M21 = value;
					break;
				case 7:
					M31 = value;
					break;
				case 8:
					M02 = value;
					break;
				case 9:
					M12 = value;
					break;
				case 10:
					M22 = value;
					break;
				case 11:
					M32 = value;
					break;
				case 12:
					M03 = value;
					break;
				case 13:
					M13 = value;
					break;
				case 14:
					M23 = value;
					break;
				case 15:
					M33 = value;
					break;
				default:
					throw new ArgumentOutOfRangeException();
				}
			}
		}

		/// <summary>
		/// 创建转置矩阵
		/// </summary>
		public readonly FPMatrix4x4 Transposed => FromColumns(M00, M01, M02, M03, M10, M11, M12, M13, M20, M21, M22, M23, M30, M31, M32, M33);

		/// <summary>
		/// 如果此矩阵等于 <see cref="P:Photon.Deterministic.FPMatrix4x4.Identity" /> 则返回 <see langword="true" />
		/// </summary>
		public readonly bool IsIdentity
		{
			get
			{
				if (M00 == 1 && M11 == 1 && M22 == 1 && M33 == 1)
				{
					return (M01.RawValue | M02.RawValue | M03.RawValue | M10.RawValue | M12.RawValue | M13.RawValue | M20.RawValue | M21.RawValue | M23.RawValue | M30.RawValue | M31.RawValue | M32.RawValue) == 0;
				}
				return false;
			}
		}

		/// <summary>
		/// 尝试从矩阵中获取缩放值
		/// </summary>
		public readonly FPVector3 LossyScale
		{
			get
			{
				long x = (M00.RawValue * M00.RawValue + 32768 >> 16) + (M10.RawValue * M10.RawValue + 32768 >> 16) + (M20.RawValue * M20.RawValue + 32768 >> 16);
				long x2 = (M01.RawValue * M01.RawValue + 32768 >> 16) + (M11.RawValue * M11.RawValue + 32768 >> 16) + (M21.RawValue * M21.RawValue + 32768 >> 16);
				long x3 = (M02.RawValue * M02.RawValue + 32768 >> 16) + (M12.RawValue * M12.RawValue + 32768 >> 16) + (M22.RawValue * M22.RawValue + 32768 >> 16);
				return new FPVector3(FP.FromRaw(FPMath.SqrtRaw(x) * FPMath.SignInt(Determinant3x3)), FP.FromRaw(FPMath.SqrtRaw(x2)), FP.FromRaw(FPMath.SqrtRaw(x3)));
			}
		}

		/// <summary>
		/// 创建逆矩阵 行列式为 0 时矩阵不可逆并返回 <see cref="P:Photon.Deterministic.FPMatrix4x4.Zero" />
		/// </summary>
		public readonly FPMatrix4x4 Inverted
		{
			get
			{
				long num = (M00.RawValue * M11.RawValue + 32768 >> 16) - (M10.RawValue * M01.RawValue + 32768 >> 16);
				long num2 = (M00.RawValue * M12.RawValue + 32768 >> 16) - (M10.RawValue * M02.RawValue + 32768 >> 16);
				long num3 = (M00.RawValue * M13.RawValue + 32768 >> 16) - (M10.RawValue * M03.RawValue + 32768 >> 16);
				long num4 = (M01.RawValue * M12.RawValue + 32768 >> 16) - (M11.RawValue * M02.RawValue + 32768 >> 16);
				long num5 = (M01.RawValue * M13.RawValue + 32768 >> 16) - (M11.RawValue * M03.RawValue + 32768 >> 16);
				long num6 = (M02.RawValue * M13.RawValue + 32768 >> 16) - (M12.RawValue * M03.RawValue + 32768 >> 16);
				long num7 = (M22.RawValue * M33.RawValue + 32768 >> 16) - (M32.RawValue * M23.RawValue + 32768 >> 16);
				long num8 = (M21.RawValue * M33.RawValue + 32768 >> 16) - (M31.RawValue * M23.RawValue + 32768 >> 16);
				long num9 = (M21.RawValue * M32.RawValue + 32768 >> 16) - (M31.RawValue * M22.RawValue + 32768 >> 16);
				long num10 = (M20.RawValue * M33.RawValue + 32768 >> 16) - (M30.RawValue * M23.RawValue + 32768 >> 16);
				long num11 = (M20.RawValue * M32.RawValue + 32768 >> 16) - (M30.RawValue * M22.RawValue + 32768 >> 16);
				long num12 = (M20.RawValue * M31.RawValue + 32768 >> 16) - (M30.RawValue * M21.RawValue + 32768 >> 16);
				long num13 = (num * num7 + 32768 >> 16) - (num2 * num8 + 32768 >> 16) + (num3 * num9 + 32768 >> 16) + (num4 * num10 + 32768 >> 16) - (num5 * num11 + 32768 >> 16) + (num6 * num12 + 32768 >> 16);
				if (num13 == 0L)
				{
					return Zero;
				}
				long num14 = 4294967296L / num13;
				num = num * num14 + 32768 >> 16;
				num2 = num2 * num14 + 32768 >> 16;
				num3 = num3 * num14 + 32768 >> 16;
				num4 = num4 * num14 + 32768 >> 16;
				num5 = num5 * num14 + 32768 >> 16;
				num6 = num6 * num14 + 32768 >> 16;
				num7 = num7 * num14 + 32768 >> 16;
				num8 = num8 * num14 + 32768 >> 16;
				num9 = num9 * num14 + 32768 >> 16;
				num10 = num10 * num14 + 32768 >> 16;
				num11 = num11 * num14 + 32768 >> 16;
				num12 = num12 * num14 + 32768 >> 16;
				FPMatrix4x4 result = default(FPMatrix4x4);
				result.M00.RawValue = (M11.RawValue * num7 + 32768 >> 16) - (M12.RawValue * num8 + 32768 >> 16) + (M13.RawValue * num9 + 32768 >> 16);
				result.M01.RawValue = -(M01.RawValue * num7 + 32768 >> 16) + (M02.RawValue * num8 + 32768 >> 16) - (M03.RawValue * num9 + 32768 >> 16);
				result.M02.RawValue = (M31.RawValue * num6 + 32768 >> 16) - (M32.RawValue * num5 + 32768 >> 16) + (M33.RawValue * num4 + 32768 >> 16);
				result.M03.RawValue = -(M21.RawValue * num6 + 32768 >> 16) + (M22.RawValue * num5 + 32768 >> 16) - (M23.RawValue * num4 + 32768 >> 16);
				result.M10.RawValue = -(M10.RawValue * num7 + 32768 >> 16) + (M12.RawValue * num10 + 32768 >> 16) - (M13.RawValue * num11 + 32768 >> 16);
				result.M11.RawValue = (M00.RawValue * num7 + 32768 >> 16) - (M02.RawValue * num10 + 32768 >> 16) + (M03.RawValue * num11 + 32768 >> 16);
				result.M12.RawValue = -(M30.RawValue * num6 + 32768 >> 16) + (M32.RawValue * num3 + 32768 >> 16) - (M33.RawValue * num2 + 32768 >> 16);
				result.M13.RawValue = (M20.RawValue * num6 + 32768 >> 16) - (M22.RawValue * num3 + 32768 >> 16) + (M23.RawValue * num2 + 32768 >> 16);
				result.M20.RawValue = (M10.RawValue * num8 + 32768 >> 16) - (M11.RawValue * num10 + 32768 >> 16) + (M13.RawValue * num12 + 32768 >> 16);
				result.M21.RawValue = -(M00.RawValue * num8 + 32768 >> 16) + (M01.RawValue * num10 + 32768 >> 16) - (M03.RawValue * num12 + 32768 >> 16);
				result.M22.RawValue = (M30.RawValue * num5 + 32768 >> 16) - (M31.RawValue * num3 + 32768 >> 16) + (M33.RawValue * num + 32768 >> 16);
				result.M23.RawValue = -(M20.RawValue * num5 + 32768 >> 16) + (M21.RawValue * num3 + 32768 >> 16) - (M23.RawValue * num + 32768 >> 16);
				result.M30.RawValue = -(M10.RawValue * num9 + 32768 >> 16) + (M11.RawValue * num11 + 32768 >> 16) - (M12.RawValue * num12 + 32768 >> 16);
				result.M31.RawValue = (M00.RawValue * num9 + 32768 >> 16) - (M01.RawValue * num11 + 32768 >> 16) + (M02.RawValue * num12 + 32768 >> 16);
				result.M32.RawValue = -(M30.RawValue * num4 + 32768 >> 16) + (M31.RawValue * num2 + 32768 >> 16) - (M32.RawValue * num + 32768 >> 16);
				result.M33.RawValue = (M20.RawValue * num4 + 32768 >> 16) - (M21.RawValue * num2 + 32768 >> 16) + (M22.RawValue * num + 32768 >> 16);
				return result;
			}
		}

		/// <summary>
		/// 计算此矩阵的行列式
		/// </summary>
		public readonly FP Determinant
		{
			get
			{
				long num = (M00.RawValue * M11.RawValue + 32768 >> 16) - (M10.RawValue * M01.RawValue + 32768 >> 16);
				long num2 = (M00.RawValue * M12.RawValue + 32768 >> 16) - (M10.RawValue * M02.RawValue + 32768 >> 16);
				long num3 = (M00.RawValue * M13.RawValue + 32768 >> 16) - (M10.RawValue * M03.RawValue + 32768 >> 16);
				long num4 = (M01.RawValue * M12.RawValue + 32768 >> 16) - (M11.RawValue * M02.RawValue + 32768 >> 16);
				long num5 = (M01.RawValue * M13.RawValue + 32768 >> 16) - (M11.RawValue * M03.RawValue + 32768 >> 16);
				long num6 = (M02.RawValue * M13.RawValue + 32768 >> 16) - (M12.RawValue * M03.RawValue + 32768 >> 16);
				long num7 = (M22.RawValue * M33.RawValue + 32768 >> 16) - (M32.RawValue * M23.RawValue + 32768 >> 16);
				long num8 = (M21.RawValue * M33.RawValue + 32768 >> 16) - (M31.RawValue * M23.RawValue + 32768 >> 16);
				long num9 = (M21.RawValue * M32.RawValue + 32768 >> 16) - (M31.RawValue * M22.RawValue + 32768 >> 16);
				long num10 = (M20.RawValue * M33.RawValue + 32768 >> 16) - (M30.RawValue * M23.RawValue + 32768 >> 16);
				long num11 = (M20.RawValue * M32.RawValue + 32768 >> 16) - (M30.RawValue * M22.RawValue + 32768 >> 16);
				long num12 = (M20.RawValue * M31.RawValue + 32768 >> 16) - (M30.RawValue * M21.RawValue + 32768 >> 16);
				return FP.FromRaw((num * num7 + 32768 >> 16) - (num2 * num8 + 32768 >> 16) + (num3 * num9 + 32768 >> 16) + (num4 * num10 + 32768 >> 16) - (num5 * num11 + 32768 >> 16) + (num6 * num12 + 32768 >> 16));
			}
		}

		/// <summary>
		/// 仅考虑此矩阵的旋转和缩放部分计算行列式
		/// </summary>
		public readonly FP Determinant3x3 => FP.FromRaw(((M00.RawValue * M11.RawValue + 32768 >> 16) * M22.RawValue + 32768 >> 16) + ((M10.RawValue * M21.RawValue + 32768 >> 16) * M02.RawValue + 32768 >> 16) + ((M20.RawValue * M01.RawValue + 32768 >> 16) * M12.RawValue + 32768 >> 16) - (((M02.RawValue * M11.RawValue + 32768 >> 16) * M20.RawValue + 32768 >> 16) + ((M12.RawValue * M21.RawValue + 32768 >> 16) * M00.RawValue + 32768 >> 16) + ((M22.RawValue * M01.RawValue + 32768 >> 16) * M10.RawValue + 32768 >> 16)));

		/// <summary>
		/// 尝试从此矩阵获取旋转四元数
		/// </summary>
		public readonly FPQuaternion Rotation
		{
			get
			{
				long num = M00.RawValue + M11.RawValue + M22.RawValue;
				FP w = default(FP);
				FP x = default(FP);
				FP y = default(FP);
				FP z = default(FP);
				if (num > 0)
				{
					long num2 = FPMath.SqrtRaw(num + 65536);
					w.RawValue = num2 >> 1;
					num2 = 2147483648u / num2;
					x.RawValue = (M21.RawValue - M12.RawValue) * num2 + 32768 >> 16;
					y.RawValue = (M02.RawValue - M20.RawValue) * num2 + 32768 >> 16;
					z.RawValue = (M10.RawValue - M01.RawValue) * num2 + 32768 >> 16;
				}
				else if ((M00 > M11) & (M00 > M22))
				{
					long num3 = FPMath.SqrtRaw(65536 + (M00.RawValue - M11.RawValue - M22.RawValue));
					x.RawValue = num3 >> 1;
					num3 = 2147483648u / num3;
					w.RawValue = (M21.RawValue - M12.RawValue) * num3 + 32768 >> 16;
					y.RawValue = (M01.RawValue + M10.RawValue) * num3 + 32768 >> 16;
					z.RawValue = (M02.RawValue + M20.RawValue) * num3 + 32768 >> 16;
				}
				else if (M11 > M22)
				{
					long num4 = FPMath.SqrtRaw(65536 + (M11.RawValue - M00.RawValue - M22.RawValue));
					y.RawValue = num4 >> 1;
					num4 = 2147483648u / num4;
					w.RawValue = (M02.RawValue - M20.RawValue) * num4 + 32768 >> 16;
					x.RawValue = (M01.RawValue + M10.RawValue) * num4 + 32768 >> 16;
					z.RawValue = (M12.RawValue + M21.RawValue) * num4 + 32768 >> 16;
				}
				else
				{
					long num5 = FPMath.SqrtRaw(65536 + (M22.RawValue - M00.RawValue - M11.RawValue));
					z.RawValue = num5 >> 1;
					num5 = 2147483648u / num5;
					w.RawValue = (M10.RawValue - M01.RawValue) * num5 + 32768 >> 16;
					x.RawValue = (M02.RawValue + M20.RawValue) * num5 + 32768 >> 16;
					y.RawValue = (M12.RawValue + M21.RawValue) * num5 + 32768 >> 16;
				}
				return new FPQuaternion(x, y, z, w);
			}
		}

		/// <summary>
		/// 根据行创建矩阵 每四个连续参数设置一行
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPMatrix4x4 FromRows(FP m00, FP m01, FP m02, FP m03, FP m10, FP m11, FP m12, FP m13, FP m20, FP m21, FP m22, FP m23, FP m30, FP m31, FP m32, FP m33)
		{
			FPMatrix4x4 result = default(FPMatrix4x4);
			result.M00 = m00;
			result.M10 = m10;
			result.M20 = m20;
			result.M30 = m30;
			result.M01 = m01;
			result.M11 = m11;
			result.M21 = m21;
			result.M31 = m31;
			result.M02 = m02;
			result.M12 = m12;
			result.M22 = m22;
			result.M32 = m32;
			result.M03 = m03;
			result.M13 = m13;
			result.M23 = m23;
			result.M33 = m33;
			return result;
		}

		/// <summary>
		/// 根据列创建前四个值设置第一列后四个值设置第二列依此类推
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FPMatrix4x4 FromColumns(FP m00, FP m10, FP m20, FP m30, FP m01, FP m11, FP m21, FP m31, FP m02, FP m12, FP m22, FP m32, FP m03, FP m13, FP m23, FP m33)
		{
			FPMatrix4x4 result = default(FPMatrix4x4);
			result.M00 = m00;
			result.M10 = m10;
			result.M20 = m20;
			result.M30 = m30;
			result.M01 = m01;
			result.M11 = m11;
			result.M21 = m21;
			result.M31 = m31;
			result.M02 = m02;
			result.M12 = m12;
			result.M22 = m22;
			result.M32 = m32;
			result.M03 = m03;
			result.M13 = m13;
			result.M23 = m23;
			result.M33 = m33;
			return result;
		}

		/// <summary>
		/// 创建观察矩阵的逆矩阵 即观察空间到世界空间的变换 等价于 Unity Matrix4x4 LookAt
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <param name="up"></param>
		/// <returns></returns>
		public static FPMatrix4x4 InverseLookAt(FPVector3 from, FPVector3 to, FPVector3 up)
		{
			FPVector3 forward = default(FPVector3);
			forward.X.RawValue = to.X.RawValue - from.X.RawValue;
			forward.Y.RawValue = to.Y.RawValue - from.Y.RawValue;
			forward.Z.RawValue = to.Z.RawValue - from.Z.RawValue;
			FPMatrix4x4 result = Rotate(FPQuaternion.LookRotation(forward, up));
			result.M03.RawValue = from.X.RawValue;
			result.M13.RawValue = from.Y.RawValue;
			result.M23.RawValue = from.Z.RawValue;
			return result;
		}

		/// <summary>
		/// 创建世界空间到观察空间的观察矩阵 Unity Matrix4x4 LookAt 创建相反方向的变换 如需相同行为请使用 <see cref="M:Photon.Deterministic.FPMatrix4x4.InverseLookAt(Photon.Deterministic.FPVector3,Photon.Deterministic.FPVector3,Photon.Deterministic.FPVector3)" />
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <param name="up"></param>
		/// <returns></returns>
		public static FPMatrix4x4 LookAt(FPVector3 from, FPVector3 to, FPVector3 up)
		{
			FPVector3 forward = default(FPVector3);
			forward.X.RawValue = to.X.RawValue - from.X.RawValue;
			forward.Y.RawValue = to.Y.RawValue - from.Y.RawValue;
			forward.Z.RawValue = to.Z.RawValue - from.Z.RawValue;
			FPMatrix4x4 result = Rotate(FPQuaternion.LookRotation(forward, up).Conjugated);
			result.M03.RawValue = -(from.X.RawValue * result.M00.RawValue + 32768 >> 16) - (from.Y.RawValue * result.M01.RawValue + 32768 >> 16) - (from.Z.RawValue * result.M02.RawValue + 32768 >> 16);
			result.M13.RawValue = -(from.X.RawValue * result.M10.RawValue + 32768 >> 16) - (from.Y.RawValue * result.M11.RawValue + 32768 >> 16) - (from.Z.RawValue * result.M12.RawValue + 32768 >> 16);
			result.M23.RawValue = -(from.X.RawValue * result.M20.RawValue + 32768 >> 16) - (from.Y.RawValue * result.M21.RawValue + 32768 >> 16) - (from.Z.RawValue * result.M22.RawValue + 32768 >> 16);
			return result;
		}

		/// <summary>
		/// 创建缩放矩阵
		/// </summary>
		/// <param name="scale"></param>
		/// <returns></returns>
		public static FPMatrix4x4 Scale(FPVector3 scale)
		{
			return FromColumns(scale.X, 0, 0, 0, 0, scale.Y, 0, 0, 0, 0, scale.Z, 0, 0, 0, 0, 1);
		}

		/// <summary>
		/// 创建平移矩阵
		/// </summary>
		/// <param name="translation"></param>
		/// <returns></returns>
		public static FPMatrix4x4 Translate(FPVector3 translation)
		{
			return FromRows(1, 0, 0, translation.X, 0, 1, 0, translation.Y, 0, 0, 1, translation.Z, 0, 0, 0, 1);
		}

		/// <summary>
		/// 使用给定IDeterministicFrameSerializer序列化FPMatrix4x4对象
		/// </summary>
		/// <param name="ptr">待序列化FPMatrix4x4对象的指针</param>
		/// <param name="serializer">用于序列化的IDeterministicFrameSerializer</param>
		public unsafe static void Serialize(void* ptr, IDeterministicFrameSerializer serializer)
		{
			FP.Serialize(&((FPMatrix4x4*)ptr)->M00, serializer);
			FP.Serialize(&((FPMatrix4x4*)ptr)->M10, serializer);
			FP.Serialize(&((FPMatrix4x4*)ptr)->M20, serializer);
			FP.Serialize(&((FPMatrix4x4*)ptr)->M30, serializer);
			FP.Serialize(&((FPMatrix4x4*)ptr)->M01, serializer);
			FP.Serialize(&((FPMatrix4x4*)ptr)->M11, serializer);
			FP.Serialize(&((FPMatrix4x4*)ptr)->M21, serializer);
			FP.Serialize(&((FPMatrix4x4*)ptr)->M31, serializer);
			FP.Serialize(&((FPMatrix4x4*)ptr)->M02, serializer);
			FP.Serialize(&((FPMatrix4x4*)ptr)->M12, serializer);
			FP.Serialize(&((FPMatrix4x4*)ptr)->M22, serializer);
			FP.Serialize(&((FPMatrix4x4*)ptr)->M32, serializer);
			FP.Serialize(&((FPMatrix4x4*)ptr)->M03, serializer);
			FP.Serialize(&((FPMatrix4x4*)ptr)->M13, serializer);
			FP.Serialize(&((FPMatrix4x4*)ptr)->M23, serializer);
			FP.Serialize(&((FPMatrix4x4*)ptr)->M33, serializer);
		}

		/// <summary>
		/// 返回表示以下对象的字符串FPMatrix4x4对象
		/// 由矩阵元素组成的格式化字符串
		/// 按 4x4 矩阵形式排列
		/// </summary>
		/// <returns>FPMatrix4x4对象的字符串表示</returns>
		public override readonly string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "(({0}, {1}, {2}, {3}), ({4}, {5}, {6}, {7}), ({8}, {9}, {10}, {11}), ({12}, {13}, {14}, {15}))", M00.AsFloat, M01.AsFloat, M02.AsFloat, M03.AsFloat, M10.AsFloat, M11.AsFloat, M12.AsFloat, M13.AsFloat, M20.AsFloat, M21.AsFloat, M22.AsFloat, M23.AsFloat, M30.AsFloat, M31.AsFloat, M32.AsFloat, M33.AsFloat);
		}

		/// <summary>
		/// 计算当前实例的哈希码
		/// </summary>
		/// <returns>
		/// 哈希码用于当前实例
		/// </returns>
		public override readonly int GetHashCode()
		{
			int num = 17;
			num = num * 31 + M00.GetHashCode();
			num = num * 31 + M10.GetHashCode();
			num = num * 31 + M20.GetHashCode();
			num = num * 31 + M30.GetHashCode();
			num = num * 31 + M01.GetHashCode();
			num = num * 31 + M11.GetHashCode();
			num = num * 31 + M21.GetHashCode();
			num = num * 31 + M31.GetHashCode();
			num = num * 31 + M02.GetHashCode();
			num = num * 31 + M12.GetHashCode();
			num = num * 31 + M22.GetHashCode();
			num = num * 31 + M32.GetHashCode();
			num = num * 31 + M03.GetHashCode();
			num = num * 31 + M13.GetHashCode();
			num = num * 31 + M23.GetHashCode();
			return num * 31 + M33.GetHashCode();
		}

		/// <summary>
		/// 乘以两个矩阵
		/// </summary>
		public static FPMatrix4x4 operator *(FPMatrix4x4 a, FPMatrix4x4 b)
		{
			FPMatrix4x4 result = default(FPMatrix4x4);
			result.M00.RawValue = (a.M00.RawValue * b.M00.RawValue + 32768 >> 16) + (a.M01.RawValue * b.M10.RawValue + 32768 >> 16) + (a.M02.RawValue * b.M20.RawValue + 32768 >> 16) + (a.M03.RawValue * b.M30.RawValue + 32768 >> 16);
			result.M01.RawValue = (a.M00.RawValue * b.M01.RawValue + 32768 >> 16) + (a.M01.RawValue * b.M11.RawValue + 32768 >> 16) + (a.M02.RawValue * b.M21.RawValue + 32768 >> 16) + (a.M03.RawValue * b.M31.RawValue + 32768 >> 16);
			result.M02.RawValue = (a.M00.RawValue * b.M02.RawValue + 32768 >> 16) + (a.M01.RawValue * b.M12.RawValue + 32768 >> 16) + (a.M02.RawValue * b.M22.RawValue + 32768 >> 16) + (a.M03.RawValue * b.M32.RawValue + 32768 >> 16);
			result.M03.RawValue = (a.M00.RawValue * b.M03.RawValue + 32768 >> 16) + (a.M01.RawValue * b.M13.RawValue + 32768 >> 16) + (a.M02.RawValue * b.M23.RawValue + 32768 >> 16) + (a.M03.RawValue * b.M33.RawValue + 32768 >> 16);
			result.M10.RawValue = (a.M10.RawValue * b.M00.RawValue + 32768 >> 16) + (a.M11.RawValue * b.M10.RawValue + 32768 >> 16) + (a.M12.RawValue * b.M20.RawValue + 32768 >> 16) + (a.M13.RawValue * b.M30.RawValue + 32768 >> 16);
			result.M11.RawValue = (a.M10.RawValue * b.M01.RawValue + 32768 >> 16) + (a.M11.RawValue * b.M11.RawValue + 32768 >> 16) + (a.M12.RawValue * b.M21.RawValue + 32768 >> 16) + (a.M13.RawValue * b.M31.RawValue + 32768 >> 16);
			result.M12.RawValue = (a.M10.RawValue * b.M02.RawValue + 32768 >> 16) + (a.M11.RawValue * b.M12.RawValue + 32768 >> 16) + (a.M12.RawValue * b.M22.RawValue + 32768 >> 16) + (a.M13.RawValue * b.M32.RawValue + 32768 >> 16);
			result.M13.RawValue = (a.M10.RawValue * b.M03.RawValue + 32768 >> 16) + (a.M11.RawValue * b.M13.RawValue + 32768 >> 16) + (a.M12.RawValue * b.M23.RawValue + 32768 >> 16) + (a.M13.RawValue * b.M33.RawValue + 32768 >> 16);
			result.M20.RawValue = (a.M20.RawValue * b.M00.RawValue + 32768 >> 16) + (a.M21.RawValue * b.M10.RawValue + 32768 >> 16) + (a.M22.RawValue * b.M20.RawValue + 32768 >> 16) + (a.M23.RawValue * b.M30.RawValue + 32768 >> 16);
			result.M21.RawValue = (a.M20.RawValue * b.M01.RawValue + 32768 >> 16) + (a.M21.RawValue * b.M11.RawValue + 32768 >> 16) + (a.M22.RawValue * b.M21.RawValue + 32768 >> 16) + (a.M23.RawValue * b.M31.RawValue + 32768 >> 16);
			result.M22.RawValue = (a.M20.RawValue * b.M02.RawValue + 32768 >> 16) + (a.M21.RawValue * b.M12.RawValue + 32768 >> 16) + (a.M22.RawValue * b.M22.RawValue + 32768 >> 16) + (a.M23.RawValue * b.M32.RawValue + 32768 >> 16);
			result.M23.RawValue = (a.M20.RawValue * b.M03.RawValue + 32768 >> 16) + (a.M21.RawValue * b.M13.RawValue + 32768 >> 16) + (a.M22.RawValue * b.M23.RawValue + 32768 >> 16) + (a.M23.RawValue * b.M33.RawValue + 32768 >> 16);
			result.M30.RawValue = (a.M30.RawValue * b.M00.RawValue + 32768 >> 16) + (a.M31.RawValue * b.M10.RawValue + 32768 >> 16) + (a.M32.RawValue * b.M20.RawValue + 32768 >> 16) + (a.M33.RawValue * b.M30.RawValue + 32768 >> 16);
			result.M31.RawValue = (a.M30.RawValue * b.M01.RawValue + 32768 >> 16) + (a.M31.RawValue * b.M11.RawValue + 32768 >> 16) + (a.M32.RawValue * b.M21.RawValue + 32768 >> 16) + (a.M33.RawValue * b.M31.RawValue + 32768 >> 16);
			result.M32.RawValue = (a.M30.RawValue * b.M02.RawValue + 32768 >> 16) + (a.M31.RawValue * b.M12.RawValue + 32768 >> 16) + (a.M32.RawValue * b.M22.RawValue + 32768 >> 16) + (a.M33.RawValue * b.M32.RawValue + 32768 >> 16);
			result.M33.RawValue = (a.M30.RawValue * b.M03.RawValue + 32768 >> 16) + (a.M31.RawValue * b.M13.RawValue + 32768 >> 16) + (a.M32.RawValue * b.M23.RawValue + 32768 >> 16) + (a.M33.RawValue * b.M33.RawValue + 32768 >> 16);
			return result;
		}

		/// <summary>
		/// 使用此矩阵变换位置支持常规三维变换和投影变换
		/// </summary>
		public readonly FPVector3 MultiplyPoint(FPVector3 point)
		{
			long num = (M30.RawValue * point.X.RawValue + 32768 >> 16) + (M31.RawValue * point.Y.RawValue + 32768 >> 16) + (M32.RawValue * point.Z.RawValue + 32768 >> 16) + M33.RawValue;
			long num2 = 4294967296L / num;
			FPVector3 result = MultiplyPoint3x4(point);
			result.X.RawValue = result.X.RawValue * num2 + 32768 >> 16;
			result.Y.RawValue = result.Y.RawValue * num2 + 32768 >> 16;
			result.Z.RawValue = result.Z.RawValue * num2 + 32768 >> 16;
			return result;
		}

		/// <summary>
		/// 使用此矩阵变换位置速度快于<see cref="M:Photon.Deterministic.FPMatrix4x4.MultiplyPoint(Photon.Deterministic.FPVector3)" />但仅支持常规三维变换
		/// </summary>
		public readonly FPVector3 MultiplyPoint3x4(FPVector3 point)
		{
			FPVector3 result = default(FPVector3);
			result.X.RawValue = (M00.RawValue * point.X.RawValue + 32768 >> 16) + (M01.RawValue * point.Y.RawValue + 32768 >> 16) + (M02.RawValue * point.Z.RawValue + 32768 >> 16) + M03.RawValue;
			result.Y.RawValue = (M10.RawValue * point.X.RawValue + 32768 >> 16) + (M11.RawValue * point.Y.RawValue + 32768 >> 16) + (M12.RawValue * point.Z.RawValue + 32768 >> 16) + M13.RawValue;
			result.Z.RawValue = (M20.RawValue * point.X.RawValue + 32768 >> 16) + (M21.RawValue * point.Y.RawValue + 32768 >> 16) + (M22.RawValue * point.Z.RawValue + 32768 >> 16) + M23.RawValue;
			return result;
		}

		/// <summary>
		/// 使用此矩阵变换方向向量 仅旋转和缩放部分参与计算
		/// </summary>
		public readonly FPVector3 MultiplyVector(FPVector3 vector)
		{
			FPVector3 result = default(FPVector3);
			result.X.RawValue = (M00.RawValue * vector.X.RawValue + 32768 >> 16) + (M01.RawValue * vector.Y.RawValue + 32768 >> 16) + (M02.RawValue * vector.Z.RawValue + 32768 >> 16);
			result.Y.RawValue = (M10.RawValue * vector.X.RawValue + 32768 >> 16) + (M11.RawValue * vector.Y.RawValue + 32768 >> 16) + (M12.RawValue * vector.Z.RawValue + 32768 >> 16);
			result.Z.RawValue = (M20.RawValue * vector.X.RawValue + 32768 >> 16) + (M21.RawValue * vector.Y.RawValue + 32768 >> 16) + (M22.RawValue * vector.Z.RawValue + 32768 >> 16);
			return result;
		}

		/// <summary>
		/// 创建平移旋转和缩放矩阵
		/// 可用于创建局部空间到世界空间的变换
		/// 旋转是应为归一化状态
		/// </summary>
		public static FPMatrix4x4 TRS(FPVector3 pos, FPQuaternion q, FPVector3 s)
		{
			FPMatrix4x4 result = Rotate(q);
			result.M00.RawValue = result.M00.RawValue * s.X.RawValue + 32768 >> 16;
			result.M10.RawValue = result.M10.RawValue * s.X.RawValue + 32768 >> 16;
			result.M20.RawValue = result.M20.RawValue * s.X.RawValue + 32768 >> 16;
			result.M01.RawValue = result.M01.RawValue * s.Y.RawValue + 32768 >> 16;
			result.M11.RawValue = result.M11.RawValue * s.Y.RawValue + 32768 >> 16;
			result.M21.RawValue = result.M21.RawValue * s.Y.RawValue + 32768 >> 16;
			result.M02.RawValue = result.M02.RawValue * s.Z.RawValue + 32768 >> 16;
			result.M12.RawValue = result.M12.RawValue * s.Z.RawValue + 32768 >> 16;
			result.M22.RawValue = result.M22.RawValue * s.Z.RawValue + 32768 >> 16;
			result.M03.RawValue = pos.X.RawValue;
			result.M13.RawValue = pos.Y.RawValue;
			result.M23.RawValue = pos.Z.RawValue;
			return result;
		}

		/// <summary>
		/// 创建平移旋转缩放矩阵的逆矩阵 此方法明显更快
		/// 比对 TRS 矩阵直接求逆更快
		/// 可用于创建世界空间到局部空间的变换
		/// 旋转是应为归一化状态
		/// </summary>
		public static FPMatrix4x4 InverseTRS(FPVector3 pos, FPQuaternion q, FPVector3 s)
		{
			FPMatrix4x4 result = Rotate(q.Conjugated);
			result.M00.RawValue = result.M00.RawValue * s.X.RawValue + 32768 >> 16;
			result.M01.RawValue = result.M01.RawValue * s.X.RawValue + 32768 >> 16;
			result.M02.RawValue = result.M02.RawValue * s.X.RawValue + 32768 >> 16;
			result.M10.RawValue = result.M10.RawValue * s.Y.RawValue + 32768 >> 16;
			result.M11.RawValue = result.M11.RawValue * s.Y.RawValue + 32768 >> 16;
			result.M12.RawValue = result.M12.RawValue * s.Y.RawValue + 32768 >> 16;
			result.M20.RawValue = result.M20.RawValue * s.Z.RawValue + 32768 >> 16;
			result.M21.RawValue = result.M21.RawValue * s.Z.RawValue + 32768 >> 16;
			result.M22.RawValue = result.M22.RawValue * s.Z.RawValue + 32768 >> 16;
			result.M03.RawValue = -(pos.X.RawValue * result.M00.RawValue + 32768 >> 16) - (pos.Y.RawValue * result.M01.RawValue + 32768 >> 16) - (pos.Z.RawValue * result.M02.RawValue + 32768 >> 16);
			result.M13.RawValue = -(pos.X.RawValue * result.M10.RawValue + 32768 >> 16) - (pos.Y.RawValue * result.M11.RawValue + 32768 >> 16) - (pos.Z.RawValue * result.M12.RawValue + 32768 >> 16);
			result.M23.RawValue = -(pos.X.RawValue * result.M20.RawValue + 32768 >> 16) - (pos.Y.RawValue * result.M21.RawValue + 32768 >> 16) - (pos.Z.RawValue * result.M22.RawValue + 32768 >> 16);
			return result;
		}

		/// <summary>
		/// 创建旋转矩阵旋转是应为归一化状态
		/// </summary>
		public static FPMatrix4x4 Rotate(FPQuaternion q)
		{
			long num = q.X.RawValue * 2;
			long num2 = q.Y.RawValue * 2;
			long num3 = q.Z.RawValue * 2;
			long num4 = q.X.RawValue * num + 32768 >> 16;
			long num5 = q.Y.RawValue * num2 + 32768 >> 16;
			long num6 = q.Z.RawValue * num3 + 32768 >> 16;
			long num7 = q.X.RawValue * num2 + 32768 >> 16;
			long num8 = q.X.RawValue * num3 + 32768 >> 16;
			long num9 = q.Y.RawValue * num3 + 32768 >> 16;
			long num10 = q.W.RawValue * num + 32768 >> 16;
			long num11 = q.W.RawValue * num2 + 32768 >> 16;
			long num12 = q.W.RawValue * num3 + 32768 >> 16;
			FPMatrix4x4 result = default(FPMatrix4x4);
			result.M00.RawValue = 65536 - (num5 + num6);
			result.M10.RawValue = num7 + num12;
			result.M20.RawValue = num8 - num11;
			result.M30.RawValue = 0L;
			result.M01.RawValue = num7 - num12;
			result.M11.RawValue = 65536 - (num4 + num6);
			result.M21.RawValue = num9 + num10;
			result.M31.RawValue = 0L;
			result.M02.RawValue = num8 + num11;
			result.M12.RawValue = num9 - num10;
			result.M22.RawValue = 65536 - (num4 + num5);
			result.M32.RawValue = 0L;
			result.M03.RawValue = 0L;
			result.M13.RawValue = 0L;
			result.M23.RawValue = 0L;
			result.M33.RawValue = 65536L;
			return result;
		}
	}
}

