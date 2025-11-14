using System.Security.Cryptography;
using System.Text;

public class KeyDerivationService
{
    private readonly string _secret = "SuperSecretKey123"; // nên đưa vào cấu hình

    public int DeriveCaesarKey(string maBenhNhan)
    {
        using (var sha256 = SHA256.Create())
        {
            var input = Encoding.UTF8.GetBytes(maBenhNhan + _secret);
            var hash = sha256.ComputeHash(input);
            int key = (hash[0] << 8 | hash[1]) % 100 + 1; // key từ 1–100
            return key;
        }
    }
}