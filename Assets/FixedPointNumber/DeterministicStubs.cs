namespace Photon.Deterministic
{
	/// <summary> LUT 字节加载委托 </summary>
	public delegate byte[] LutProvider(string path);

	/// <summary> LUT 表生成工具 </summary>
	public static class LutGenerator
	{
		/// <summary> 生成 LUT 表到目录 </summary>
		public static void Generate(string directoryPath)
		{
		}
	}

	/// <summary> 帧序列化流接口 </summary>
	public interface IDeterministicSerializeStream
	{
		/// <summary> 序列化 long 原始值 </summary>
		unsafe void Serialize(long* value);

		/// <summary> 写入 int </summary>
		void WriteInt(int value);

		/// <summary> 读取 int </summary>
		int ReadInt();
	}

	/// <summary> 确定性帧序列化器接口 </summary>
	public interface IDeterministicFrameSerializer
	{
		/// <summary> 是否写入模式 </summary>
		bool Writing { get; }

		/// <summary> 序列化流 </summary>
		IDeterministicSerializeStream Stream { get; }
	}
}
