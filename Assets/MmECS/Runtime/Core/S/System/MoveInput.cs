using MmECS;

namespace Game.Simulation
{
    public readonly struct MoveInput
    {
        /// <summary> 输入生效的逻辑 Tick </summary>
        public long TargetTick { get; }

        /// <summary> 接收输入的模拟实体 </summary>
        public Entity Target { get; }

        /// <summary> 横向控制值 当前直接写入 Velocity.X </summary>
        public float DirectionX { get; }
        
        /// <summary>
        /// 保存目标 Tick 实体和横向控制值
        /// </summary>
        public MoveInput(long targetTick, Entity Target, float directionX)
        {
            TargetTick = targetTick;
            this.Target = Target;
            DirectionX = directionX;
        }
    }
}
