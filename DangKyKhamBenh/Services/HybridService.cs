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

    
}