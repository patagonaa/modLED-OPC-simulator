using System;
using System.Threading.Tasks;

namespace ModLedSimulator.Common
{
    public class PanelInfo
    {
        public int X { get; set; }
        public int Y { get; set; }
        public OpcServer Server { get; set; }
        public DateTime LastFrameTime { get; set; }
        public Task<ArraySegment<byte>> NextFrameTask { get; set; }
    }
}
