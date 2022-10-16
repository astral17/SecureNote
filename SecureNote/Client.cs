using System.Net.Sockets;
using System.Text;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System;

namespace SecureNote
{
    public class SecureNoteClient
    {
        private readonly TcpClient _client;
        private readonly SecureStream _stream;
        //private readonly Task _task;
        public SecureNoteClient(string address, int port, RSA rsa)
        {
            _client = new TcpClient(address, port);
            _stream = new SecureStream(_client.GetStream());
            _stream.HandshakeAsClientAsync(rsa).GetAwaiter().GetResult();
            //_task = RunMainLoop();
        }
        //private async Task RunMainLoop()
        //{
        //    try
        //    {
        //        while (true)
        //        {
        //            byte[] headerBuffer = new byte[4];
        //            await _stream.ReadAsync(headerBuffer, 0, 4);
        //            OnMessage?.Invoke(Encoding.UTF8.GetString(headerBuffer));
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex.Message);
        //    }
        //}
        public async Task SendAsync(string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message, 0, message.Length);
            await _stream.WriteAsync(buffer, 0, buffer.Length);
        }

        public async Task<string[]> GetFiles()
        {
            await SendAsync("snfl");
            int count = BinaryPrimitives.ReadInt32LittleEndian(await ReadAsync(4));
            string[] result = new string[count];
            for (int i = 0; i < count; i++)
                result[i] = Encoding.UTF8.GetString(await ReadAsync(32), 0, 32).TrimEnd('\x0');
            return result;
        }
        public async Task DownloadFile(string filename)
        {
            if (filename.Length > 32)
                filename = filename.Substring(0, 32);
            await SendAsync("snfd" + filename.PadRight(32, '\x0'));
            int length = BinaryPrimitives.ReadInt32LittleEndian(await ReadAsync(4));
            if (!Directory.Exists("workspace"))
                Directory.CreateDirectory("workspace");
            File.WriteAllBytes("workspace/" + filename, await ReadAsync(length));
        }
        public async Task UploadFile(string filename)
        {
            if (filename.Length > 32)
                filename = filename.Substring(0, 32);
            await SendAsync("snfu" + filename.PadRight(32, '\x0'));
            MemoryStream memory = new MemoryStream();
            using (BinaryWriter bw = new BinaryWriter(memory))
            {
                byte[] fileBytes = await File.ReadAllBytesAsync("workspace/" + filename);
                bw.Write(fileBytes.Length);
                bw.Write(fileBytes);
            }
            byte[] buffer = memory.ToArray();
            await _stream.WriteAsync(buffer, 0, buffer.Length);
        }
        public async Task<byte[]> ReadAsync(int length)
        {
            byte[] buffer = new byte[length];
            int received = 0;
            do
            {
                received += await _stream.ReadAsync(buffer, received, length - received);
            } while (received < length);
            return buffer;
        }
        //public event Action<string> OnMessage;
    }
}
