
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls.WebParts;
using DangKyKhamBenh.Models;

namespace DangKyKhamBenh.Controllers
{
    public class AccountController : Controller
    {
        // GET: Account
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(string user, string password)
        {
            bool isValid = false;
      
            using (var conn = new OracleConnection(
                ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString))
            {
                conn.Open();
                string sql = @"SELECT COUNT(*) 
                               FROM TAIKHOAN 
                               WHERE TRIM(UPPER(TK_UserName)) = TRIM(UPPER(:pUser)) 
                                 AND TRIM(TK_PassWord) = TRIM(:pPassword)";


                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add(new OracleParameter("pUser", user?.Trim()));
                    cmd.Parameters.Add(new OracleParameter("pPassword", password?.Trim()));

                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    isValid = (count > 0);
                }
            }

            if (isValid)
            {
                Session["User"] = user;
                return RedirectToAction("Index", "Home");
            }
            else
            {
                ViewBag.Error = $"Invalid username or password. (User={user}, Pass={password})";
                return View();
            }
        }



        [HttpGet]
        public ActionResult TestConnection()
        {
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"]?.ConnectionString;
            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                using (var cmd = new OracleCommand(
                    "SELECT USER, SYS_CONTEXT('USERENV','DB_NAME') FROM dual", conn))
                using (var r = cmd.ExecuteReader())
                {
                    r.Read();
                    return Content($"USER={r.GetString(0)}, DB={r.GetString(1)}");
                }
            }
        }

        private static string BuildId(string prefix, decimal nextVal)
            => prefix + nextVal.ToString("00000000");

        private static string MapRole(string staffType)
            => string.Equals(staffType, "BacSi", StringComparison.OrdinalIgnoreCase) ? "DOCTOR" : "USER";

        // GET: Account/Register
        [HttpGet, AllowAnonymous]
        public ActionResult Register()
        {
            // nếu muốn default StaffType:
            var model = new TaiKhoan { StaffType = "BenhNhan", Role = "USER" };
            return View(model);
        }

        // POST: Account/Register
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult Register(TaiKhoan model)
        {
            // Lấy các field KHÔNG có trong TaiKhoan từ form
            var confirmPassword = Request.Form["ConfirmPassword"];
            var email = Request.Form["Email"];
            var phoneNumber = Request.Form["PhoneNumber"];
            var address = Request.Form["Address"];
            DateTime? dateOfBirth = null;
            if (DateTime.TryParse(Request.Form["DateOfBirth"], out var dobVal)) dateOfBirth = dobVal;

            // Validate cơ bản
            if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError("", "Vui lòng nhập Username và Password.");
                return View(model);
            }
            if (!string.Equals(model.Password?.Trim(), confirmPassword?.Trim()))
            {
                ModelState.AddModelError("", "Mật khẩu xác nhận không khớp.");
                return View(model);
            }
            if (string.IsNullOrEmpty(model.StaffType)) model.StaffType = "BenhNhan";
            if (string.IsNullOrEmpty(model.Role)) model.Role = MapRole(model.StaffType);

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"]?.ConnectionString;

            try
            {
                using (var conn = new OracleConnection(cs))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        try
                        {
                            // 0) Check username đã tồn tại
                            using (var checkCmd = new OracleCommand(@"
                                SELECT COUNT(*) FROM TAIKHOAN 
                                WHERE TRIM(UPPER(TK_UserName)) = TRIM(UPPER(:u))", conn))
                            {
                                checkCmd.Transaction = tx;
                                checkCmd.BindByName = true;
                                checkCmd.Parameters.Add("u", model.Username?.Trim());
                                if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                                {
                                    ModelState.AddModelError("", "Username đã tồn tại.");
                                    tx.Rollback();
                                    return View(model);
                                }
                            }

                            // 1) NGUOIDUNG
                            string ndId = NextId(conn, tx, "NGUOIDUNG", "ND_IdNguoiDung", "ND");
                            using (var cmd = new OracleCommand(@"
                                INSERT INTO NGUOIDUNG
                                (ND_IdNguoiDung, ND_HoTen, ND_SoDienThoai, ND_Email, ND_NgaySinh, ND_DiaChiThuongChu)
                                VALUES (:id, :hoten, :sdt, :email, :dob, :addr)", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", ndId);
                                cmd.Parameters.Add("hoten", (object)model.Username ?? DBNull.Value);
                                cmd.Parameters.Add("sdt", (object)phoneNumber ?? DBNull.Value);
                                cmd.Parameters.Add("email", (object)email ?? DBNull.Value);
                                cmd.Parameters.Add("dob", (object)dateOfBirth ?? DBNull.Value);
                                cmd.Parameters.Add("addr", (object)address ?? DBNull.Value);
                                cmd.ExecuteNonQuery();
                            }

                            // 2) BENHNHAN hoặc BACSI
                            string bnId = null, bsId = null;
                            bool isBacSi = string.Equals(model.StaffType, "BacSi", StringComparison.OrdinalIgnoreCase);

                            if (isBacSi)
                            {
                                bsId = NextId(conn, tx, "BACSI", "BS_MaBacSi", "BS");
                                using (var cmd = new OracleCommand(@"
                                    INSERT INTO BACSI (BS_MaBacSi, BS_ChuyenKhoa, BS_ChucDanh, BS_NamKinhNghiem, ND_IdNguoiDung)
                                    VALUES (:id, :ck, :cd, :nam, :nd)", conn))
                                {
                                    cmd.Transaction = tx;
                                    cmd.BindByName = true;
                                    cmd.Parameters.Add("id", bsId);
                                    cmd.Parameters.Add("ck", "Tổng quát");
                                    cmd.Parameters.Add("cd", "BS");
                                    cmd.Parameters.Add("nam", 0);
                                    cmd.Parameters.Add("nd", ndId);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            else
                            {
                                bnId = NextId(conn, tx, "BENHNHAN", "BN_MaBenhNhan", "BN");
                                using (var cmd = new OracleCommand(@"
                                    INSERT INTO BENHNHAN (BN_MaBenhNhan, BN_SoBaoHiemYT, BN_NhomMau, BN_TieuSuBenhAn, ND_IdNguoiDung)
                                    VALUES (:id, :bh, :nhom, :tieuSu, :nd)", conn))
                                {
                                    cmd.Transaction = tx;
                                    cmd.BindByName = true;
                                    cmd.Parameters.Add("id", bnId);
                                    cmd.Parameters.Add("bh", DBNull.Value);
                                    cmd.Parameters.Add("nhom", DBNull.Value);
                                    cmd.Parameters.Add("tieuSu", DBNull.Value);
                                    cmd.Parameters.Add("nd", ndId);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            // 3) TAIKHOAN
                            string tkId = NextId(conn, tx, "TAIKHOAN", "TK_MaTK", "TK");
                            using (var cmd = new OracleCommand(@"
                                INSERT INTO TAIKHOAN
                                (TK_MaTK, TK_UserName, TK_PassWord, TK_Role, TK_TrangThai, TK_StaffType,
                                 BN_MaBenhNhan, BS_MaBacSi, ND_IdNguoiDung)
                                VALUES
                                (:tk, :u, :p, :r, :tt, :st, :bn, :bs, :nd)", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("tk", tkId);
                                cmd.Parameters.Add("u", model.Username?.Trim());
                                cmd.Parameters.Add("p", model.Password?.Trim());   // TODO: hash mật khẩu
                                cmd.Parameters.Add("r", MapRole(model.StaffType));
                                cmd.Parameters.Add("tt", "Active");
                                cmd.Parameters.Add("st", isBacSi ? (object)@"Bác sĩ" : @"Bệnh nhân");
                                cmd.Parameters.Add("bn", (object)bnId ?? DBNull.Value);
                                cmd.Parameters.Add("bs", (object)bsId ?? DBNull.Value);
                                cmd.Parameters.Add("nd", ndId);
                                cmd.ExecuteNonQuery();
                            }

                            tx.Commit();
                            TempData["Success"] = "Đăng ký thành công, vui lòng đăng nhập.";
                            return RedirectToAction("Login");
                        }
                        catch (Exception ex)
                        {
                            tx.Rollback();
                            ModelState.AddModelError("", "Lỗi đăng ký: " + ex.Message);
                            return View(model);
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                ModelState.AddModelError("", $"Kết nối Oracle lỗi ORA-{ex.Number}: {ex.Message}");
                return View(model);
            }
        }

        // Sinh mã kế tiếp
        private static string NextId(
            OracleConnection conn, OracleTransaction tx,
            string table, string idColumn, string prefix)
        {
            using (var lockCmd = new OracleCommand($"LOCK TABLE {table} IN EXCLUSIVE MODE", conn))
            {
                lockCmd.Transaction = tx;
                lockCmd.ExecuteNonQuery();
            }

            var sqlMax = $@"
                SELECT NVL(MAX(TO_NUMBER(SUBSTR({idColumn}, -8))), 0)
                FROM {table}";
            decimal maxTail;
            using (var cmd = new OracleCommand(sqlMax, conn))
            {
                cmd.Transaction = tx;
                var ret = cmd.ExecuteScalar();
                maxTail = Convert.ToDecimal(ret);
            }

            var next = maxTail + 1;
            return prefix + next.ToString("00000000");
        }






        public ActionResult Logout()
        {
            return View();
        }

    }
}