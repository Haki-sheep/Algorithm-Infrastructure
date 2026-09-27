using System;

namespace MmECS
{
    /// <summary>
    /// 使用 SplitMix64 明确保存状态并产生可重复的随机序列
    /// </summary>
    public struct DeterministicRandom
    {
        /// <summary> 默认可重复种子 </summary>
        public const ulong DefaultSeed = 1;

        /// <summary> SplitMix64 的固定状态步进常量 </summary>
        private const ulong Increment = 0x9E3779B97F4A7C15UL;

        /// <summary> SplitMix64 的第一轮混合常量 </summary>
        private const ulong MixFirst = 0xBF58476D1CE4E5B9UL;

        /// <summary> SplitMix64 的第二轮混合常量 </summary>
        private const ulong MixSecond = 0x94D049BB133111EBUL;

        /// <summary> 下一次随机输出所依赖的完整内部状态 </summary>
        private ulong state;

        public ulong State => state;

        /// <summary>
        /// 使用明确种子开始随机序列且零种子同样有效
        /// </summary>
        public DeterministicRandom(ulong seed) => state = seed;

        /// <summary>
        /// 推进状态并返回混合结果的高三十二位
        /// </summary>
        public uint NextUInt()
        {
            // 固定宽度的溢出回绕是算法定义的一部分
            unchecked
            {
                state += Increment;
                ulong value = state;
                value = (value ^ (value >> 30)) * MixFirst;
                value = (value ^ (value >> 27)) * MixSecond;
                return (uint)((value ^ (value >> 31)) >> 32);
            }
        }

        /// <summary>
        /// 通过拒绝采样消除取模偏差并返回不含上限的随机整数
        /// </summary>
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            uint bound = (uint)exclusiveMax;
            uint threshold = unchecked(0u - bound) % bound;
            uint value;
            do { value = NextUInt(); } while (value < threshold);
            return (int)(value % bound);
        }

        /// <summary>
        /// 使用二十四位有效随机数生成不含一的浮点值
        /// </summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);
    }
}
