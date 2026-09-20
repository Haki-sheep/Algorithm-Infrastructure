using System;
using System.Runtime.InteropServices;

namespace Photon.Deterministic
{
	/// <summary>
	/// 表示二维轴对齐包围盒AABB
	/// </summary>
	/// \ingroup MathAPI
	[Serializable]
	[StructLayout(LayoutKind.Explicit)]
	public struct FPBounds2
	{
		/// <summary>
		/// 结构体在Frame数据缓冲区或栈中的内存大小作为值参数传递时适用
		/// 这与结构体占用的快照载荷无关快照载荷会按位打包并压缩
		/// </summary>
		public const int SIZE = 32;

		/// <summary>
		/// 边界盒的中心点
		/// </summary>
		[FieldOffset(0)]
		public FPVector2 Center;

		/// <summary>
		/// 边界盒的范围即尺寸的一半
		/// </summary>
		[FieldOffset(16)]
		public FPVector2 Extents;

		/// <summary>
		/// 获取或设置边界盒的最大点这是始终等于<see cref="F:Photon.Deterministic.FPBounds2.Center" />+<see cref="F:Photon.Deterministic.FPBounds2.Extents" />
		/// 设置此属性不会影响<see cref="P:Photon.Deterministic.FPBounds2.Min" />
		/// </summary>
		public FPVector2 Max
		{
			readonly get
			{
				return Center + Extents;
			}
			set
			{
				SetMinMax(Min, value);
			}
		}

		/// <summary>
		/// 获取或设置边界盒的最小点这是始终等于<see cref="F:Photon.Deterministic.FPBounds2.Center" />-<see cref="F:Photon.Deterministic.FPBounds2.Extents" />
		/// 设置此属性不会影响<see cref="P:Photon.Deterministic.FPBounds2.Max" />
		/// </summary>
		public FPVector2 Min
		{
			readonly get
			{
				return Center - Extents;
			}
			set
			{
				SetMinMax(value, Max);
			}
		}

		/// <summary>
		/// 使用给定中心点和范围创建新的边界盒
		/// </summary>
		/// <param name="center">中心点</param>
		/// <param name="extents">范围即尺寸的一半</param>
		public FPBounds2(FPVector2 center, FPVector2 extents)
		{
			Center = center;
			Extents = extents;
		}

		/// <summary>
		/// 在两个方向上将边界盒扩展0.5 *<paramref name="amount" />
		/// </summary>
		/// <param name="amount"></param>
		public void Expand(FP amount)
		{
			Extents += new FPVector2(amount * FP._0_50, amount * FP._0_50);
		}

		/// <summary>
		/// 在两个方向上将边界盒扩展0.5 *<paramref name="amount" />
		/// </summary>
		/// <param name="amount"></param>
		public void Expand(FPVector2 amount)
		{
			Extents += amount * FP._0_50;
		}

		/// <summary>
		/// 将边界设置为给定最小点和最大点
		/// </summary>
		/// <param name="min">最小位置</param>
		/// <param name="max">最大位置</param>
		public void SetMinMax(FPVector2 min, FPVector2 max)
		{
			Extents = (max - min) * FP._0_50;
			Center = min + Extents;
		}

		/// <summary>
		/// 扩展边界盒以包含<paramref name="point" />必要时执行
		/// </summary>
		/// <param name="point"></param>
		public void Encapsulate(FPVector2 point)
		{
			SetMinMax(FPVector2.Min(Min, point), FPVector2.Max(Max, point));
		}

		/// <summary>
		/// 必要时扩展边界盒以包含 <paramref name="bounds" />
		/// </summary>
		/// <param name="bounds"></param>
		public void Encapsulate(FPBounds2 bounds)
		{
			Encapsulate(bounds.Center - bounds.Extents);
			Encapsulate(bounds.Center + bounds.Extents);
		}

		/// <summary>
		/// 如果两个边界盒相交则返回<see langword="true" />
		/// </summary>
		/// <param name="bounds"></param>
		/// <returns></returns>
		public readonly bool Intersects(FPBounds2 bounds)
		{
			if (Min.X <= bounds.Max.X && Max.X >= bounds.Min.X && Min.Y <= bounds.Max.Y)
			{
				return Max.Y >= bounds.Min.Y;
			}
			return false;
		}

		/// <summary>
		/// 如果<paramref name="point" />位于边界盒内部则返回<see langword="true" />
		/// </summary>
		/// <param name="point"></param>
		/// <returns></returns>
		public readonly bool Contains(FPVector2 point)
		{
			FP fP = FPMath.Abs(point.X - Center.X);
			FP fP2 = FPMath.Abs(point.Y - Center.Y);
			if (fP <= Extents.X)
			{
				return fP2 <= Extents.Y;
			}
			return false;
		}

		/// <summary>
		/// 通过序列化FPVector2字段来序列化FPBounds2结构体
		/// </summary>
		/// <param name="ptr">待序列化FPBounds2结构体的指针</param>
		/// <param name="serializer">用于序列化分量的序列化器对象</param>
		public unsafe static void Serialize(void* ptr, IDeterministicFrameSerializer serializer)
		{
			FPVector2.Serialize(&((FPBounds2*)ptr)->Center, serializer);
			FPVector2.Serialize(&((FPBounds2*)ptr)->Extents, serializer);
		}

		/// <summary>
		/// 计算当前 FPBounds2 实例的哈希码
		/// </summary>
		/// <returns>FPBounds2对象的哈希码</returns>
		public override readonly int GetHashCode()
		{
			int num = 17;
			num = num * 31 + Center.GetHashCode();
			return num * 31 + Extents.GetHashCode();
		}
	}
}

