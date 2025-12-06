using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Services
{
    public class SysAdmin
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
        public DataTable GetAllUsers()
        {
            DataTable usersTable = new DataTable();
            try
            {
                using (OracleConnection conn = new OracleConnection(connectionString))
                {
                    conn.Open();
                    using (OracleCommand cmd = new OracleCommand("sys.pkg_PhanQuyen.pro_select_user", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        OracleParameter resultParam = new OracleParameter();
                        resultParam.ParameterName = "@Result";
                        resultParam.OracleDbType = OracleDbType.RefCursor;
                        resultParam.Direction = ParameterDirection.Output;
                        cmd.Parameters.Add(resultParam);
                        cmd.ExecuteNonQuery();
                        if (resultParam.Value != DBNull.Value)
                        {
                            OracleDataReader reader = ((OracleRefCursor)resultParam.Value).GetDataReader();
                            usersTable.Load(reader); 
                        }
                    }
                }
            }
            catch (Exception ex)
            {
           
                Console.WriteLine("Error: " + ex.Message);
            }
            return usersTable;
        }

        // 2. Truy vấn các role mà user đang có
        public DataTable GetUserRoles()
        {
            DataTable rolesTable = new DataTable();
            try
            {
                using (OracleConnection conn = new OracleConnection(connectionString))
                {
                    conn.Open();
                    using (OracleCommand cmd = new OracleCommand("sys.pkg_PhanQuyen.pro_select_roles", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // Thêm tham số đầu ra kiểu RefCursor
                        OracleParameter resultParam = new OracleParameter();
                        resultParam.ParameterName = "@Result";
                        resultParam.OracleDbType = OracleDbType.RefCursor;
                        resultParam.Direction = ParameterDirection.Output;
                        cmd.Parameters.Add(resultParam);
                        cmd.ExecuteNonQuery();
                        if (resultParam.Value != DBNull.Value)
                        {
                            OracleDataReader reader = ((OracleRefCursor)resultParam.Value).GetDataReader();
                            rolesTable.Load(reader); 
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            return rolesTable;
        }

        // 3. Truy vấn các role của một user cụ thể
        public DataTable GetUserRoles(string username)
        {
            DataTable userRolesTable = new DataTable();
            try
            {
                using (OracleConnection conn = new OracleConnection(connectionString))
                {
                    conn.Open();
                    using (OracleCommand cmd = new OracleCommand("sys.pkg_PhanQuyen.pro_user_roles", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add(new OracleParameter("username", OracleDbType.Varchar2)).Value = username;

                        OracleParameter resultParam = new OracleParameter();
                        resultParam.ParameterName = "@Result";
                        resultParam.OracleDbType = OracleDbType.RefCursor;
                        resultParam.Direction = ParameterDirection.Output;
                        cmd.Parameters.Add(resultParam);
                        cmd.ExecuteNonQuery();
                        if (resultParam.Value != DBNull.Value)
                        {
                            OracleDataReader reader = ((OracleRefCursor)resultParam.Value).GetDataReader();
                            userRolesTable.Load(reader); 
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            return userRolesTable;
        }

        // 4. Kiểm tra user có role cụ thể không
        public bool CheckUserRole(string username, string role)
        {
            int count = 0;
            try
            {
                using (OracleConnection conn = new OracleConnection(connectionString))
                {
                    conn.Open();

                    // Tạo command để gọi stored procedure
                    using (OracleCommand cmd = new OracleCommand("sys.pkg_PhanQuyen.pro_user_roles_check", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // Thêm tham số đầu vào
                        cmd.Parameters.Add(new OracleParameter("username", OracleDbType.Varchar2)).Value = username;
                        cmd.Parameters.Add(new OracleParameter("roles", OracleDbType.Varchar2)).Value = role;

                        // Thêm tham số đầu ra
                        OracleParameter resultParam = new OracleParameter("cout", OracleDbType.Int32);
                        resultParam.Direction = ParameterDirection.Output;
                        cmd.Parameters.Add(resultParam);

                        // Thực thi stored procedure
                        cmd.ExecuteNonQuery();

                        // Lấy giá trị trả về từ tham số cout
                        count = Convert.ToInt32(resultParam.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            // Trả về true nếu người dùng có vai trò, ngược lại trả về false
            return count > 0;
        }

    }
}