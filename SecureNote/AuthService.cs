using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Security.Cryptography;

namespace SecureNote
{
    public class AuthService
    {
        private Dictionary<string, string> userPasswords;
        private string _path;
        public AuthService(string path)
        {
            _path = path;
            if (!File.Exists(path))
            {
                userPasswords = new Dictionary<string, string>();
                Save();
            }
            FileStream file = File.OpenRead(path);
            userPasswords = JsonSerializer.Deserialize<Dictionary<string, string>>(file);
            file.Close();
        }
        public void Save()
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(userPasswords));
        }
        public bool CheckUser(string username, string password)
        {
            return userPasswords.TryGetValue(username, out string? pass) && Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))) == pass;
        }
        public bool CreateUser(string username, string password)
        {
            if (userPasswords.ContainsKey(username) || username.Length < 3 || password.Length < 3)
                return false;
            userPasswords[username] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
            Save();
            return true;
        }
    }
}
