using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Buffers.Binary;
using System.IO.Enumeration;

namespace SecureNote
{
    public class SecureNoteServer
    {
        private readonly TcpListener _listener;
        private readonly HashSet<SecureNoteConnection> _connections = new();
        public SecureNoteServer(string address, int port)
        {
            _listener = new TcpListener(IPAddress.Parse(address), port);
        }
        public async Task ListenAsync()
        {
            try
            {
                _listener.Start();

                while (true)
                {
                    Console.WriteLine("Ожидание подключений...");
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    lock (_connections)
                        _connections.Add(new SecureNoteConnection(client));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
    public class SecureNoteConnection
    {
        private readonly string _endPoint;
        private readonly SecureStream _stream;
        private readonly Task _task;
        public SecureNoteConnection(TcpClient client)
        {
            _stream = new SecureStream(client.GetStream());
            _endPoint = client.Client.RemoteEndPoint?.ToString() ?? "NULL";
            Console.WriteLine($"Подключен клиент: {_endPoint}");
            _task = RunMainLoop();
        }

        private async Task RunMainLoop()
        {
            await Task.Yield(); // https://ru.stackoverflow.com/a/1422205/373567
            try
            {
                await _stream.HandshakeAsServerAsync();
                byte[] headerBuffer = new byte[4];
                int received = 0;
                //int received = await _stream.ReadAsync(headerBuffer, 0, 4);
                //if (received != 4)
                //    throw new NotSupportedException("Unknown header");
                while (true)
                {
                    received = await _stream.ReadAsync(headerBuffer, 0, 4);
                    if (received != 4)
                        break;
                    string action = Encoding.UTF8.GetString(headerBuffer);
                    //int length = BinaryPrimitives.ReadInt32LittleEndian(headerBuffer);
                    //Console.WriteLine($"{_endPoint}: Got {length}");
                    switch (action)
                    {
                        //case "ping":
                        //    {
                        //        await SendTextAsync("pong");
                        //        break;
                        //    }
                        //case "snau":
                        //    {
                        //        byte[] buffer = new byte[64];
                        //        do
                        //        {
                        //            received += await _stream.ReadAsync(buffer, received, buffer.Length - received);
                        //        } while (received < buffer.Length);
                        //        if (received != 64)
                        //            throw new NotSupportedException("auth wrong length");
                        //        string login = Encoding.UTF8.GetString(buffer, 0, 32).TrimEnd('\x0');
                        //        string password = Encoding.UTF8.GetString(buffer, 32, 32).TrimEnd('\x0');
                        //        if (login == "admin" && password == "admin")
                        //        {
                        //            MemoryStream memory = new MemoryStream(buffer);
                        //            memory.Write("123");
                        //            _stream.WriteAsync(buffer,)
                        //        }
                        //        break;
                        //    }
                        //case "snre":
                        //    {
                        //        break;
                        //    }
                        case "snfl": // List
                            {
                                MemoryStream memory = new MemoryStream();
                                if (!Directory.Exists("storage"))
                                    Directory.CreateDirectory("storage");
                                var list = Directory.GetFiles("storage");
                                byte[] buffer;
                                using (BinaryWriter bw = new BinaryWriter(memory))
                                {
                                    bw.Write(list.Length);
                                    foreach (var file in list)
                                    {
                                        buffer = Encoding.UTF8.GetBytes(Path.GetFileName(file).PadRight(32, '\x0'));
                                        bw.Write(buffer, 0, 32);
                                    }
                                }
                                buffer = memory.ToArray();
                                await _stream.WriteAsync(buffer, 0, buffer.Length);
                                break;
                            }
                        case "snfd": // Download
                            {
                                byte[] buffer = await ReadAsync(32);
                                string filename = Encoding.UTF8.GetString(buffer, 0, 32).TrimEnd('\x0');
                                Console.WriteLine($"{_endPoint}: Downloading {filename}");
                                MemoryStream memory = new MemoryStream();
                                using (BinaryWriter bw = new BinaryWriter(memory))
                                {
                                    if (!Directory.Exists("storage"))
                                        Directory.CreateDirectory("storage");
                                    filename = "storage/" + filename;
                                    if (!File.Exists(filename))
                                        File.Create(filename).Close();
                                    byte[] fileBytes = await File.ReadAllBytesAsync(filename);
                                    bw.Write(fileBytes.Length);
                                    bw.Write(fileBytes);
                                }
                                buffer = memory.ToArray();
                                await _stream.WriteAsync(buffer, 0, buffer.Length);
                                break;
                            }
                        case "snfu": // Upload
                            {
                                byte[] buffer = await ReadAsync(32);
                                string filename = Encoding.UTF8.GetString(buffer, 0, 32).TrimEnd('\x0');
                                Console.WriteLine($"{_endPoint}: Uploading {filename}");
                                if (!Directory.Exists("storage"))
                                    Directory.CreateDirectory("storage");
                                filename = "storage/" + filename;
                                int length = BinaryPrimitives.ReadInt32LittleEndian(await ReadAsync(4));
                                File.WriteAllBytes(filename, await ReadAsync(length));
                                break;
                            }
                        //case "snfr": // Rename
                        //    {
                        //        break;
                        //    }
                        //case "snfe": // Erase
                        //    {
                        //        break;
                        //    }
                    }
                }
                Console.WriteLine($"Клиент {_endPoint} отключился.");
                _stream.Close();
            }
            catch (IOException)
            {
                Console.WriteLine($"Подключение к {_endPoint} закрыто сервером.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.GetType().Name + ": " + ex.Message);
            }
            //if (!disposed)
            //    _disposeCallback(this);
        }

        public async Task SendTextAsync(string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message, 0, message.Length);
            await _stream.WriteAsync(buffer, 0, buffer.Length);
        }
        public async Task SendAsync(byte[] buffer, int offset, int count)
        {
            await _stream.WriteAsync(buffer, offset, count);
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

        //private async Task RunWritingLoop()
        //{
        //    byte[] header = new byte[4];
        //    await foreach (string message in _channel.Reader.ReadAllAsync())
        //    {
        //        byte[] buffer = Encoding.UTF8.GetBytes(message);
        //        BinaryPrimitives.WriteInt32LittleEndian(header, buffer.Length);
        //        await _stream.WriteAsync(header, 0, header.Length);
        //        await _stream.WriteAsync(buffer, 0, buffer.Length);
        //    }
        //}

        //public void Dispose()
        //{
        //    Dispose(true);
        //    GC.SuppressFinalize(this);
        //}

        //protected virtual void Dispose(bool disposing)
        //{
        //    if (disposed)
        //        throw new ObjectDisposedException(GetType().FullName);
        //    disposed = true;
        //    if (_client.Connected)
        //    {
        //        _channel.Writer.Complete();
        //        _stream.Close();
        //        Task.WaitAll(_readingTask, _writingTask);
        //    }
        //    if (disposing)
        //    {
        //        _client.Dispose();
        //    }
        //}

        //~SecureNoteConnection() => Dispose(false);

    }
}
