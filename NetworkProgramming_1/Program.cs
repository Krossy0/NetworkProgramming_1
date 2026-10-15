
using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FileServer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            int port = 5000;

            Socket server = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );

            server.Bind(new IPEndPoint(IPAddress.Any, port));
            server.Listen(10);

            string downloadsPath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                "Downloads");

            Console.WriteLine("Server ise salindi...");
            Console.WriteLine("Port: " + port);
            Console.WriteLine("Fayllar Downloads qovluguna yazilacaq.");

            while (true)
            {
                using Socket client = await server.AcceptAsync();

                try
                {
                    byte[] header = new byte[12];
                    await ReceiveExactlyAsync(client, header);

                    int nameLength =
                        BinaryPrimitives.ReadInt32BigEndian(
                            header.AsSpan(0, 4));

                    long fileSize =
                        BinaryPrimitives.ReadInt64BigEndian(
                            header.AsSpan(4, 8));

                    if (nameLength <= 0 || nameLength > 255 ||
                        fileSize < 0)
                    {
                        throw new InvalidDataException(
                            "Fayl melumatlari duzgun deyil.");
                    }

                    byte[] nameBytes = new byte[nameLength];
                    await ReceiveExactlyAsync(client, nameBytes);

                    string fileName =
                        Path.GetFileName(
                            Encoding.UTF8.GetString(nameBytes));

                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        throw new InvalidDataException(
                            "Faylin adi duzgun deyil.");
                    }

                    string savePath = Path.Combine(downloadsPath,DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + fileName);

                    using FileStream fs = new FileStream(
                        savePath, FileMode.Create, FileAccess.Write);

                    byte[] buffer = new byte[8192];
                    long remaining = fileSize;

                    while (remaining > 0)
                    {
                        int count = (int)Math.Min(
                            buffer.Length, remaining);

                        int received = await client.ReceiveAsync(
                            buffer.AsMemory(0, count),
                            SocketFlags.None);

                        if (received == 0)
                        {
                            throw new EndOfStreamException(
                                "Fayl tam gonderilmedi.");
                        }

                        await fs.WriteAsync(
                            buffer.AsMemory(0, received));

                        remaining -= received;
                    }

                    fs.Close();

                    byte[] response = Encoding.UTF8.GetBytes("OK");
                    await SendAllAsync(client, response);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Fayl qebul edildi!");
                    Console.ResetColor();

                    Console.WriteLine("Adi: " + fileName);
                    Console.WriteLine(
                        "Olcusu: " + (fileSize / 1024.0).ToString("F1") + " KB");
                    Console.WriteLine("Vaxt: " + DateTime.Now.ToString("HH:mm:ss"));
                    Console.WriteLine("Yol: " + savePath);
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Xeta: " + ex.Message);
                }
            }
        }

        static async Task ReceiveExactlyAsync(
            Socket socket, byte[] data)
        {
            int totalReceived = 0;

            while (totalReceived < data.Length)
            {
                int received = await socket.ReceiveAsync(
                    data.AsMemory(totalReceived),
                    SocketFlags.None);

                if (received == 0)
                {
                    throw new EndOfStreamException(
                        "Baglanti kesildi.");
                }

                totalReceived += received;
            }
        }

        static async Task SendAllAsync(
            Socket socket, ReadOnlyMemory<byte> data)
        {
            int totalSent = 0;

            while (totalSent < data.Length)
            {
                int sent = await socket.SendAsync(
                    data[totalSent..],
                    SocketFlags.None);

                if (sent == 0)
                {
                    throw new IOException(
                        "Cavab gonderile bilmedi.");
                }

                totalSent += sent;
            }
        }
    }
}
