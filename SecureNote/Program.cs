using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace SecureNote
{
    public static class Program
    {
        //public static async Task Main(string[] args)
        //{
        //    //RSA rsa = RSA.Create();
        //    //Console.WriteLine(Convert.ToBase64String(rsa.ExportRSAPublicKey()));
        //    //Console.WriteLine();
        //    ////Console.WriteLine(Convert.ToBase64String(rsa.ExportRSAPrivateKey()));
        //    ////Console.WriteLine();
        //    //RSA rsa2 = RSA.Create();
        //    //Console.WriteLine(Convert.ToBase64String(rsa2.ExportRSAPublicKey()));
        //    //Console.WriteLine();
        //    //Console.WriteLine(Convert.ToBase64String(rsa2.ExportRSAPrivateKey()));
        //    //Console.WriteLine();
        //    //rsa2.ImportRSAPublicKey(rsa.ExportRSAPublicKey(), out int _);
        //    //Console.WriteLine(Convert.ToBase64String(rsa2.ExportRSAPublicKey()));
        //    //Console.WriteLine();
        //    //Console.WriteLine(Convert.ToBase64String(rsa2.ExportRSAPrivateKey()));
        //    //Console.WriteLine();
        //    ////Console.WriteLine(Convert.ToBase64String(rsa.ExportRSAPrivateKey()));
        //    //return;
        //    //Aes aes = Aes.Create();
        //    //aes.Padding = PaddingMode.None;
        //    //aes.Key = Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000");
        //    //aes.IV = Convert.FromHexString("00000000000000000000000000000000");
        //    //Aes aes2 = Aes.Create();
        //    //aes2.Padding = PaddingMode.None;
        //    //aes2.Key = aes.Key;
        //    //aes2.IV = aes.IV;
        //    //CryptoStream crypto1 = new CryptoStream(new ZeroStream(), aes.CreateEncryptor(), CryptoStreamMode.Read);
        //    //CryptoStream crypto2 = new CryptoStream(new ZeroStream(), aes2.CreateEncryptor(), CryptoStreamMode.Read);
        //    //byte[] buffer = new byte[16];
        //    //crypto1.Read(buffer, 0, 16);
        //    //Console.WriteLine(Convert.ToHexString(buffer));
        //    //crypto2.Read(buffer, 0, 16);
        //    //Console.WriteLine(Convert.ToHexString(buffer));
        //    //return;
        //    Console.Write("Server (Y/n): ");
        //    if (Console.ReadLine()?.ToLower()[0] == 'y')
        //    {
        //        await new SecureNoteServer("127.0.0.1", 8888).ListenAsync();
        //    }
        //    else
        //    {
        //        RSA rsa = RSA.Create();
        //        var client = new SecureNoteClient("127.0.0.1", 8888, rsa);
        //        //client.OnMessage += (string x) => Console.WriteLine("Got: {0}", x);
        //        //byte[] buffer = new byte[4];
        //        //BinaryPrimitives.WriteInt32LittleEndian(buffer, 1234);
        //        //Thread.Sleep(5000);
        //        await client.DownloadFile("test.txt");
        //        Console.WriteLine("Press key to upload...");
        //        Console.ReadKey(true);
        //        await client.UploadFile("test.txt");
        //    }
        //    Console.WriteLine("Press key to exit...");
        //    Console.ReadKey(true);
        //}


        public static void Main(string[] args)
        {
            //FileInfo exceptionsFile = new FileInfo("log.txt");
            //TextWriter exceptionWriter = new StreamWriter(exceptionsFile.FullName);
            //Console.SetError(exceptionWriter);

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
                { "download" , DownloadFunc },
                { "upload" , UploadFunc },
                { "list" , ListFunc },
                //{ "genrsa" , GenRsaFunc },
                //{ "loadrsa" , LoadRsaFunc },
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
                //{ "genrsa" , "generate and save rsa public/private key" },
                //{ "loadrsa" , "load rsa from save" },
            };

        private static void ServerFunc(string[] obj)
        {
            if (obj.Length == 0)
                obj = new string[] { "127.0.0.1", "8888" };
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
        private static RSA? _rsa = null;
        private static SecureNoteClient _client;
        private static void ConnectFunc(string[] obj)
        {
            if (obj.Length == 0)
                obj = new string[] { "127.0.0.1", "8888" };
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
            _rsa ??= RSA.Create();
            _client = new SecureNoteClient(address, port, _rsa);
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

        //public static void GenRsaFunc(string[] args)
        //{
        //    var pass = string.Empty;
        //    ConsoleKey key;
        //    Console.Write("Pass: ");
        //    do
        //    {
        //        var keyInfo = Console.ReadKey(intercept: true);
        //        key = keyInfo.Key;

        //        if (key == ConsoleKey.Backspace && pass.Length > 0)
        //        {
        //            Console.Write("\b \b");
        //            pass = pass[0..^1];
        //        }
        //        else if (!char.IsControl(keyInfo.KeyChar))
        //        {
        //            Console.Write("*");
        //            pass += keyInfo.KeyChar;
        //        }
        //    } while (key != ConsoleKey.Enter);
        //    Console.WriteLine(pass);
        //    _rsa = RSA.Create();
        //    byte[] prvKey = _rsa.ExportRSAPrivateKey();
        //    //File.WriteAllBytes("rsa.key", );
        //    Aes aes = Aes.Create();
        //    SHA256 sha256 = SHA256.Create();
        //    aes.Key = sha256.ComputeHash(Encoding.UTF8.GetBytes(pass));

        //    SHA1 sha1 = SHA1.Create();
        //    aes.IV = sha1.ComputeHash(Encoding.UTF8.GetBytes(pass));

        //    aes.Enc
        //    //ae
        //    //sha256.ComputeHash();
        //    //_rsa.ex
        //}
        //public static void LoadRsaFunc(string[] args)
        //{
        //    if (!File.Exists("rsa.key"))
        //    {
        //        Console.WriteLine("File with key not found");
        //        return;
        //    }
        //    var pass = string.Empty;
        //    ConsoleKey key;
        //    Console.Write("Pass: ");
        //    do
        //    {
        //        var keyInfo = Console.ReadKey(intercept: true);
        //        key = keyInfo.Key;

        //        if (key == ConsoleKey.Backspace && pass.Length > 0)
        //        {
        //            Console.Write("\b \b");
        //            pass = pass[0..^1];
        //        }
        //        else if (!char.IsControl(keyInfo.KeyChar))
        //        {
        //            Console.Write("*");
        //            pass += keyInfo.KeyChar;
        //        }
        //    } while (key != ConsoleKey.Enter);
        //    Console.WriteLine(pass);
        //    _rsa = RSA.Create();
        //    _rsa.ImportRSAPrivateKey(File.ReadAllBytes("rsa.key"), out int _);
        //}

        public static void HelpFunc(string[] args)
        {
            foreach (var kv in commandMan)
                Console.WriteLine(kv.Key + " - " + kv.Value);
        }
    }
}