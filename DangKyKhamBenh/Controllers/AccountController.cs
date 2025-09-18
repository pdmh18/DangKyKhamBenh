
using DangKyKhamBenh.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System.Web.UI.WebControls.WebParts;

namespace DangKyKhamBenh.Controllers
{
    public class AccountController : Controller
    {
        // GET: Account
        [HttpGet, AllowAnonymous]
        public ActionResult Login(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult Login(string user, string password, string returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập Username và Password.";
                return View();
            }

            try
            {
                using (var conn = new OracleConnection(
                    ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString))
                {
                    conn.Open();

                    // Lấy trạng thái + role theo username (so sánh case-insensitive)
                    const string sql = @"
                        SELECT TK_PassWord, NVL(TK_TrangThai,'PENDING') AS TrangThai, TK_Role, TK_StaffType
                        FROM TAIKHOAN
                        WHERE TRIM(UPPER(TK_UserName)) = TRIM(UPPER(:pUser))";

                    using (var cmd = new OracleCommand(sql, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("pUser", user?.Trim());

                        using (var r = cmd.ExecuteReader())
                        {
                            if (!r.Read())
                            {
                                ViewBag.Error = "Sai tài khoản hoặc mật khẩu.";
                                return View();
                            }

                            var dbPassword = r.GetString(0)?.Trim();
                            var status = r.GetString(1)?.Trim().ToUpperInvariant(); // ACTIVE / PENDING / LOCKED ...
                            var role = r.IsDBNull(2) ? null : r.GetString(2);
                            var staffType = r.IsDBNull(3) ? null : r.GetString(3);

                            // So sánh password thô (TODO: hash sau)
                            if (!string.Equals(dbPassword, password?.Trim()))
                            {
                                ViewBag.Error = "Sai tài khoản hoặc mật khẩu.";
                                return View();
                            }

                            if (!string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                            {
                                // Tuỳ biến message theo trạng thái
                                if (status == "PENDING")
                                    ViewBag.Error = "Tài khoản đang chờ duyệt. Vui lòng đợi Admin phê duyệt.";
                                else if (status == "LOCKED")
                                    ViewBag.Error = "Tài khoản đã bị khoá. Vui lòng liên hệ quản trị.";
                                else
                                    ViewBag.Error = $"Tài khoản chưa sẵn sàng (trạng thái: {status}).";

                                return View();
                            }

                            // OK: set session & điều hướng
                            Session["User"] = user?.Trim();
                            Session["Role"] = role;          // ví dụ "ADMIN" / "USER"
                            Session["StaffType"] = staffType;

                            // 1) Nếu có returnUrl -> quay lại
                            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                                return Redirect(returnUrl);

                            // 2) Nếu là ADMIN -> vào Home admin
                            if (string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase))
                                return RedirectToAction("Home", "Admin");

                            // 3) user thường -> Home thường
                            return RedirectToAction("Index", "Home");
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                ViewBag.Error = $"Lỗi Oracle ORA-{ex.Number}: {ex.Message}";
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
            // giữ default nếu bạn cần
            var model = new TaiKhoan { StaffType = "BenhNhan", Role = "USER" };

            // nhận cờ/thông điệp từ TempData (sau khi Redirect)
            ViewBag.PendingOk = TempData["PendingOk"] as bool?;
            ViewBag.PendingMsg = TempData["PendingMsg"] as string;
            ViewBag.PendingPhone = TempData["PendingPhone"] as string;

            return View(model);
            //// nếu muốn default StaffType:
            //var model = new TaiKhoan { StaffType = "BenhNhan", Role = "USER" };
            //return View(model);
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

            // Chỉ cho phép đăng ký Bệnh nhân; Role mặc định USER
            model.StaffType = "BenhNhan";
            model.Role = "USER";

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
                            // 0) Check username đã tồn tại trong TAIKHOAN (đã Active/Pending/Locked gì cũng tính)
                            using (var cmd = new OracleCommand(@"
                        SELECT COUNT(*) FROM TAIKHOAN 
                        WHERE TRIM(UPPER(TK_UserName)) = TRIM(UPPER(:u))", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("u", model.Username?.Trim());
                                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                                {
                                    ModelState.AddModelError("", "Username đã tồn tại trong hệ thống.");
                                    tx.Rollback();
                                    return View(model);
                                }
                            }

                            // 0.1) Check username đã nằm trong hàng chờ PENDING_TAIKHOAN chưa
                            using (var cmd = new OracleCommand(@"
                        SELECT COUNT(*) FROM PENDING_TAIKHOAN
                        WHERE TRIM(UPPER(PT_UserName)) = TRIM(UPPER(:u))", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("u", model.Username?.Trim());
                                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                                {
                                    ModelState.AddModelError("", "Username này đang chờ duyệt, vui lòng đợi Admin phê duyệt.");
                                    tx.Rollback();
                                    return View(model);
                                }
                            }

                            // 1) Tạo mã yêu cầu mới cho bảng PENDING_TAIKHOAN (PT_MaYeuCau)
                            string reqId = NextId(conn, tx, "PENDING_TAIKHOAN", "PT_MaYeuCau", "PT");

                            // 2) Ghi vào bảng chờ duyệt
                            using (var ins = new OracleCommand(@"
                        INSERT INTO PENDING_TAIKHOAN
                        (PT_MaYeuCau, PT_UserName, PT_PassWord, PT_Email, PT_SoDienThoai, PT_NgaySinh, PT_DiaChi, PT_Stafftype, PT_NgayYeuCau)
                        VALUES
                        (:id, :u, :p, :email, :sdt, :dob, :addr, :st, SYSDATE)", conn))
                            {
                                ins.Transaction = tx;
                                ins.BindByName = true;
                                ins.Parameters.Add("id", reqId);
                                ins.Parameters.Add("u", model.Username?.Trim());
                                ins.Parameters.Add("p", model.Password?.Trim()); // TODO: mã hoá mật khẩu sau
                                ins.Parameters.Add("email", (object)email ?? DBNull.Value);
                                ins.Parameters.Add("sdt", (object)phoneNumber ?? DBNull.Value);
                                ins.Parameters.Add("dob", (object)dateOfBirth ?? DBNull.Value);
                                ins.Parameters.Add("addr", (object)address ?? DBNull.Value);
                                ins.Parameters.Add("st", model.StaffType); // luôn 'BenhNhan'
                                ins.ExecuteNonQuery();
                            }

                            tx.Commit();
                            // Gửi thông điệp qua TempData để hiển thị ở Register.cshtml
                            TempData["PendingOk"] = true;
                            TempData["PendingMsg"] = "Tài khoản của bạn đã được gửi để chờ xác minh. Khi xác minh xong chúng tôi sẽ gửi thông báo về số điện thoại bạn đã cung cấp.";
                            TempData["PendingPhone"] = MaskPhone(phoneNumber);

                            // Redirect về GET Register (PRG) để tránh F5 tạo trùng
                            return RedirectToAction("Register");
                        }
                        catch (Exception ex)
                        {
                            tx.Rollback();
                            ModelState.AddModelError("", "Lỗi khi lưu yêu cầu: " + ex.Message);
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
        private static string MaskPhone(string s) // ẩn bớt số
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var digits = new string(s.Where(char.IsDigit).ToArray());
            if (digits.Length <= 4) return s;
            var prefix = digits.Substring(0, Math.Min(3, digits.Length));
            var suffix = digits.Substring(digits.Length - 2);
            return $"{prefix}******{suffix}";
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
            // Xoá thông tin đăng nhập trên Session
            Session.Clear();
            Session.Abandon();

            // (tuỳ chọn) đăng xuất FormsAuthentication nếu có dùng
            try { FormsAuthentication.SignOut(); } catch { }

            // (tuỳ chọn) xoá cookie session cũ
            Response.Cookies.Add(new HttpCookie("ASP.NET_SessionId", "")
            {
                Expires = DateTime.UtcNow.AddDays(-1)
            });

            TempData["Msg"] = "Bạn đã đăng xuất.";
            return RedirectToAction("Login", "Account");
        }

    }
}