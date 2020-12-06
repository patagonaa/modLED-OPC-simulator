using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ModLedSimulator
{
    class Options
    {
        [Option('x', "panelsx", Required = true, HelpText = "Number of panels in X-direction")]
        public int PanelsX { get; set; }
        [Option('y', "panelsy", Required = true, HelpText = "Number of panels in Y-direction")]
        public int PanelsY { get; set; }
        [Option('p', "port", Default = 7890, Required = false, HelpText = "start port (top left = this port)")]
        public int PortStart { get; set; }
    }

    class Program
    {
        static IList<PanelInfo> _panels = new List<PanelInfo>();

        static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<Options>(args)
                .WithParsedAsync(x => Run(x));
        }

        private static async Task Run(Options options)
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) => cts.Cancel();

            int portStart = options.PortStart;
            int panelsX = options.PanelsX;
            int panelsY = options.PanelsY;
            int i = 0;
            for (int y = 0; y < panelsY; y++)
            {
                for (int x = 0; x < panelsX; x++)
                {
                    var panel = new PanelInfo
                    {
                        X = x,
                        Y = y,
                        Server = new OpcServer(portStart + i)
                    };
                    await panel.Server.Start();
                    _panels.Add(panel);
                    i++;
                }
            }

            foreach (var panel in _panels)
            {
                panel.NextFrameTask = panel.Server.GetNextFrame();
            }

            while (!cts.IsCancellationRequested)
            {
                var task = await Task.WhenAny(_panels.Select(x => x.NextFrameTask));
                var panel = _panels.First(x => x.NextFrameTask == task);
                var result = task.Result;

                OutputFrame(result, panel.X, panel.Y);

                panel.NextFrameTask = panel.Server.GetNextFrame();
            }

            foreach (var panel in _panels)
            {
                await panel.Server.Stop();
            }
        }

        private static void OutputFrame(ArraySegment<byte> data, int panelX, int panelY)
        {
            if (data.Count != 16 * 16 * 3)
            {
                Console.Error.WriteLine($"invalid frame length {data.Count}");
                return;
            }

            Console.Out.Write("\x1B[?25l");

            var sb = new StringBuilder(256);

            for (int y = 0; y < 16; y++)
            {
                Console.CursorTop = panelY * 16 + y;
                Console.CursorLeft = panelX * 16 * 2;
                sb.Clear();
                for (int x = 0; x < 16; x++)
                {
                    var pixelIndex = y * 16 + x;
                    var arrIndex = pixelIndex * 3;
                    var r = data[arrIndex];
                    var g = data[arrIndex + 1];
                    var b = data[arrIndex + 2];

                    sb.Append($"\x1B[38;2;{r};{g};{b}m##");
                }
                Console.Out.Write(sb.ToString());
            }
        }

        private class PanelInfo
        {
            public int X { get; set; }
            public int Y { get; set; }
            public OpcServer Server { get; set; }
            public Task<ArraySegment<byte>> NextFrameTask { get; set; }
        }
    }
}
