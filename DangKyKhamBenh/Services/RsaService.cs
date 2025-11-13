using System;
using System.Security.Cryptography;
using System.Text;

namespace DangKyKhamBenh.Services
{
    public class RsaService
    {
        private RSA rsa;
        public string PublicKey { get; private set; }
        public string PrivateKey { get; private set; }

        public RsaService()
        {
            rsa = RSA.Create(2048);
            var privateKeyParameters = rsa.ExportParameters(true);
            var publicKeyParameters = rsa.ExportParameters(false);

            PublicKey = Convert.ToBase64String(publicKeyParameters.Modulus);
            PrivateKey = Convert.ToBase64String(privateKeyParameters.D);
        }

        public string Encrypt(string plainText)
        {
            // Đảm bảo Modulus và Exponent được thiết lập đúng
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = Convert.FromBase64String(PublicKey),
                Exponent = new byte[] { 1, 0, 1 } // Exponent mặc định (thường là 65537 trong dạng byte array)
            });

            byte[] data = Encoding.UTF8.GetBytes(plainText);
            byte[] encrypted = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
            return Convert.ToBase64String(encrypted);
        }

        public string Decrypt(string cipherText)
        {
            rsa.ImportParameters(new RSAParameters
            {
                D = Convert.FromBase64String(PrivateKey),
                Modulus = Convert.FromBase64String(PublicKey),
                Exponent = new byte[] { 1, 0, 1 } // Exponent mặc định (thường là 65537 trong dạng byte array)
            });

            byte[] data = Convert.FromBase64String(cipherText);
            byte[] decrypted = rsa.Decrypt(data, RSAEncryptionPadding.OaepSHA256);
            return Encoding.UTF8.GetString(decrypted);
        }
    }

}
