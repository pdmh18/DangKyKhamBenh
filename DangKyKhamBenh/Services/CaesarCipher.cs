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
            string inputString = input?.ToString() ?? string.Empty; // Nếu input null, chuyển thành chuỗi rỗng

            string result = "";
            foreach (char c in inputString)
            {
                // Lấy mã Unicode của ký tự
                int charCode = c;

                // Dịch chuyển mã Unicode theo key
                int encryptedCharCode = charCode + key;

                // Chuyển mã Unicode trở lại thành ký tự
                result += (char)encryptedCharCode;
            }

            return result;


        }

        // Giải mã văn bản với khóa
        public string Decrypt(string input, int key)
        {
            string result = "";
            foreach (char c in input)
            {
                // Lấy mã Unicode của ký tự
                int charCode = c;

                // Dịch chuyển mã Unicode ngược lại theo key
                int decryptedCharCode = charCode - key;

                // Chuyển mã Unicode trở lại thành ký tự
                result += (char)decryptedCharCode;
            }
            return result;
        }
    }
}