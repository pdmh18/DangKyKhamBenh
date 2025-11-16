using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace DangKyKhamBenh.Services
{
    public class HybridService
    {
        private readonly CaesarCipher _caesarCipher;
        private readonly KeyDerivationService _keyDerivation;

        public HybridService()
        {
            _caesarCipher = new CaesarCipher();
            _keyDerivation = new KeyDerivationService();
        }

        public string Encrypt(string plainText, string maBenhNhan)
        {
            int key = _keyDerivation.DeriveCaesarKey(maBenhNhan);
            return _caesarCipher.Encrypt(plainText, key);
        }

        public string Decrypt(string encryptedText, string maBenhNhan)
        {
            int key = _keyDerivation.DeriveCaesarKey(maBenhNhan);
            return _caesarCipher.Decrypt(encryptedText, key);
        }
    }

    //public class HybridService
    //{

    //    private readonly RsaService _rsaService;
    //    private readonly CaesarCipher _caesarCipher;

    //    public HybridService()
    //    {
    //        _rsaService = new RsaService();
    //        _caesarCipher = new CaesarCipher();
    //    }

    //    // Mã hóa dữ liệu (Caesar Cipher) và khóa Caesar Cipher (RSA)
    //    public (string EncryptedData, string EncryptedKey) Encrypt(string plainText, int caesarKey)
    //    {
    //        // Mã hóa văn bản bằng Caesar Cipher
    //        string encryptedData = _caesarCipher.Encrypt(plainText, caesarKey);

    //        // Mã hóa khóa Caesar Cipher bằng RSA
    //        string encryptedKey = _rsaService.Encrypt(caesarKey.ToString());

    //        return (encryptedData, encryptedKey);
    //    }

    //    // Giải mã dữ liệu (Caesar Cipher) và khóa Caesar Cipher (RSA)
    //    public string Decrypt(string encryptedData, string encryptedKey)
    //    {
    //        string decryptedKeyString = _rsaService.Decrypt(encryptedKey);
    //        int key = int.Parse(decryptedKeyString);
    //        return _caesarCipher.Decrypt(encryptedData, key);
    //    }

    //}
    //    public class HybridService
    //    {
    //        private readonly RsaService _rsaService;
    //        private readonly CaesarCipher _caesarCipher;
    //        private readonly KeyDerivationService _keyDerivation;

    //        public HybridService()
    //        {
    //            _rsaService = new RsaService();
    //            _caesarCipher = new CaesarCipher();
    //            _keyDerivation = new KeyDerivationService();
    //        }

    //        public string Encrypt(string plainText, string maBenhNhan)
    //        {
    //            int caesarKey = _keyDerivation.DeriveCaesarKey(maBenhNhan);
    //            return _caesarCipher.Encrypt(plainText, caesarKey);
    //        }

    //        public string Decrypt(string encryptedText, string maBenhNhan)
    //        {
    //            int caesarKey = _keyDerivation.DeriveCaesarKey(maBenhNhan);
    //            return _caesarCipher.Decrypt(encryptedText, caesarKey);
    //        }
    //    }
}