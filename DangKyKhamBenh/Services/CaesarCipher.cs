using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Services
{
    public class CaesarCipher
    {
        public string Encrypt(string input, int key)
        {
            string inputString = input?.ToString() ?? string.Empty; 
            string result = "";
            foreach (char c in inputString)
            {
                int charCode = c;
                int encryptedCharCode = charCode + key;
                result += (char)encryptedCharCode;
            }
            return result;
        }

        public string Decrypt(string input, int key)
        {
            string result = "";
            foreach (char c in input)
            {
                int charCode = c;
                int decryptedCharCode = charCode - key;
                result += (char)decryptedCharCode;
            }
            return result;
        }
    }
}