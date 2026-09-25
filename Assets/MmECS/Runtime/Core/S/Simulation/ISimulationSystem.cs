using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MmECS;

namespace Game.Simulation
{
    public interface ISimulationSystem
    {
        void Tick(World World, float stepSeconds);
    }
}