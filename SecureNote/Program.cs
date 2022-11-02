using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ProtoBuf;

namespace SecureNote
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Welcome to SecureNote. Type in a command.");

            while (true)
            {
                Console.Write("$ ");
                string command = Console.ReadLine();

                string command_main = command.Split(new char[] { ' ' }).First();
                string[] arguments = command.Split(new char[] { ' ' }).Skip(1).ToArray();
                if (lCommands.ContainsKey(command_main))
                {
                    Action<string[]> function_to_execute = null;
                    lCommands.TryGetValue(command_main, out function_to_execute);
                    try
                    {
                        function_to_execute(arguments);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
                else
                    Console.WriteLine("Command '" + command_main + "' not found");
            }
        }

        private static Dictionary<string, Action<string[]>> lCommands =
            new Dictionary<string, Action<string[]>>()
            {
                { "help", HelpFunc },
                { "server" , ServerFunc },
                { "connect" , ConnectFunc },
                { "signup" , SignUpFunc },
                { "signin" , SignInFunc },
                { "download" , DownloadFunc },
                { "upload" , UploadFunc },
                { "list" , ListFunc },
                { "password" , PasswordFunc },
            };
        private static Dictionary<string, string> commandMan =
            new Dictionary<string, string>()
            {
                { "help", "show command list"},
                { "server", "create server (address) (port)"},
                { "connect", "connect server (address) (port)"},
                { "download" , "download file by name from server (filename)" },
                { "upload" , "upload file by name to server (filename)" },
                { "list" , "get file list" },
                { "password" , "set file encrypt password" },
            };

        private static void ServerFunc(string[] obj)
        {
            if (obj.Length == 0)
                obj = new string[] { "127.0.0.1", "18888" };
            if (obj.Length != 2)
            {
                Console.WriteLine("Wrong param count: address port");
                return;
            }    
            string address = obj[0];
            if (!int.TryParse(obj[1], out int port))
            {
                Console.WriteLine("port should be a number!");
                return;
            }
            new SecureNoteServer(address, port).ListenAsync().GetAwaiter().GetResult();
        }
        private static SecureNoteClient _client;
        private static void ConnectFunc(string[] obj)
        {
            if (obj.Length == 0)
                obj = new string[] { "127.0.0.1", "18888" };
            if (obj.Length != 2)
            {
                Console.WriteLine("Wrong param count: address port");
                return;
            }
            string address = obj[0];
            if (!int.TryParse(obj[1], out int port))
            {
                Console.WriteLine("port should be a number!");
                return;
            }
            _client = new SecureNoteClient(address, port);
        }

        public static void SignUpFunc(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("not enough args!");
                return;
            }
            Console.WriteLine("Sign Up result: {0}", _client.SignUp(args[0], args[1]).GetAwaiter().GetResult());
        }
        public static void SignInFunc(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("not enough args!");
                return;
            }
            Console.WriteLine("Sign In result: {0}", _client.SignIn(args[0], args[1]).GetAwaiter().GetResult());
        }

        public static void PasswordFunc(string[] args)
        {
            if (args.Length == 0)
            {
                _client.SetEncryptPassword(null);
                Console.WriteLine("Encrypt password removed");
            }
            else
            {
                _client.SetEncryptPassword(String.Join(' ', args));
                Console.WriteLine("Encrypt password changed");
            }
        }
        public static void DownloadFunc(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("no file name!");
                return;
            }
            string filename = String.Join(' ', args);
            _client.DownloadFile(filename).GetAwaiter().GetResult();
        }

        public static void UploadFunc(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("no file name!");
                return;
            }
            string filename = String.Join(' ', args);
            _client.UploadFile(filename).GetAwaiter().GetResult();
        }

        public static void ListFunc(string[] args)
        {
            string[] files = _client.GetFiles().GetAwaiter().GetResult();
            foreach (var file in files)
                Console.WriteLine(file);
        }

        public static void HelpFunc(string[] args)
        {
            foreach (var kv in commandMan)
                Console.WriteLine(kv.Key + " - " + kv.Value);
        }
    }
}