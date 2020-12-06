using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ModLedSimulator
{
    class Program
    {
        private const int MaxUDPSize = 0x10000;
        static async Task Main(string[] args)
        {
            using var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
            {
                DualMode = true
            };
            socket.Bind(new IPEndPoint(IPAddress.IPv6Any, 7890));

            //var test = new byte[16 * 16 * 3];
            //for (int i = 0; i < 256; i++)
            //{
            //    test[i*3] = (byte)i;
            //    test[i*3+1] = (byte)i;
            //    test[i*3+2] = (byte)i;
            //}
            //OutputFrame(test);

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) => cts.Cancel();

            var buffer = new byte[MaxUDPSize];
            while (!cts.Token.IsCancellationRequested)
            {
                var numBytes = await socket.ReceiveAsync(buffer, SocketFlags.None, cts.Token);
                await HandleOpc(new ArraySegment<byte>(buffer, 0, numBytes));
            }
        }

        private static async Task HandleOpc(ArraySegment<byte> buffer)
        {
            if (buffer.Count < 4)
                return;
            var channel = buffer[0];
            var command = buffer[1];
            var length = buffer[2] << 8 | buffer[3];

            if (buffer.Count < (4 + length))
            {
                Console.Error.WriteLine("not enough data");
                return;
            }

            if (buffer.Count > (4 + length))
            {
                Console.Error.WriteLine("more data after first message");
            }

            var data = buffer.Slice(4, length);

            switch (command)
            {
                case 0x00: // 8-bit color
                    if (channel == 0)
                        OutputFrame(data);
                    break;
                default:
                    Console.Error.WriteLine($"Unsupported command {command}");
                    break;
            }
        }

        private static void OutputFrame(ArraySegment<byte> data)
        {
            if(data.Count != 16 * 16 * 3)
            {
                Console.Error.WriteLine($"invalid frame length {data.Count}");
                return;
            }

            Console.CursorTop = 0;
            Console.CursorLeft = 0;

            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    var pixelIndex = y * 16 + x;
                    var arrIndex = pixelIndex * 3;
                    var r = data[arrIndex];
                    var g = data[arrIndex+1];
                    var b = data[arrIndex+2];

                    Console.Out.Write($"\x1B[38;2;{r};{g};{b}m##");
                }
                Console.Out.WriteLine();
            }
        }
    }
}
