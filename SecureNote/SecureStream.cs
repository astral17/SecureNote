using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace SecureNote
{
    public class SecureStream : Stream
    {
        private Stream _stream;
        private bool _leaveStreamOpen;
        public const int HandshakeNewSession = 1853058675;
        public const int HandshakeLoadSession = 1819504243;
        CryptoStream _readCrypto = null!;
        CryptoStream _writeCrypto = null!;

        public SecureStream(Stream stream, bool leaveStreamOpen = false)
        {
            _stream = stream;
            _leaveStreamOpen = leaveStreamOpen;
        }
        public async Task<bool> HandshakeAsServerAsync()
        {
            Console.WriteLine("HandshakeAsServerAsync");
            int action = await RawReadIntAsync();
            if (action == HandshakeNewSession)
            {
                Console.WriteLine("NewSession");
                int length = await RawReadIntAsync();
                RSA rsa = RSA.Create();
                byte[] buffer = await RawReadExactlyAsync(length);
                rsa.ImportRSAPublicKey(buffer, out int _);
                Console.WriteLine("RSA public:\n{0}\n", Convert.ToHexString(buffer));
                byte[] secret = new byte[64];
                Aes inputAes = Aes.Create();
                Aes outputAes = Aes.Create();
                inputAes.Padding = outputAes.Padding = PaddingMode.None;
                outputAes.Key = inputAes.Key;
                Console.WriteLine("inputKey:\n{0}\n", Convert.ToHexString(inputAes.Key));
                Console.WriteLine("inputIV:\n{0}\n", Convert.ToHexString(inputAes.IV));
                Console.WriteLine("outputKey:\n{0}\n", Convert.ToHexString(outputAes.Key));
                Console.WriteLine("outputIV:\n{0}\n", Convert.ToHexString(outputAes.IV));
                Buffer.BlockCopy(inputAes.Key, 0, secret, 0, 32);
                Buffer.BlockCopy(inputAes.IV, 0, secret, 32, 16);
                Buffer.BlockCopy(outputAes.IV, 0, secret, 48, 16);
                _readCrypto = new CryptoStream(new ZeroStream(), inputAes.CreateEncryptor(), CryptoStreamMode.Read);
                _writeCrypto = new CryptoStream(new ZeroStream(), outputAes.CreateEncryptor(), CryptoStreamMode.Read);
                Console.WriteLine("Secret:\n{0}\n", Convert.ToHexString(secret));
                byte[] encrypted = rsa.Encrypt(secret, RSAEncryptionPadding.Pkcs1);
                
                MemoryStream memory = new MemoryStream();
                using (BinaryWriter bw = new BinaryWriter(memory))
                {
                    bw.Write(encrypted.Length);
                    bw.Write(encrypted);
                }
                buffer = memory.ToArray();
                await _stream.WriteAsync(buffer, 0, buffer.Length);
                return true;
            }
            if (action == HandshakeLoadSession)
            {
                throw new NotImplementedException("TODO: Load session");
            }
            return false;
        }

        public async Task<bool> HandshakeAsClientAsync(RSA rsa)
        {
            Console.WriteLine("HandshakeAsClientAsync");
            MemoryStream memory = new MemoryStream();
            using (BinaryWriter bw = new BinaryWriter(memory))
            {
                byte[] pubKey = rsa.ExportRSAPublicKey();
                Console.WriteLine("RSA public:\n{0}\n", Convert.ToHexString(pubKey));
                bw.Write(HandshakeNewSession);
                bw.Write(pubKey.Length);
                bw.Write(pubKey);
            }
            byte[] buffer = memory.ToArray();
            await _stream.WriteAsync(buffer, 0, buffer.Length);

            int length = await RawReadIntAsync();
            buffer = await RawReadExactlyAsync(length);
            buffer = rsa.Decrypt(buffer, RSAEncryptionPadding.Pkcs1);
            Console.WriteLine("Secret:\n{0}\n", Convert.ToHexString(buffer));
            if (buffer.Length != 64)
                return false;
            Aes inputAes = Aes.Create();
            Aes outputAes = Aes.Create();
            inputAes.Padding = outputAes.Padding = PaddingMode.None;
            byte[] key = new byte[32];
            byte[] iv = new byte[16];
            Buffer.BlockCopy(buffer, 0, key, 0, 32);
            inputAes.Key = outputAes.Key = key;
            Buffer.BlockCopy(buffer, 32, iv, 0, 16);
            outputAes.IV = iv;
            Buffer.BlockCopy(buffer, 48, iv, 0, 16);
            inputAes.IV = iv;
            //Buffer.BlockCopy(buffer, 0, inputAes.Key, 0, 32);
            //Buffer.BlockCopy(buffer, 0, outputAes.Key, 0, 32);
            //Buffer.BlockCopy(buffer, 32, outputAes.IV, 0, 16);
            //Buffer.BlockCopy(buffer, 48, inputAes.IV, 0, 16);
            Console.WriteLine("inputKey:\n{0}\n", Convert.ToHexString(inputAes.Key));
            Console.WriteLine("inputIV:\n{0}\n", Convert.ToHexString(inputAes.IV));
            Console.WriteLine("outputKey:\n{0}\n", Convert.ToHexString(outputAes.Key));
            Console.WriteLine("outputIV:\n{0}\n", Convert.ToHexString(outputAes.IV));
            _readCrypto = new CryptoStream(new ZeroStream(), inputAes.CreateEncryptor(), CryptoStreamMode.Read);
            _writeCrypto = new CryptoStream(new ZeroStream(), outputAes.CreateEncryptor(), CryptoStreamMode.Read);
            return true;
        }

        public override bool CanRead => _stream.CanRead && _readCrypto != null;

        public override bool CanSeek => false;

        public override bool CanWrite => _stream.CanWrite && _writeCrypto != null;

        public override long Length => _stream.Length;

        public override long Position
        {
            get => _stream.Position;
            set
            {
                throw new NotSupportedException("SecureStream position set");
            }
        }

        public override void Flush() => _stream.Flush();
        private async Task<int> RawReadIntAsync()
        {
            byte[] buffer = await RawReadExactlyAsync(4);
            return BinaryPrimitives.ReadInt32LittleEndian(buffer);
        }
        //private async Task RawWriteIntAsync(int x)
        //{
        //    byte[] buffer = new byte[4];
        //    BinaryPrimitives.WriteInt32LittleEndian(buffer, x);
        //    await _stream.WriteAsync(buffer, 0, buffer.Length);
        //}

        private async Task<byte[]> RawReadExactlyAsync(int length)
        {
            byte[] buffer = new byte[length];
            int received = 0;
            do
            {
                received += await _stream.ReadAsync(buffer, received, length - received);
            } while (received < length);
            return buffer;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count <= 0)
                return 0;
            if (_readCrypto == null)
                throw new NotSupportedException("SecureStream read crypto not inited");
            int received = _stream.Read(buffer, offset, count);
            for (int i = 0; i < received; i++)
            {
                //byte c = (byte)_readCrypto.ReadByte();
                //Console.WriteLine("Read Char: r={0} c={1} o={2}", buffer[offset + i], c, buffer[offset + i] ^= c);
                buffer[offset + i] ^= (byte)_readCrypto.ReadByte();
            }
            return received;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("SecureStream seek");
        }

        public override void SetLength(long value) => _stream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count <= 0)
                return;
            //Console.WriteLine("Write!");
            if (_writeCrypto == null)
                throw new NotSupportedException("SecureStream write crypto not inited");
            byte[] crypto = new byte[count];
            Buffer.BlockCopy(buffer, offset, crypto, 0, count);
            for (int i = 0; i < count; i++)
            {
                //byte c = (byte)_writeCrypto.ReadByte();
                //Console.WriteLine("Write Char: r={0} c={1} o={2}", crypto[i], c, crypto[i] ^= c);
                crypto[i] ^= (byte)_writeCrypto.ReadByte();
            }
            _stream.Write(crypto, 0, crypto.Length);
        }
    }

    class ZeroStream : Stream
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                buffer[offset + i] = 0;
            }

            return count;
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    }

}
