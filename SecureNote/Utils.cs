using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SecureNote
{
    public static class Utils
    {
        public static byte[] EncryptBytes(byte[] bytes, string key)
        {
            using (var ms = new MemoryStream())
            {
                var aes = Aes.Create();
                aes.Padding = PaddingMode.None;
                aes.Key = SHA256.HashData(Encoding.UTF8.GetBytes(key));
                var crypto = new CryptoStream(new ZeroStream(), aes.CreateEncryptor(), CryptoStreamMode.Read);
                ms.Write(aes.IV);
                for (int i = 0; i < bytes.Length; i++)
                    ms.WriteByte((byte)(bytes[i] ^ crypto.ReadByte()));
                return ms.ToArray();
            }
        }
        public static byte[] DecryptBytes(byte[] bytes, string key)
        {
            using (var ms = new MemoryStream())
            {
                var aes = Aes.Create();
                aes.Padding = PaddingMode.None;
                aes.Key = SHA256.HashData(Encoding.UTF8.GetBytes(key));
                var iv = new byte[aes.IV.Length];
                Buffer.BlockCopy(bytes, 0, iv, 0, iv.Length);
                aes.IV = iv;
                var crypto = new CryptoStream(new ZeroStream(), aes.CreateEncryptor(), CryptoStreamMode.Read);
                for (int i = iv.Length; i < bytes.Length; i++)
                    ms.WriteByte((byte)(bytes[i] ^ crypto.ReadByte()));
                return ms.ToArray();
            }
        }
    }
}
