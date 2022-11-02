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
    public class MiddleManServer
    {
        private readonly TcpListener _listener;
        private readonly HashSet<MiddleManConnection> _connections = new();
        string _serverAddress;
        int _serverPort;
        public MiddleManServer(string middleAddress, int middlePort, string serverAddress, int serverPort)
        {
            _listener = new TcpListener(IPAddress.Parse(middleAddress), middlePort);
            _serverAddress = serverAddress;
            _serverPort = serverPort;
        }
        public async Task ListenAsync()
        {
            try
            {
                _listener.Start();

                while (true)
                {
                    Console.WriteLine("Connections wait...");
                    TcpClient userClient = await _listener.AcceptTcpClientAsync();
                    TcpClient serverClient = new TcpClient(_serverAddress, _serverPort);
                    lock (_connections)
                        _connections.Add(new MiddleManConnection(userClient, serverClient));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
    public class MiddleManConnection
    {
        private readonly string _endPoint;
        private readonly SecureStream _userStream;
        private readonly SecureStream _serverStream;
        private readonly Task _taskS2U;
        private readonly Task _taskU2S;
        public MiddleManConnection(TcpClient userClient, TcpClient serverClient)
        {
            _userStream = new SecureStream(userClient.GetStream());
            _userStream.HandshakeAsServerAsync().GetAwaiter().GetResult();
            _serverStream = new SecureStream(serverClient.GetStream());
            _serverStream.HandshakeAsClientAsync().GetAwaiter().GetResult();
            _endPoint = userClient.Client.RemoteEndPoint?.ToString() ?? "NULL";
            Console.WriteLine($"Client connected: {_endPoint}");
            _taskU2S = RunMainLoop(_userStream, _serverStream, "User");
            _taskS2U = RunMainLoop(_serverStream, _userStream, "Server");
        }

        private async Task RunMainLoop(SecureStream inputStream, SecureStream outputStream, string name)
        {
            await Task.Yield(); // https://ru.stackoverflow.com/a/1422205/373567
            try
            {
                byte[] buffer = new byte[4096];
                while (true)
                {
                    int received = await inputStream.ReadAsync(buffer);
                    if (received > 0)
                    {
                        Console.WriteLine($"{name} {_endPoint} send {received} bytes: {Convert.ToHexString(buffer, 0, received)}");
                        outputStream.Write(buffer, 0, received);
                    }
                }
            }
            catch (IOException)
            {
                Console.WriteLine($"{name} {_endPoint} closed by other side.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
