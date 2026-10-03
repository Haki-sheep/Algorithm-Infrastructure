using System;

namespace MmECS.Samples
{
    /// <summary>
    /// 验证表现消费落后与输出确认及回调错误语义
    /// </summary>
    public static partial class CoreVerification
    {
        /// <summary>
        /// 暂停消费后逐批确认事件并验证恢复丢弃旧时间线输出
        /// </summary>
        private static void VerifyEventBacklog()
        {
            var World = CreateWorld();
            using var Runner = new Simulation(World, 0.5d);
            Runner.Register(new ActionSystem(CurrentWorld =>
            {
                var Message = new Position { Value = (int)CurrentWorld.Tick + 1 };
                CurrentWorld.Events.Emit(in Message);
                CurrentWorld.Events.Emit(in Message);
            }));
            Runner.Tick();
            Check(Runner.Events.TryPeek(out var FirstFrame), "首步事件未发布");
            var Snapshot = Runner.Capture();
            int TickCount = 64;
            for (int Index = 1; Index < TickCount; Index++) Runner.Tick();
            for (int Index = 1; Index <= TickCount; Index++)
            {
                Check(Runner.Events.TryPeek(out var Frame), "积压事件提前释放");
                var TypedFrame = (EventFrame<Position>)Frame;
                Check(Frame.Tick == Index && Frame.Sequence == Index && TypedFrame.Events.Count == 2,
                    "积压事件顺序或批次合并错误");
                Check(TypedFrame.Events[0].Value == Index && TypedFrame.Events[1].Value == Index,
                    "已发布事件载荷被后续 Tick 修改");
                Check(Runner.Events.Acknowledge(Frame), "当前队首确认失败");
                Check(!Runner.Events.Acknowledge(Frame), "同一批次可以重复确认");
            }
            Check(!Runner.Events.TryPeek(out _), "确认后仍有残留事件");
            Runner.Tick();
            Check(Runner.Events.TryPeek(out var OldFrame), "恢复前事件未发布");
            Runner.Restore(Snapshot);
            Check(!Runner.Events.TryPeek(out _), "恢复未清除旧时间线输出");
            Check(!Runner.Events.Acknowledge(FirstFrame) && !Runner.Events.Acknowledge(OldFrame),
                "旧时间线批次可以确认");
            Runner.Tick();
            Check(Runner.Events.TryPeek(out var NewFrame) && NewFrame.Tick == 2 &&
                NewFrame.Sequence == 1 && NewFrame.StateEpoch == World.StateEpoch,
                "恢复后事件身份或序号错误");
            Check(Runner.DispatchEvents() == 1 && !Runner.Events.TryPeek(out _),
                "无订阅者的派发未消费事件");
        }

        /// <summary>
        /// 回调抛错后当前批次不重播 后续批次可在取消订阅后继续消费
        /// </summary>
        private static void VerifyEventFailure()
        {
            var World = CreateWorld();
            using var Runner = new Simulation(World, 0.5d);
            Runner.Register(new ActionSystem(CurrentWorld =>
            {
                var First = new Position { Value = 1 };
                var Second = new Velocity { Value = 2 };
                CurrentWorld.Events.Emit(in First);
                CurrentWorld.Events.Emit(in Second);
            }));
            var Failure = new InvalidOperationException("测试表现回调失败");
            int FirstCalls = 0;
            int SecondCalls = 0;
            using var FirstSubscription = Runner.Events.Subscribe<Position>((Frame, Message) =>
            {
                FirstCalls++;
                throw Failure;
            });
            using var SecondSubscription = Runner.Events.Subscribe<Velocity>((Frame, Message) => SecondCalls++);
            Runner.Tick();
            Check(ReferenceEquals(Throws<InvalidOperationException>(() => Runner.DispatchEvents()), Failure),
                "表现原始异常丢失");
            Check(FirstCalls == 1 && SecondCalls == 0, "失败回调调用次数错误");
            Check(Runner.Events.TryPeek(out var Remaining) && Remaining is EventFrame<Velocity>,
                "回调失败未保留后续批次");
            FirstSubscription.Dispose();
            Check(Runner.DispatchEvents() == 1 && FirstCalls == 1 && SecondCalls == 1,
                "回调重试重播已领取批次或遗漏后续批次");
            Check(Runner.DispatchEvents() == 0, "消费完毕仍重复派发");
            Runner.Tick();
            Check(Runner.DispatchEvents() == 2 && FirstCalls == 1 && SecondCalls == 2,
                "取消订阅失效或表现错误阻断后续 Tick");
        }
    }
}
