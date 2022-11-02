using System.Net.Sockets;
using System.Text;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System;
using ProtoBuf;

namespace SecureNote
{
    public class SecureNoteClient
    {
        private readonly TcpClient _client;
        private readonly SecureStream _stream;
        //private readonly Task _task;
        private string? _password = null;
        public SecureNoteClient(string address, int port)
        {
            _client = new TcpClient(address, port);
            _stream = new SecureStream(_client.GetStream());
            _stream.HandshakeAsClientAsync().GetAwaiter().GetResult();
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

        public async Task<bool> SignUp(string username, string password)
        {
            Serializer.SerializeWithLengthPrefix(_stream, SecureNoteActions.SignUp, PrefixStyle.Fixed32);
            var request = new SecureNoteSignInRequest
            {
                username = username,
                password = password,
            };
            Serializer.SerializeWithLengthPrefix(_stream, request, PrefixStyle.Fixed32);
            byte[] emptyByteArray = new byte[0];
            await _stream.ReadAsync(emptyByteArray, 0, 0);
            var response = Serializer.DeserializeWithLengthPrefix<SecureNoteSignUpResponse>(_stream, PrefixStyle.Fixed32);
            return response.success;
        }
        public async Task<bool> SignIn(string username, string password)
        {
            Serializer.SerializeWithLengthPrefix(_stream, SecureNoteActions.SignIn, PrefixStyle.Fixed32);
            var request = new SecureNoteSignInRequest
            {
                username = username,
                password = password,
            };
            Serializer.SerializeWithLengthPrefix(_stream, request, PrefixStyle.Fixed32);
            byte[] emptyByteArray = new byte[0];
            await _stream.ReadAsync(emptyByteArray, 0, 0);
            var response = Serializer.DeserializeWithLengthPrefix<SecureNoteSignInResponse>(_stream, PrefixStyle.Fixed32);
            return response.success;
        }

        public async Task<string[]> GetFiles()
        {
            Serializer.SerializeWithLengthPrefix(_stream, SecureNoteActions.FileList, PrefixStyle.Fixed32);
            byte[] emptyByteArray = new byte[0];
            await _stream.ReadAsync(emptyByteArray, 0, 0);
            var response = Serializer.DeserializeWithLengthPrefix<SecureNoteFileListResponse>(_stream, PrefixStyle.Fixed32);
            response.files ??= new string[0];
            return response.files;
        }
        public void SetEncryptPassword(string? password)
        {
            _password = password;
        }
        public async Task DownloadFile(string filename)
        {
            var request = new SecureNoteFileDownloadRequest
            {
                filename = filename,
            };
            Serializer.SerializeWithLengthPrefix(_stream, SecureNoteActions.FileDownload, PrefixStyle.Fixed32);
            Serializer.SerializeWithLengthPrefix(_stream, request, PrefixStyle.Fixed32);
            byte[] emptyByteArray = new byte[0];
            await _stream.ReadAsync(emptyByteArray, 0, 0);
            var response = Serializer.DeserializeWithLengthPrefix<SecureNoteFileDownloadResponse>(_stream, PrefixStyle.Fixed32);
            if (!Directory.Exists("workspace"))
                Directory.CreateDirectory("workspace");
            if (_password != null)
                response.file = Utils.DecryptBytes(response.file, _password);
            File.WriteAllBytes("workspace/" + filename, response.file);
        }
        public async Task UploadFile(string filename)
        {
            var request = new SecureNoteFileUploadRequest
            {
                filename = filename,
                file = await File.ReadAllBytesAsync("workspace/" + filename),
            };
            if (_password != null)
                request.file = Utils.EncryptBytes(request.file, _password);
            Serializer.SerializeWithLengthPrefix(_stream, SecureNoteActions.FileUpload, PrefixStyle.Fixed32);
            Serializer.SerializeWithLengthPrefix(_stream, request, PrefixStyle.Fixed32);
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
