using System;
using System.Collections.Generic;
using MmECS;

namespace Game.Simulation
{
    public partial class Simulation
    {
        /// <summary>
        /// 按目标 Tick 保存移动输入
        /// </summary>
        private readonly Dictionary<long, List<MoveInput>> inputDict =
            new Dictionary<long, List<MoveInput>>();

        /// <summary>
        /// 最近已冻结的目标 Tick
        /// </summary>
        private long frozenTick;

        /// <summary>
        /// 接收未来 Tick 的移动输入
        /// </summary>
        public void SubmitInput(MoveInput Input)
        {
            if (Input.TargetTick <= world.Tick || Input.TargetTick <= frozenTick)
                throw new InvalidOperationException("Input target Tick is closed");

            if (!inputDict.TryGetValue(Input.TargetTick, out var InputList))
            {
                InputList = new List<MoveInput>();
                inputDict.Add(Input.TargetTick, InputList);
            }

            InputList.Add(Input);
        }

        /// <summary>
        /// 冻结并消费本 Tick 的移动输入
        /// </summary>
        private void ProcessInput()
        {
            // 世界Tick + 1是本Tick要生效的Tick
            long targetTick = world.Tick + 1;
            // 此Tick进入后 冻结此Tick 不接受任何其他输入
            frozenTick = targetTick;

            if (!inputDict.TryGetValue(targetTick, out var InputList))
                return;

            inputDict.Remove(targetTick);

            // 遍历输入指令改动值
            for (int index = 0; index < InputList.Count; index++)
            {
                var Input = InputList[index];
                if (!world.Has<Velocity>(Input.Target))
                    continue;

                ref Velocity VelocityData = ref world.Get<Velocity>(Input.Target);
                VelocityData.X = Input.DirectionX;
            }
        }
    }
}