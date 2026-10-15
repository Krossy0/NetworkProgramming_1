using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FileClient
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            while (true)
            {
                Console.Clear();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("===== FILE CLIENT =====");
                Console.ResetColor();

                Console.WriteLine("1 - Fayl gonder (sekil/ses)");
                Console.WriteLine("0 - Cixis");
                Console.Write("Secim: ");

                string? choice = Console.ReadLine();

                if (choice == "0")
                    break;

                if (choice != "1")
                {
                    Console.WriteLine("Yanlis secim!");
                    Console.ReadLine();
                    continue;
                }

                Console.Write("Faylin yolunu daxil edin: ");
                string? path = Console.ReadLine()?.Trim('"');

                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    Console.WriteLine("Fayl tapilmadi!");
                    Console.ReadLine();
                    continue;
                }

                try
                {
                    await SendFileAsync(path);
                    Console.WriteLine("Fayl ugurla gonderildi!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Xeta: " + ex.Message);
                }

                Console.WriteLine("Davam etmek ucun Enter basin.");
                Console.ReadLine();
            }
        }

        static async Task SendFileAsync(string path)
        {
            string fileName = Path.GetFileName(path);
            byte[] nameBytes = Encoding.UTF8.GetBytes(fileName);
            long fileSize = new FileInfo(path).Length;

            using Socket client = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);

            await client.ConnectAsync(
                new IPEndPoint(IPAddress.Loopback, 5000));

            byte[] header = new byte[12];
            BinaryPrimitives.WriteInt32BigEndian(
                header.AsSpan(0, 4), nameBytes.Length);

            BinaryPrimitives.WriteInt64BigEndian(
                header.AsSpan(4, 8), fileSize);

            await SendAllAsync(client, header);
            await SendAllAsync(client, nameBytes);

            using FileStream fs = new FileStream(
                path, FileMode.Open, FileAccess.Read);

            byte[] buffer = new byte[8192];
            int read;

            while ((read = await fs.ReadAsync(buffer)) > 0)
            {
                await SendAllAsync(
                    client, buffer.AsMemory(0, read));
            }

            byte[] response = new byte[2];
            await ReceiveExactlyAsync(client, response);

            if (Encoding.UTF8.GetString(response) != "OK")
                throw new Exception("Server fayli tesdiq etmedi.");
        }

        static async Task SendAllAsync(
            Socket socket, ReadOnlyMemory<byte> data)
        {
            int sentTotal = 0;

            while (sentTotal < data.Length)
            {
                int sent = await socket.SendAsync(
                    data[sentTotal..], SocketFlags.None);

                if (sent == 0)
                    throw new Exception("Fayl gonderile bilmedi.");

                sentTotal += sent;
            }
        }

        static async Task ReceiveExactlyAsync(
            Socket socket, byte[] data)
        {
            int receivedTotal = 0;

            while (receivedTotal < data.Length)
            {
                int received = await socket.ReceiveAsync(
                    data.AsMemory(receivedTotal),
                    SocketFlags.None);

                if (received == 0)
                    throw new Exception("Server ile elaqe kesildi.");

                receivedTotal += received;
            }
        }
    }
}