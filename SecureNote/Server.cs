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
        SignUp,
        SignIn,
        FileList,
        FileDownload,
        FileUpload,
        FileRename,
        FileDelete,
    }

    [ProtoContract]
    public struct SecureNoteSignUpRequest
    {
        [ProtoMember(1)]
        public string username;
        [ProtoMember(2)]
        public string password;
    }
    [ProtoContract]
    public struct SecureNoteSignUpResponse
    {
        [ProtoMember(1)]
        public bool success;
    }

    [ProtoContract]
    public struct SecureNoteSignInRequest
    {
        [ProtoMember(1)]
        public string username;
        [ProtoMember(2)]
        public string password;
    }
    [ProtoContract]
    public struct SecureNoteSignInResponse
    {
        [ProtoMember(1)]
        public bool success;
    }

    [ProtoContract]
    public struct SecureNoteFileListResponse
    {
        [ProtoMember(1)]
        public bool success;
        [ProtoMember(2)]
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
        public bool success;
        [ProtoMember(2)]
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
    [ProtoContract]
    public struct SecureNoteFileUploadResponse
    {
        [ProtoMember(1)]
        public bool success;
    }

    public class SecureNoteServer
    {
        private readonly TcpListener _listener;
        private readonly HashSet<SecureNoteConnection> _connections = new();
        private readonly AuthService _authService = new AuthService("users.json");
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
                        _connections.Add(new SecureNoteConnection(client, _authService));
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
        private string? _username = null;
        private AuthService _authService;
        public SecureNoteConnection(TcpClient client, AuthService authService)
        {
            _stream = new SecureStream(client.GetStream());
            _endPoint = client.Client.RemoteEndPoint?.ToString() ?? "NULL";
            _authService = authService;
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
                        case SecureNoteActions.SignUp:
                            {
                                await _stream.ReadAsync(emptyByteArray, 0, 0);
                                var request = Serializer.DeserializeWithLengthPrefix<SecureNoteSignInRequest>(_stream, PrefixStyle.Fixed32);
                                var response = new SecureNoteSignInResponse
                                {
                                    success = _authService.CreateUser(request.username, request.password),
                                };
                                if (response.success)
                                    _username = request.username;
                                using (var ms = new MemoryStream())
                                {
                                    Serializer.SerializeWithLengthPrefix(ms, response, PrefixStyle.Fixed32);
                                    await _stream.WriteAsync(ms.GetBuffer(), 0, (int)ms.Position);
                                }
                                break;
                            }
                        case SecureNoteActions.SignIn:
                            {
                                await _stream.ReadAsync(emptyByteArray, 0, 0);
                                var request = Serializer.DeserializeWithLengthPrefix<SecureNoteSignInRequest>(_stream, PrefixStyle.Fixed32);
                                var response = new SecureNoteSignInResponse
                                {
                                    success = _authService.CheckUser(request.username, request.password),
                                };
                                if (response.success)
                                    _username = request.username;
                                using (var ms = new MemoryStream())
                                {
                                    Serializer.SerializeWithLengthPrefix(ms, response, PrefixStyle.Fixed32);
                                    await _stream.WriteAsync(ms.GetBuffer(), 0, (int)ms.Position);
                                }
                                break;
                            }
                        case SecureNoteActions.FileList:
                            {
                                var response = new SecureNoteFileListResponse
                                {
                                    success = _username != null,
                                    files = null!,
                                };
                                string path = "storage/" + _username + "/";
                                if (response.success)
                                {
                                    if (!Directory.Exists(path))
                                        Directory.CreateDirectory(path);
                                    response.files = Directory.GetFiles(path);
                                    for (int i = 0; i < response.files.Length; i++)
                                        response.files[i] = Path.GetFileName(response.files[i]);
                                }
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

                                var response = new SecureNoteFileDownloadResponse
                                {
                                    success = _username != null,
                                    file = null!,
                                };
                                if (response.success)
                                {
                                    Console.WriteLine($"{_endPoint} as {_username}: Downloading {request.filename}");
                                    string path = "storage/" + _username + "/";
                                    if (!Directory.Exists(path))
                                        Directory.CreateDirectory(path);
                                    if (!File.Exists(path + request.filename))
                                        File.Create(path + request.filename).Close();
                                    response.file = await File.ReadAllBytesAsync(path + request.filename);
                                }
                                using (var ms = new MemoryStream())
                                {
                                    Serializer.SerializeWithLengthPrefix(ms, response, PrefixStyle.Fixed32);
                                    await _stream.WriteAsync(ms.GetBuffer(), 0, (int)ms.Position);
                                }
                                break;
                            }
                        case SecureNoteActions.FileUpload:
                            {
                                await _stream.ReadAsync(emptyByteArray, 0, 0);
                                var request = Serializer.DeserializeWithLengthPrefix<SecureNoteFileUploadRequest>(_stream, PrefixStyle.Fixed32);
                                if (_username == null)
                                    break;
                                string path = "storage/" + _username + "/";
                                Console.WriteLine($"{_endPoint} as {_username}: Uploading {request.filename}");
                                if (!Directory.Exists(path))
                                    Directory.CreateDirectory(path);
                                File.WriteAllBytes(path + request.filename, request.file);
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
                Console.WriteLine($"Client {_endPoint} as {_username} disconnected.");
                _stream.Close();
            }
            catch (IOException)
            {
                Console.WriteLine($"Connection {_endPoint} as {_username} closed by server.");
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
