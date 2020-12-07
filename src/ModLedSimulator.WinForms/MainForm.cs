using ModLedSimulator.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModLedSimulator.WinForms
{
    public partial class MainForm : Form
    {
        private const int panelsX = 6;
        private const int panelsY = 4;
        private const int panelSizeX = 16;
        private const int panelSizeY = 16;
        private IList<PanelInfo> _panels = new List<PanelInfo>();
        private byte[,][] _frames;

        public MainForm()
        {
            InitializeComponent();
            DoubleBuffered = true;
            this.Init().ContinueWith((x) => { this.Paint += Form1_Paint; });
        }

        public async Task Init()
        {
            var portStart = 7890;

            _frames = new byte[panelsX, panelsY][];

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
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Factory.StartNew(() => Worker(), TaskCreationOptions.LongRunning);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        public async Task Worker()
        {
            foreach (var panel in _panels)
            {
                panel.NextFrameTask = panel.Server.GetNextFrame(CancellationToken.None);
            }

            while (true)
            {
                // this relies on the implementation detail that WhenAny always returns the first completed task in the list so we don't output the same panel all the time
                var tasks = _panels
                    .OrderBy(x => x.LastFrameTime)
                    .Select(x => x.NextFrameTask);
                var task = await Task.WhenAny(tasks);
                var panel = _panels.First(x => x.NextFrameTask == task);
                panel.LastFrameTime = DateTime.UtcNow;
                var result = task.Result;

                _frames[panel.X, panel.Y] = result.ToArray();
                this.Invalidate();

                panel.NextFrameTask = panel.Server.GetNextFrame(CancellationToken.None);
            }
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            const int scale = 8;

            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

            for (int panelY = 0; panelY < panelsY; panelY++)
            {
                for (int panelX = 0; panelX < panelsX; panelX++)
                {
                    using var image = new Bitmap(panelSizeX, panelSizeY, PixelFormat.Format24bppRgb);
                    var data = image.LockBits(new Rectangle(0, 0, panelSizeX, panelSizeY), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

                    var bytes = _frames[panelX, panelY];
                    if (bytes != null)
                    {
                        for (int y = 0; y < panelSizeY; y++)
                        {
                            for (int x = 0; x < panelSizeX; x++)
                            {
                                var srcIdx = (panelSizeX * y + x) * 3;
                                var dstIdx = data.Stride * y + x * 3;

                                Marshal.WriteByte(data.Scan0 + dstIdx, bytes[srcIdx + 2]);
                                Marshal.WriteByte(data.Scan0 + dstIdx + 1, bytes[srcIdx + 1]);
                                Marshal.WriteByte(data.Scan0 + dstIdx + 2, bytes[srcIdx]);
                            }
                        }
                    }
                    image.UnlockBits(data);

                    e.Graphics.DrawImage(
                        image,
                        new Rectangle(panelX * panelSizeX * scale, panelY * panelSizeY * scale, panelSizeX * scale, panelSizeY * scale),
                        new Rectangle(0, 0, panelSizeX, panelSizeY), GraphicsUnit.Pixel);
                }
            }
        }
    }
}
