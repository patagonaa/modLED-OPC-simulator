using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ModLedSimulator
{
    class OpcServer
    {
        private const int MaxUDPSize = 0x10000;
        private Socket _socket;
        private readonly int _port;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        private SemaphoreSlim _frameSemaphore = new SemaphoreSlim(0, 1);
        private ArraySegment<byte> _currentFrame = null;

        public OpcServer(int port = 7890)
        {
            _port = port;
        }

        public Task Start()
        {
            _socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
            {
                DualMode = true
            };
            _socket.Bind(new IPEndPoint(IPAddress.IPv6Any, _port));
            Task.Factory.StartNew(() => Worker(), TaskCreationOptions.LongRunning);
            return Task.CompletedTask;
        }

        public Task Stop()
        {
            _cts.Cancel();
            _socket.Disconnect(false);
            return Task.CompletedTask;
        }

        public async Task<ArraySegment<byte>> GetNextFrame(CancellationToken token)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, _cts.Token);
            await _frameSemaphore.WaitAsync(cts.Token);
            return _currentFrame;
        }

        private async Task Worker()
        {
            var buffer = new byte[MaxUDPSize];
            while (!_cts.Token.IsCancellationRequested)
            {
                int numBytes;
                try
                {
                    numBytes = await _socket.ReceiveAsync(buffer, SocketFlags.None, _cts.Token);
                }
                catch (Exception)
                {

                    throw;
                }
                HandleOpc(new ArraySegment<byte>(buffer, 0, numBytes));
            }
        }

        private void HandleOpc(ArraySegment<byte> buffer)
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
                    {
                        AddFrame(data);
                    }
                    break;
                default:
                    Console.Error.WriteLine($"Unsupported command {command}");
                    break;
            }
        }

        private void AddFrame(ArraySegment<byte> frame)
        {
            _currentFrame = frame;
            if(_frameSemaphore.CurrentCount == 0)
            {
                _frameSemaphore.Release();
            }
        }
    }
}
