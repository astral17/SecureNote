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
using ProtoBuf;
using System.IO;

namespace SecureNote
{
    public enum SecureNoteActions : int
    {
        Ping,
        SignUp,
        SignIn,
        FileList,
        FileDownload,
        FileUpload,
        FileRename,
        FileDelete,
    }
    [ProtoContract]
    public struct SecureNoteFileListResponse
    {
        [ProtoMember(1)]
        public string[] files;
    }
    [ProtoContract]
    public struct SecureNoteFileDownloadRequest
    {
        [ProtoMember(1)]
        public string filename;
    }
    [ProtoContract]
    public struct SecureNoteFileDownloadResponse
    {
        [ProtoMember(1)]
        public byte[] file;
    }

    [ProtoContract]
    public struct SecureNoteFileUploadRequest
    {
        [ProtoMember(1)]
        public string filename;
        [ProtoMember(2)]
        public byte[] file;
    }
    //[ProtoContract]
    //public struct SecureNoteFileUploadResponse
    //{
    //}

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
                    Console.WriteLine("Connections wait...");
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
            Console.WriteLine($"Client connected: {_endPoint}");
            _task = RunMainLoop();
        }

        private async Task RunMainLoop()
        {
            await Task.Yield(); // https://ru.stackoverflow.com/a/1422205/373567
            try
            {
                await _stream.HandshakeAsServerAsync();
                byte[] emptyByteArray = new byte[0];
                while (true)
                {
                    await _stream.ReadAsync(emptyByteArray, 0, 0);
                    var action = Serializer.DeserializeWithLengthPrefix<SecureNoteActions>(_stream, PrefixStyle.Fixed32);
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
                        case SecureNoteActions.FileList: // List
                            {
                                if (!Directory.Exists("storage"))
                                    Directory.CreateDirectory("storage");
                                var response = new SecureNoteFileListResponse
                                {
                                    files = Directory.GetFiles("storage"),
                                };
                                for (int i = 0; i < response.files.Length; i++)
                                    response.files[i] = Path.GetFileName(response.files[i]);
                                using (var ms = new MemoryStream())
                                {
                                    Serializer.SerializeWithLengthPrefix(ms, response, PrefixStyle.Fixed32);
                                    await _stream.WriteAsync(ms.GetBuffer(), 0, (int)ms.Position);
                                }
                                break;
                            }
                        case SecureNoteActions.FileDownload:
                            {
                                await _stream.ReadAsync(emptyByteArray, 0, 0);
                                var request = Serializer.DeserializeWithLengthPrefix<SecureNoteFileDownloadRequest>(_stream, PrefixStyle.Fixed32);

                                Console.WriteLine($"{_endPoint}: Downloading {request.filename}");
                                MemoryStream memory = new MemoryStream();
                                if (!Directory.Exists("storage"))
                                    Directory.CreateDirectory("storage");
                                if (!File.Exists("storage/" + request.filename))
                                    File.Create("storage/" + request.filename).Close();
                                var response = new SecureNoteFileDownloadResponse
                                {
                                    file = await File.ReadAllBytesAsync("storage/" + request.filename),
                                };
                                using (var ms = new MemoryStream())
                                {
                                    Serializer.SerializeWithLengthPrefix(ms, response, PrefixStyle.Fixed32);
                                    await _stream.WriteAsync(ms.GetBuffer(), 0, (int)ms.Position);
                                }
                                break;
                            }
                        case SecureNoteActions.FileUpload: // Upload
                            {
                                await _stream.ReadAsync(emptyByteArray, 0, 0);
                                var request = Serializer.DeserializeWithLengthPrefix<SecureNoteFileUploadRequest>(_stream, PrefixStyle.Fixed32);
                                Console.WriteLine($"{_endPoint}: Uploading {request.filename}");
                                if (!Directory.Exists("storage"))
                                    Directory.CreateDirectory("storage");
                                File.WriteAllBytes("storage/" + request.filename, request.file);
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
