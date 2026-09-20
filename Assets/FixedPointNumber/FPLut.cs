using System;
using System.IO;

namespace Photon.Deterministic
{
	/// <summary>
	/// 反余弦函数的查找表
	/// 表存储特定角度对应的反余弦函数预计算值
	/// 这些值采用精度为16位的定点格式
	/// </summary>
	public static class FPLut
	{
		/// <summary>
		/// 定点数小数部分占用的位数
		/// </summary>
		public const int PRECISION = 16;

		/// <summary>
		/// 定点数格式的圆周率 PI
		/// </summary>
		public const long PI = 205887L;

		/// <summary>
		/// 定点数格式的二倍圆周率 PI
		/// </summary>
		public const long PITIMES2 = 411775L;

		/// <summary>
		/// 定点数格式的二分之 PI
		/// </summary>
		public const long PIOVER2 = 102944L;

		/// <summary>
		/// 定点数格式的数值 1
		/// </summary>
		public const long ONE = 65536L;

		internal const int SQRT_RESOLUTION_SPACE = 3;

		internal const int SQRT_LUT_SIZE_BASE_2 = 16;

		internal const int SQRT_VALUE_STEP = 3;

		internal const int SQRT_LUT_SIZE_BASE_10 = 65536;

		internal const int SqrtAdditionalPrecisionBits = 6;

		internal const int Log2LutSizeExponent = 6;

		internal const int Log2AdditionalPrecisionBits = 15;

		/// <summary>
		/// 安全执行 FPHighPrecisionDivisor 的 Log2 除法时 结果额外精度需要移动的位数
		/// 这里应存在计算方式但当前暂时无法确定
		/// 此处选择 6 以保证安全 因为 Log2 的最大值为 48
		/// </summary>
		internal const int Log2APShiftForHPDivision = 6;

		internal const int ExpNegativeLutPrecision = 42;

		internal const int ExpNegativeLutCount = 30;

		internal const int ExpNonNegativeLutCount = 33;

		internal const int ExpOverflowingThreshold = 20;

		/// <summary>
		/// 近似平方根值的查找表
		/// </summary>
		public static int[] sqrt_aprox_lut;

		/// <summary>
		/// 反正弦函数的查找表
		/// </summary>
		public static long[] asin_lut;

		/// <summary>
		/// 用于近似计算定点数反余弦acos函数的查找表
		/// </summary>
		public static long[] acos_lut;

		/// <summary>
		/// FPMath中Atan函数的查找表
		/// </summary>
		public static long[] atan_lut;

		/// <summary>
		/// 正弦和余弦函数的查找表
		/// </summary>
		public static long[] sin_cos_lut;

		/// <summary>
		/// 正切函数的查找表
		/// </summary>
		public static long[] tan_lut;

		/// <summary>
		/// log2函数的查找表
		/// </summary>
		public static uint[] log2_approx_lut;

		/// <summary>
		/// exp函数的查找表
		/// </summary>
		public static long[] exp_integral_lut;

		/// <summary>
		/// 查找表已加载时返回 <see langword="true" />
		/// </summary>
		public static bool IsLoaded
		{
			get
			{
				if (sin_cos_lut != null && sin_cos_lut.Length != 0 && tan_lut != null && tan_lut.Length != 0 && asin_lut != null && asin_lut.Length != 0 && acos_lut != null && acos_lut.Length != 0 && atan_lut != null && atan_lut.Length != 0 && sqrt_aprox_lut != null)
				{
					return sqrt_aprox_lut.Length != 0;
				}
				return false;
			}
		}

		/// <summary>
		/// 从 <paramref name="directoryPath" /> 目录初始化 LUT 目录必须包含以下文件
		/// * <c>FPSin.bytes</c>
		/// * <c>FPCos.bytes</c>
		/// * <c>FPTan.bytes</c>
		/// * <c>FPAsin.bytes</c>
		/// * <c>FPAcos.bytes</c>
		/// * <c>FPAtan.bytes</c>
		/// * <c>FPSqrt.bytes</c>
		/// </summary>
		/// <param name="directoryPath"></param>
		public static void Init(string directoryPath)
		{
			Load(directoryPath, "FPSinCos", ref sin_cos_lut);
			Load(directoryPath, "FPTan", ref tan_lut);
			Load(directoryPath, "FPAsin", ref asin_lut);
			Load(directoryPath, "FPAcos", ref acos_lut);
			Load(directoryPath, "FPAtan", ref atan_lut);
			Load(directoryPath, "FPSqrt", ref sqrt_aprox_lut);
			InitSmallLut();
		}

		/// <summary>
		/// 使用 <paramref name="lutProvider" /> 初始化 LUT 提供程序必须能够加载以下路径
		/// * FPSin
		/// * FPCos
		/// * FPTan
		/// * FPAsin
		/// * FPAcos
		/// * FPAtan
		/// * FPSqrt
		/// </summary>
		/// <param name="lutProvider"></param>
		public static void Init(LutProvider lutProvider)
		{
			Load(lutProvider, "FPSinCos", ref sin_cos_lut);
			Load(lutProvider, "FPTan", ref tan_lut);
			Load(lutProvider, "FPAsin", ref asin_lut);
			Load(lutProvider, "FPAcos", ref acos_lut);
			Load(lutProvider, "FPAtan", ref atan_lut);
			Load(lutProvider, "FPSqrt", ref sqrt_aprox_lut);
			InitSmallLut();
		}

		/// <summary>
		/// 使用字节数组初始化LUT
		/// </summary>
		public static void Init(byte[] sinCos, byte[] tan, byte[] asin, byte[] acos, byte[] atan, byte[] sqrt)
		{
			Load(sinCos ?? throw new ArgumentNullException("sinCos"), ref sin_cos_lut);
			Load(tan ?? throw new ArgumentNullException("tan"), ref tan_lut);
			Load(asin ?? throw new ArgumentNullException("asin"), ref asin_lut);
			Load(acos ?? throw new ArgumentNullException("acos"), ref acos_lut);
			Load(atan ?? throw new ArgumentNullException("atan"), ref atan_lut);
			Load(sqrt ?? throw new ArgumentNullException("sqrt"), ref sqrt_aprox_lut);
			InitSmallLut();
		}

		private static void InitSmallLut()
		{
			log2_approx_lut = new uint[66]
			{
				0u, 48034513u, 95335645u, 141925456u, 187825021u, 233054496u, 277633165u, 321579490u, 364911162u, 407645136u,
				449797678u, 491384396u, 532420281u, 572919734u, 612896598u, 652364189u, 691335320u, 729822324u, 767837083u, 805391046u,
				842495250u, 879160341u, 915396590u, 951213914u, 986621888u, 1021629764u, 1056246482u, 1090480686u, 1124340739u, 1157834731u,
				1190970490u, 1223755601u, 1256197405u, 1288303019u, 1320079339u, 1351533050u, 1382670639u, 1413498396u, 1444022426u, 1474248656u,
				1504182841u, 1533830570u, 1563197273u, 1592288229u, 1621108567u, 1649663276u, 1677957208u, 1705995083u, 1733781493u, 1761320910u,
				1788617686u, 1815676059u, 1842500157u, 1869094003u, 1895461516u, 1921606515u, 1947532725u, 1973243777u, 1998743213u, 2024034488u,
				2049120974u, 2074005959u, 2098692655u, 2123184198u, 2147483648u, 2171593995u
			};
			exp_integral_lut = new long[63]
			{
				0L, 1L, 3L, 8L, 22L, 61L, 166L, 451L, 1226L, 3334L,
				9065L, 24641L, 66982L, 182076L, 494934L, 1345372L, 3657101L, 9941033L, 27022531L, 73454856L,
				199671002L, 542762058L, 1475380240L, 4010499297L, 10901667362L, 29633804291L, 80553031713L, 218965842333L, 595210870268L, 1617950892750L,
				65536L, 178145L, 484249L, 1316325L, 3578144L, 9726404L, 26439109L, 71868950L, 195360062L, 531043708L,
				1443526462L, 3923911751L, 10666298010L, 28994004058L, 78813874367L, 214238322522L, 582360139072L, 1583018983658L, 4303091737384L, 11697016075923L,
				31795786246376L, 1318815734L, 3584912846L, 9744803446L, 26489122130L, 72004899337L, 195729609429L, 532048240602L, 1446257064291L, 3931334297144L,
				10686474581524L, 29048849665247L, 78962960182681L
			};
		}

		/// <summary>
		/// 生成查找表在<paramref name="directoryPath" />
		/// </summary>
		/// <param name="directoryPath"></param>
		public static void GenerateTables(string directoryPath)
		{
			LutGenerator.Generate(directoryPath);
		}

		private unsafe static void Load<T>(LutProvider lutProvider, string path, ref T[] lut) where T : unmanaged
		{
			byte[] array = lutProvider(path);
			lut = new T[array.Length / sizeof(T)];
			Buffer.BlockCopy(array, 0, lut, 0, array.Length);
		}

		private unsafe static void Load<T>(string directoryPath, string filePath, ref T[] lut) where T : unmanaged
		{
			byte[] array = File.ReadAllBytes(Path.Combine(directoryPath, filePath) + ".bytes");
			lut = new T[array.Length / sizeof(T)];
			Buffer.BlockCopy(array, 0, lut, 0, array.Length);
		}

		private unsafe static void Load<T>(byte[] data, ref T[] lut) where T : unmanaged
		{
			lut = new T[data.Length / sizeof(T)];
			Buffer.BlockCopy(data, 0, lut, 0, data.Length);
		}
	}
}

