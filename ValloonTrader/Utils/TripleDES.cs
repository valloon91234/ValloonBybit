using System;
using System.IO;
using System.Security.Cryptography;

/**
 * @author Valloon Project
 * @version 1.0 @2020-03-03
 */
namespace Valloon.Utils
{
    public class TripleDES : IDisposable
    {
        private readonly TripleDESCryptoServiceProvider TDES;
        private readonly ICryptoTransform Encryptor;
        private readonly ICryptoTransform Decryptor;

        public TripleDES(byte[] key, bool useEncryption = true, bool useDecryption = true)
        {
            TDES = new TripleDESCryptoServiceProvider
            {
                KeySize = 128,
                Key = key,
                IV = new byte[8],
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7
            };
            if (useEncryption) Encryptor = TDES.CreateEncryptor();
            if (useDecryption) Decryptor = TDES.CreateDecryptor();
        }

        public byte[] Encrypt(byte[] input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, Encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(input, 0, input.Length);
                    cs.Close();
                }
                return ms.ToArray();
            }

            //return Encryptor.TransformFinalBlock(input, 0, input.Length);
        }

        public byte[] Decrypt(byte[] input)
        {
            return Decryptor.TransformFinalBlock(input, 0, input.Length);
        }

        public void Dispose()
        {
            if (Encryptor != null) Encryptor.Dispose();
            if (Decryptor != null) Decryptor.Dispose();
            TDES.Dispose();
        }
    }
}
