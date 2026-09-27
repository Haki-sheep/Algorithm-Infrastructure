using System;
using System.Collections.Generic;

namespace MmECS
{
    /// <summary>
    /// 固定系统注册边界并管理一次初始化和逆序释放及失败清理
    /// </summary>
    public partial class Simulation : IDisposable
    {
        /// <summary> 初始化一旦开始便固定系统列表 </summary>
        private bool initialized;

        /// <summary> 当前模拟是否已经释放 </summary>
        private bool disposed;

        protected bool IsInitialized => initialized;
        protected bool IsDisposed => disposed;

        /// <summary> 已进入初始化的系统数量 </summary>
        private int initializedSystemCount;

        /// <summary>
        /// 显式初始化系统且重复调用不重复执行生命周期
        /// </summary>
        public void Init()
        {
            EnsureIdle();
            executionInProgress = true;
            try { InitCore(); }
            finally { executionInProgress = false; }
        }

        /// <summary>
        /// 按注册顺序初始化并在失败时释放已经进入初始化的系统
        /// </summary>
        private void InitCore()
        {
            if (initialized)
                return;
            initialized = true;
            try
            {
                for (int index = 0; index < systemList.Count; index++)
                {
                    initializedSystemCount = index + 1;
                    if (systemList[index] is ISignalSystem Signals)
                        Signals.BindSignals(world.Signals);
                    if (systemList[index] is ISystemLifecycle Lifecycle)
                        Lifecycle.Init(world);
                }
                world.Signals.Seal();
            }
            catch (Exception Failure)
            {
                disposed = true;
                var FailureList = ReleaseSystems();
                FailureList.Insert(0, Failure);
                throw new AggregateException("Simulation initialization failed", FailureList);
            }
        }

        /// <summary>
        /// 释放系统与宿主业务状态且重复释放不再次触发生命周期
        /// </summary>
        public void Dispose()
        {
            if (executionInProgress)
                throw new InvalidOperationException("Simulation is executing");
            if (disposed)
                return;
            world.Events.EnsureIdle();
            disposed = true;
            executionInProgress = true;
            try
            {
                var FailureList = ReleaseSystems();
                if (FailureList.Count != 0)
                    throw new AggregateException("Simulation disposal failed", FailureList);
            }
            finally { executionInProgress = false; }
        }

        /// <summary>
        /// 逆序清理已初始化系统并收集错误以保证后续系统仍被释放
        /// </summary>
        private List<Exception> ReleaseSystems()
        {
            var FailureList = new List<Exception>();
            for (int index = initializedSystemCount - 1; index >= 0; index--)
            {
                try
                {
                    if (systemList[index] is ISystemLifecycle Lifecycle)
                        Lifecycle.Dispose(world);
                }
                catch (Exception Failure) { FailureList.Add(Failure); }
            }
            initializedSystemCount = 0;
            world.DisposeCommunication();
            systemList.Clear();
            try { DisposeState(); }
            catch (Exception Failure) { FailureList.Add(Failure); }
            Trace = null;
            return FailureList;
        }
    }
}
