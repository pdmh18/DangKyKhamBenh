using System.IO;
using System.Security.Cryptography;

public class RsaKeyManager
{
    public static void GenerateKeys(string publicPath, string privatePath)
    {
        using (var rsa = new RSACryptoServiceProvider(2048))
        {
            string publicXml = rsa.ToXmlString(false); // public key
            string privateXml = rsa.ToXmlString(true); // private key

            File.WriteAllText(publicPath, publicXml);
            File.WriteAllText(privatePath, privateXml);
        }
    }
}