namespace MmECS
{
    public readonly struct Entity
    {
        // 实体索引
        public int Index { get; }

        // 代 就是说这个实体存在过几次 
        // 如果实体被销毁再重建 代数会+1
        public uint Generation { get; }
        public Entity(int index, uint generation)
        {
            Index = index;
            Generation = generation;
        }
    }
}
