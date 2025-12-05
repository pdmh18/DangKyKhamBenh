using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Services
{
    public class MaHoa_GiaiMa_Sql
    {
        public string EncryptUser(string user, OracleConnection conn)
        {
            const string sql = "SELECT PKG_SECURITY.AES_ENCRYPT_B64(:pUser) FROM DUAL";
            using (var cmd = new OracleCommand(sql, conn))
            {
                cmd.BindByName = true;
                cmd.Parameters.Add("pUser", user);

                return cmd.ExecuteScalar()?.ToString();
            }
        }
        public string DecryptUser(string encryptedUser, OracleConnection conn)
        {
            const string sql = "SELECT PKG_SECURITY.AES_DECRYPT_B64(:pEncryptedUser) FROM DUAL";
            using (var cmd = new OracleCommand(sql, conn))
            {
                cmd.BindByName = true;
                cmd.Parameters.Add("pEncryptedUser", encryptedUser);

                return cmd.ExecuteScalar()?.ToString();
            }
        }

        // Hàm hash mật khẩu bằng SHA-256
        public string HashPassword(string password, OracleConnection conn)
        {
            const string sql = "SELECT PKG_SECURITY.HASH_PASSWORD(:pPass) FROM DUAL";
            using (var cmd = new OracleCommand(sql, conn))
            {
                cmd.BindByName = true;
                cmd.Parameters.Add("pPass", password);

                return cmd.ExecuteScalar()?.ToString();
            }
        }
    }
}