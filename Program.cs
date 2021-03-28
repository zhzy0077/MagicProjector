using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MagicProjector
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var control = new Control("192.168.0.108", 3988);
            await control.Connect();

            await control.Send(new Packet
            {
                Type = (int) Type.Module,
                Body = Array.Empty<byte>(),
            });
            await Task.Delay(1000);
            
            await control.Send(new Packet
            {
                Type = (int) Type.ChangeMode,
                Body = Format((int) Mode.Default),
            });
            await Task.Delay(1000);

            // Power Key Down
            await control.Send(new Packet
            {
                Type = (int) Type.Mouse,
                Body = Format(1, (int) Key.Power, 0, 0,  (int) KeyOp.Down),
            });
            await Task.Delay(100);
            
            // Power Key Up.
            await control.Send(new Packet
            {
                Type = (int) Type.Mouse,
                Body = Format(1, (int) Key.Power, 0, 0,  (int) KeyOp.Up),
            });
            await Task.Delay(1000);
        }

        public static byte[] Format(params int[] keys)
        {
            return Encoding.ASCII.GetBytes("[" + string.Join(',', keys) + "]");
        }
    }

    class Control : IDisposable
    {
        public const int HeaderLength = 20;
        public const int MagicNumber = 287475865;
        public int HelloId { get; set; }
        public int Reserve { get; set; }

        private TcpClient Client { get; set; }
        private NetworkStream Stream { get; set; }

        public Control(string ip, int port)
        {
            Client = new TcpClient(ip, port);
            Stream = Client.GetStream();
            Reserve = new Random().Next(1073741823);
            HelloId = 0;
        }

        public async Task Connect(CancellationToken cancellationToken = default)
        {
            await Send(new Packet
            {
                Type = (int) Type.Hello,
                Body = Array.Empty<byte>(),
            }, cancellationToken);
            var helloPacket = await Receive(cancellationToken);
            Debug.Assert(helloPacket.Type == (int) Type.HelloResponse);

            var hello = JsonSerializer.Deserialize<HelloResponse>(helloPacket.Body);
            Debug.Assert(hello != null, nameof(hello) + " != null");
            Console.WriteLine(hello);
            HelloId = hello.HelloId;
            
            
        }

        public async Task Send(Packet packet, CancellationToken cancellationToken = default)
        {
            var header = Header(packet);
            var body = packet.Body;

            var payload = header.Concat(body).ToArray();

            await Stream.WriteAsync(payload.AsMemory(), cancellationToken);
            await Stream.FlushAsync(cancellationToken);
        }

        public async Task<Packet> Receive(CancellationToken cancellationToken = default)
        {
            var header = new byte[HeaderLength];
            await Stream.ReadFullyAsync(header, cancellationToken);

            var magicNumber = BinaryPrimitives.ReadInt32BigEndian(header);
            var size = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4));
            var type = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(8));
            var body = new byte[size];
            await Stream.ReadFullyAsync(body, cancellationToken);

            var response = new Packet
            {
                Type = type,
                Body = body,
            };
            Console.WriteLine(response);
            return response;
        }

        private byte[] Header(Packet packet)
        {
            var buffer = new byte[HeaderLength];
            var span = new Span<byte>(buffer);
            BinaryPrimitives.WriteInt32BigEndian(span.Slice(0, 4), MagicNumber);
            BinaryPrimitives.WriteInt32BigEndian(span.Slice(4, 4), packet.Body.Length);
            BinaryPrimitives.WriteInt32BigEndian(span.Slice(8, 4), packet.Type);
            BinaryPrimitives.WriteInt32BigEndian(span.Slice(12, 4), Reserve);
            var checksum = (packet.Body.Length + Reserve) ^ HelloId;
            BinaryPrimitives.WriteInt32BigEndian(span.Slice(16, 4), checksum);
            return buffer;
        }


        public void Dispose()
        {
            Stream.Close();
            Client.Close();
        }
    }

    record Packet
    {
        public int Type { get; set; }
        public byte[] Body { get; set; }
    }

    record HelloResponse
    {
        [JsonPropertyName("ver")] public string Version { get; set; }

        [JsonPropertyName("sid")] public int HelloId { get; set; }
    }

    internal enum Type
    {
        Hello = 1,
        Module = 8,
        ChangeMode = 0x118,
        Mouse = 0x107,
        HelloResponse = 0x10000001,
    }

    enum Mode
    {
        Default = 32,
    }

    enum Key
    {
        Power = 116,
    }

    enum KeyOp
    {
        Down = 0,
        Up = 1,
    }
}