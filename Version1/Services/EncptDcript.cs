using System.Security.Cryptography;
using System.Text;

namespace SQCScanner.Services
{
    public class EncptDcript
    {
        private static readonly string key = "&Ab@315100cnn@1#";
        public string Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.IV = new byte[16];
            var encryptor = aes.CreateEncryptor();
            var inputBytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
            return Convert.ToBase64String(encrypted);
        }

        public string Decrypt(string encryptedText)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.IV = new byte[16];
            var decryptor = aes.CreateDecryptor();
            var inputBytes = Convert.FromBase64String(encryptedText);
            var decrypted = decryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
            return Encoding.UTF8.GetString(decrypted);
        }
    }
}