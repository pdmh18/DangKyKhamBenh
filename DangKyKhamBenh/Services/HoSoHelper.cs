using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Services
{
    public class HoSoHelper
    {
        public static bool HasHoSo(string ndId)
        {
            if (string.IsNullOrEmpty(ndId))
                return false;

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                var sql = "SELECT COUNT(*) FROM BENHNHAN WHERE ND_IdNguoiDung = :ndid";
                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.Parameters.Add("ndid", ndId);
                    var count = Convert.ToInt64(cmd.ExecuteScalar());
                    return count > 0;
                }
            }
        }

        public static string GetMaBenhNhan(string ndId)
        {
            if (string.IsNullOrEmpty(ndId))
                return null;

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                var sql = "SELECT BN_MaBenhNhan FROM BENHNHAN WHERE ND_IdNguoiDung = :ndid";
                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.Parameters.Add("ndid", ndId);
                    var result = cmd.ExecuteScalar();
                    return result?.ToString();
                }
            }
        }

    }
}