using System;
using System.Collections.Generic;

namespace MmECS
{
    /// <summary>
    /// 持有世界独享的单例数据和可恢复随机状态
    /// </summary>
    public sealed partial class World
    {
        /// <summary> 按类型持有世界单例 </summary>
        private readonly Dictionary<Type, IWorldResource> resourceDict = new Dictionary<Type, IWorldResource>();

        /// <summary> 当前世界的随机序列状态 </summary>
        private DeterministicRandom random;

        /// <summary>
        /// 在初始化阶段注册一份世界独享的单例值
        /// </summary>
        public void RegisterSingleton<T>(T Value) where T : unmanaged
        {
            EnsureInitializing();
            resourceDict.Add(typeof(T), new WorldResource<T>(Value));
        }

        /// <summary>
        /// 判断当前世界是否已注册此单例类型
        /// </summary>
        public bool HasSingleton<T>() where T : unmanaged => resourceDict.ContainsKey(typeof(T));

        /// <summary>
        /// 取得世界单例引用且仅允许在当前系统执行范围使用
        /// </summary>
        public ref T GetSingleton<T>() where T : unmanaged
        {
            return ref ((WorldResource<T>)resourceDict[typeof(T)]).value;
        }

        /// <summary>
        /// 推进世界随机状态并返回无符号随机整数
        /// </summary>
        public uint NextRandomUInt() => random.NextUInt();

        /// <summary>
        /// 返回零到指定正上限之间且不含上限的均匀随机整数
        /// </summary>
        public int NextRandomInt(int exclusiveMax) => random.NextInt(exclusiveMax);

        /// <summary>
        /// 返回零到一之间且不含一的随机浮点数
        /// </summary>
        public float NextRandomFloat() => random.NextFloat();
    }

    /// <summary>
    /// 为异构世界单例提供深复制恢复和调试读取契约
    /// </summary>
    internal interface IWorldResource
    {
        /// <summary>
        /// 复制当前单例容器
        /// </summary>
        public IWorldResource Clone();

        /// <summary>
        /// 从同类型副本恢复当前值
        /// </summary>
        public void Restore(IWorldResource Source);

        /// <summary>
        /// 为低频调试读取装箱副本
        /// </summary>
        public object ReadBoxed();
    }

    /// <summary>
    /// 以独立容器保存单个 unmanaged 世界状态
    /// </summary>
    internal sealed class WorldResource<T> : IWorldResource where T : unmanaged
    {
        /// <summary> 当前世界单例值 </summary>
        internal T value;

        /// <summary>
        /// 保存初始化单例值
        /// </summary>
        internal WorldResource(T Value) => value = Value;

        /// <summary>
        /// 返回具有独立值的单例容器
        /// </summary>
        public IWorldResource Clone() => new WorldResource<T>(value);

        /// <summary>
        /// 复制同类型单例快照的值
        /// </summary>
        public void Restore(IWorldResource Source) => value = ((WorldResource<T>)Source).value;

        /// <summary>
        /// 返回只用于调试的装箱值副本
        /// </summary>
        public object ReadBoxed() => value;
    }
}
