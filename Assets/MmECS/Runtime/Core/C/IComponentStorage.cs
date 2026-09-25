using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MmECS
{
    public interface IComponentStorage
    {
        int Count { get; }
        int EntityIndexAt(int row);
        bool Has(int entityIndex);
        void Remove(int entityIndex);
    }
}
