using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Services
{
    public class HybridService
    {
        private readonly RsaService _rsaService;
        private readonly CaesarCipher _caesarCipher;

        public HybridService()
        {
            _rsaService = new RsaService();
            _caesarCipher = new CaesarCipher();
        }

        // Mã hóa dữ liệu (Caesar Cipher) và khóa Caesar Cipher (RSA)
        public (string EncryptedData, string EncryptedKey) Encrypt(string plainText, int caesarKey)
        {
            // Mã hóa văn bản bằng Caesar Cipher
            string encryptedData = _caesarCipher.Encrypt(plainText, caesarKey);

            // Mã hóa khóa Caesar Cipher bằng RSA
            string encryptedKey = _rsaService.Encrypt(caesarKey.ToString());

            return (encryptedData, encryptedKey);
        }

        // Giải mã dữ liệu (Caesar Cipher) và khóa Caesar Cipher (RSA)
        public string Decrypt(string encryptedData, int caesarKey)
        {
            // Chuyển caesarKey thành string trước khi truyền vào giải mã
            string encryptedKey = caesarKey.ToString();

            // Giải mã khóa Caesar Cipher bằng RSA
            string decryptedKeyString = _rsaService.Decrypt(encryptedKey);  // decryptedKeyString là một chuỗi số
            int key = int.Parse(decryptedKeyString);  // Chuyển chuỗi thành số nguyên (int)

            // Giải mã văn bản bằng Caesar Cipher với khóa đối xứng đã giải mã
            return _caesarCipher.Decrypt(encryptedData, key);
        }

    }
}