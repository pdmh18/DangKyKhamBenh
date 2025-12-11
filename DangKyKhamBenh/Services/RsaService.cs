
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;

public class RsaService
{
    private readonly string _publicKeyPath;
    private readonly string _privateKeyPath;

    public RsaService()
    {
        _publicKeyPath = HttpContext.Current.Server.MapPath("~/App_Data/rsa_public.xml");
        _privateKeyPath = HttpContext.Current.Server.MapPath("~/App_Data/rsa_private.xml");

        EnsureKeysExist(); 
    }

    private void EnsureKeysExist()
    {
        if (!File.Exists(_publicKeyPath) || !File.Exists(_privateKeyPath))
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                string publicXml = rsa.ToXmlString(false);
                string privateXml = rsa.ToXmlString(true);

                File.WriteAllText(_publicKeyPath, publicXml);
                File.WriteAllText(_privateKeyPath, privateXml);
            }
        }
    }

    public string Encrypt(string plainText)
    {
        using (var rsa = new RSACryptoServiceProvider(2048))
        {
            rsa.FromXmlString(File.ReadAllText(_publicKeyPath));
            byte[] data = Encoding.UTF8.GetBytes(plainText);
            byte[] encrypted = rsa.Encrypt(data, false);
            return Convert.ToBase64String(encrypted);
        }
    }

    public string Decrypt(string cipherText)
    {
        using (var rsa = new RSACryptoServiceProvider(2048))
        {
            rsa.FromXmlString(File.ReadAllText(_privateKeyPath));
            byte[] data = Convert.FromBase64String(cipherText);
            byte[] decrypted = rsa.Decrypt(data, false);
            return Encoding.UTF8.GetString(decrypted);
        }
    }
}