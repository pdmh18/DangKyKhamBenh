

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
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;
using System.Net;
using System.Net.Mail;
using DangKyKhamBenh.Services;


namespace DangKyKhamBenh.Controllers
{
    public class AccountController : Controller
    {

        private readonly HybridService _hybridService;

        public AccountController()
        {
            _hybridService = new HybridService();
        }
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
            // 0. Kiểm tra input cơ bản
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

                    // Mã hóa username và hash mật khẩu người dùng nhập vào
                    string encryptedUser = EncryptUser(user?.Trim(), conn);
                    string hashedPassword = HashPassword(password?.Trim(), conn);

                    const string sql = @"
                    SELECT 
                        NVL(tk.TK_TrangThai,'PENDING') AS TrangThai,
                        tk.TK_Role,
                        tk.TK_StaffType,
                        tk.BS_MaBacSi,
                        tk.ND_IdNguoiDung
                    FROM TAIKHOAN tk
                    WHERE tk.TK_UserName = :pUser
                      AND tk.TK_PassWord = :pPass";

                    using (var cmd = new OracleCommand(sql, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("pUser", encryptedUser);  // Mã hóa username
                        cmd.Parameters.Add("pPass", hashedPassword); // Hash mật khẩu

                        using (var r = cmd.ExecuteReader())
                        {
                            // Không tìm thấy tài khoản phù hợp (sai user hoặc pass)
                            if (!r.Read())
                            {
                                ViewBag.Error = "Sai tài khoản hoặc mật khẩu.";
                                return View();
                            }

                            var status = r.GetString(0)?.Trim();        // ACTIVE / PENDING / LOCKED ...
                            var role = r.IsDBNull(1) ? "" : r.GetString(1);   // ADMIN / USER
                            var staffType = r.IsDBNull(2) ? "" : r.GetString(2); // Bác sĩ / Bệnh nhân ...
                            var bsMa = r.IsDBNull(3) ? "" : r.GetString(3);     // mã bác sĩ
                            var ndId = r.IsDBNull(4) ? "" : r.GetString(4);     // ND_IdNguoiDung
                            Session["ND_IdNguoiDung"] = ndId;


                            // Kiểm tra nếu bác sĩ là trưởng khoa
                            string khoaTruongKhoa = null;
                            // string staffType = r.IsDBNull(2) ? "" : r.GetString(2); // Lấy staff type, ví dụ: BacSi, BenhNhan

                            // Kiểm tra nếu bác sĩ là trưởng khoa
                            //if (staffType.Equals("BacSi", StringComparison.OrdinalIgnoreCase))
                            //{
                            //    using (var cmdKhoa = new OracleCommand(@"
                            //    SELECT K_TRUONGKHOA
                            //    FROM KHOA
                            //    WHERE K_TRUONGKHOA = :maBacSi", conn))
                            //    {
                            //        cmdKhoa.Parameters.Add(":maBacSi", bsMa);
                            //        khoaTruongKhoa = cmdKhoa.ExecuteScalar()?.ToString();

                            //        var maKhoa = cmdKhoa.ExecuteScalar()?.ToString();
                            //        Session["IsTruongKhoa"] = !string.IsNullOrEmpty(khoaTruongKhoa);
                            //        // Lưu MaKhoa vào session
                            //        Session["MaKhoa"] = maKhoa;

                            //        // Kiểm tra nếu bác sĩ là trưởng khoa
                            //        using (var cmdTruongKhoa = new OracleCommand(@"
                            //                        SELECT K_TRUONGKHOA
                            //                        FROM KHOA
                            //                        WHERE K_MaKhoa = :maKhoa", conn))
                            //        {
                            //            cmdTruongKhoa.Parameters.Add(":maKhoa", maKhoa);
                            //            khoaTruongKhoa = cmdTruongKhoa.ExecuteScalar()?.ToString();
                            //        }
                            //    }

                            //    // Lưu thông tin trưởng khoa vào session
                            //   // Session["IsTruongKhoa"] = !string.IsNullOrEmpty(khoaTruongKhoa);
                            //}

                            if (staffType.Equals("BacSi", StringComparison.OrdinalIgnoreCase))
                            {
                                using (var cmdKhoa = new OracleCommand(@"
                                        SELECT K_MaKhoa 
                                        FROM KHOA
                                        WHERE K_TRUONGKHOA = :maBacSi", conn))
                                {
                                    cmdKhoa.Parameters.Add(":maBacSi", bsMa);
                                    khoaTruongKhoa = cmdKhoa.ExecuteScalar()?.ToString();
                                    // Lưu MaKhoa vào session nếu cần
                                    if (!string.IsNullOrEmpty(khoaTruongKhoa))
                                    {
                                        var maKhoa = khoaTruongKhoa; // Sử dụng giá trị đã có
                                        Session["MaKhoa"] = maKhoa;

                                        // Tiếp tục kiểm tra nếu bác sĩ là trưởng khoa trong khoa này
                                        using (var cmdTruongKhoa = new OracleCommand(@"
                                            SELECT K_TRUONGKHOA
                                            FROM KHOA
                                            WHERE K_MaKhoa = :maKhoa", conn))
                                        {
                                            cmdTruongKhoa.Parameters.Add(":maKhoa", maKhoa);
                                            var result = cmdTruongKhoa.ExecuteScalar()?.ToString();
                                            Session["IsTruongKhoa"] = !string.IsNullOrEmpty(result); // Đảm bảo session được thiết lập đúng
                                        }
                                    }
                                }
                            }

                            // Lưu thông tin trưởng khoa vào session
                          //  Session["IsTruongKhoa"] = !string.IsNullOrEmpty(khoaTruongKhoa);


                            // Tiếp tục xác thực trạng thái tài khoản và các thông tin khác
                            if (!string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (status.Equals("PENDING", StringComparison.OrdinalIgnoreCase))
                                    ViewBag.Error = "Tài khoản đang chờ duyệt. Vui lòng đợi Admin phê duyệt.";
                                else if (status.Equals("LOCKED", StringComparison.OrdinalIgnoreCase))
                                    ViewBag.Error = "Tài khoản đã bị khoá. Vui lòng liên hệ quản trị.";
                                else
                                    ViewBag.Error = $"Tài khoản chưa sẵn sàng (trạng thái: {status}).";

                                return View();
                            }

                            Session["User"] = user?.Trim();          // lưu plaintext để hiển thị
                            Session["Role"] = role;

                            string maBenhNhan = null;
                            using (var cmdBN = new OracleCommand("SELECT BN_MaBenhNhan FROM BENHNHAN WHERE ND_IdNguoiDung = :ndid", conn))
                            {
                                cmdBN.Parameters.Add(":ndid", ndId);
                                var result = cmdBN.ExecuteScalar();
                                if (result != null)
                                    maBenhNhan = result.ToString();
                            }

                            // ✅ Gán vào session nếu có
                            if (!string.IsNullOrEmpty(maBenhNhan))
                            {
                                Session["MaBenhNhan"] = maBenhNhan;
                            }
                            // ==== SET SESSION ==== 
                            Session["User"] = user?.Trim();          // lưu plaintext để hiển thị
                            Session["TK_Role"] = role;
                            Session["Role"] = role;
                            Session["TK_StaffType"] = staffType;
                            Session["StaffType"] = staffType;
                            Session["StaffTypeRaw"] = staffType;
                            Session["BS_MaBacSi"] = bsMa;
                            Session["ND_IdNguoiDung"] = ndId;
                            Session["TK_TrangThai"] = status;

                            // ==== ĐIỀU HƯỚNG ====
                            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                                return Redirect(returnUrl);

                            if (role.Equals("ADMIN", StringComparison.OrdinalIgnoreCase))
                                return RedirectToAction("Home", "Admin");

                            if (role.Equals("USER", StringComparison.OrdinalIgnoreCase) && IsDoctor(staffType))
                            {
                                if (string.IsNullOrWhiteSpace(bsMa))
                                {
                                    ViewBag.Error = "Tài khoản bác sĩ chưa liên kết BS_MaBacSi. Vui lòng liên hệ quản trị.";
                                    return View();
                                }

                                // Kiểm tra nếu là bác sĩ trưởng khoa
                                if (Convert.ToBoolean(Session["IsTruongKhoa"]))
                                {
                                    return RedirectToAction("DashboardTruongKhoa", "Doctor");
                                }
                                else
                                {
                                    return RedirectToAction("Dashboard", "Doctor");
                                }
                            }

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
            catch (Exception ex)
            {
                ViewBag.Error = "Lỗi: " + ex.Message;
                return View();
            }
        }

        // Hàm mã hóa username bằng AES
        private string EncryptUser(string user, OracleConnection conn)
        {
            const string sql = "SELECT PKG_SECURITY.AES_ENCRYPT_B64(:pUser) FROM DUAL";
            using (var cmd = new OracleCommand(sql, conn))
            {
                cmd.BindByName = true;
                cmd.Parameters.Add("pUser", user);

                return cmd.ExecuteScalar()?.ToString();
            }
        }

        // Hàm hash mật khẩu bằng SHA-256
        private string HashPassword(string password, OracleConnection conn)
        {
            const string sql = "SELECT PKG_SECURITY.HASH_PASSWORD(:pPass) FROM DUAL";
            using (var cmd = new OracleCommand(sql, conn))
            {
                cmd.BindByName = true;
                cmd.Parameters.Add("pPass", password);

                return cmd.ExecuteScalar()?.ToString();
            }
        }



        // Nhận diện "Bác sĩ" / "Doctor" (khử dấu + chỉ giữ ký tự chữ)
        private static bool IsDoctor(string staffType)
        {
            if (string.IsNullOrWhiteSpace(staffType)) return false;
            var s = RemoveDiacritics(staffType).ToUpperInvariant();
            s = new string(s.Where(char.IsLetter).ToArray());
            return s == "BACSI" || s == "DOCTOR";
        }

        private static string RemoveDiacritics(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var n = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in n)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
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
        }
        // GET: Account/Register
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult Register(TaiKhoan model)
        {
            // 1. Validate model (DataAnnotations)
            if (!ModelState.IsValid)
                return View(model);

            // 2. Check username / password không trống
            if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError("", "Vui lòng nhập Username và Password.");
                return View(model);
            }

            // 3. Check ConfirmPassword
            if (!string.Equals(model.Password?.Trim(), model.ConfirmPassword?.Trim()))
            {
                ModelState.AddModelError("ConfirmPassword", "Mật khẩu và xác nhận mật khẩu không khớp.");
                return View(model);
            }

            // 4. Check tuổi >= 15
            if (model.DateOfBirth.HasValue)
            {
                int age = DateTime.Now.Year - model.DateOfBirth.Value.Year;
                if (model.DateOfBirth.Value.Date > DateTime.Now.AddYears(-age)) age--;
                if (age < 15)
                {
                    ModelState.AddModelError("DateOfBirth", "Bạn phải từ 15 tuổi trở lên.");
                    return View(model);
                }
            }

            try
            {
                var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"]?.ConnectionString;
                using (var conn = new OracleConnection(cs))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        try
                        {
                            // ========== B1: Check trùng username (theo ciphertext) ==========
                            using (var cmdCheck = new OracleCommand(@"
                        SELECT COUNT(*) 
                        FROM TAIKHOAN 
                        WHERE TK_UserName = PKG_SECURITY.AES_ENCRYPT_B64(:u)", conn)) // *** ĐÃ ĐỔI ***
                            {
                                cmdCheck.Transaction = tx;
                                cmdCheck.BindByName = true;
                                cmdCheck.Parameters.Add("u", model.Username?.Trim());

                                if (Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0)
                                {
                                    ModelState.AddModelError("Username", "Username đã tồn tại trong hệ thống.");
                                    tx.Rollback();
                                    return View(model);
                                }
                            }
                            var staffType = string.IsNullOrEmpty(model.StaffType) ? "BenhNhan" : model.StaffType;
                            bool isDoctor = string.Equals(staffType, "BacSi", StringComparison.OrdinalIgnoreCase);

                            // ========== B2: Sinh mã ==========
                            string ndId = NextId(conn, tx, "NGUOIDUNG", "ND_IdNguoiDung", "ND");
                            string bnId = NextId(conn, tx, "BENHNHAN", "BN_MaBenhNhan", "BN");
                            //string bsId = NextId(conn, tx, "BACSI", "BS_MaBacSi", "BS");
                            string tkId = NextId(conn, tx, "TAIKHOAN", "TK_MaTK", "TK");

                            // ====== Mã hoá EMAIL bằng HybridService, key dựa trên BN_MaBenhNhan ======
                            string emailPlain = string.IsNullOrWhiteSpace(model.Email)
                                                    ? null
                                                    : model.Email.Trim();

                            string encEmail = emailPlain == null
                                ? null
                                : _hybridService.Encrypt(emailPlain, bnId);


                            // ========== B3: Insert NGUOIDUNG (mã hoá Email / SĐT / Địa chỉ) ==========
                            using (var cmdNd = new OracleCommand(@"
                                INSERT INTO NGUOIDUNG
                                    (ND_IdNguoiDung,
                                     ND_HoTen,
                                     ND_SoDienThoai,
                                     ND_Email,
                                     ND_NgaySinh,
                                     ND_DiaChiThuongChu)
                                VALUES
                                    (:id,
                                     :hoten,
                                     PKG_SECURITY.RSA_ENCRYPT_B64(:sdt),      -- GIỮ NGUYÊN
                                     :email,                                  -- EMAIL ĐÃ MÃ HOÁ SẴN BẰNG HybridService
                                     :ns,
                                     PKG_SECURITY.AES_ENCRYPT_B64(:diachi)    -- GIỮ NGUYÊN
                                    )", conn))
                            {
                                cmdNd.Transaction = tx;
                                cmdNd.BindByName = true;

                                cmdNd.Parameters.Add("id", ndId);
                                cmdNd.Parameters.Add("hoten", DBNull.Value);

                                cmdNd.Parameters.Add("sdt",
                                    string.IsNullOrWhiteSpace(model.PhoneNumber)
                                        ? (object)DBNull.Value
                                        : model.PhoneNumber.Trim());

                                // dùng ciphertext từ HybridService
                                cmdNd.Parameters.Add("email",
                                    (object)encEmail ?? DBNull.Value);

                                cmdNd.Parameters.Add("ns",
                                    model.DateOfBirth.HasValue
                                        ? (object)model.DateOfBirth.Value.Date
                                        : DBNull.Value);

                                cmdNd.Parameters.Add("diachi",
                                    string.IsNullOrWhiteSpace(model.Address)
                                        ? (object)DBNull.Value
                                        : model.Address.Trim());

                                cmdNd.ExecuteNonQuery();
                            }


                            // ========== B4: Insert BENHNHAN ==========
                            using (var cmdBn = new OracleCommand(@"
                        INSERT INTO BENHNHAN
                            (BN_MaBenhNhan, BN_SoBaoHiemYT, BN_NhomMau, BN_TieuSuBenhAn, ND_IdNguoiDung)
                        VALUES
                            (:bn, NULL, NULL, NULL, :nd)", conn))
                            {
                                cmdBn.Transaction = tx;
                                cmdBn.BindByName = true;
                                cmdBn.Parameters.Add("bn", bnId);
                                cmdBn.Parameters.Add("nd", ndId);
                                cmdBn.ExecuteNonQuery();
                            }

                            // ========== B6: Insert TAIKHOAN (mã hoá username + hash password) ==========
                            using (var cmdTk = new OracleCommand(@"
                        INSERT INTO TAIKHOAN
                            (TK_MaTK, TK_UserName, TK_PassWord, TK_Role, TK_TrangThai, TK_StaffType,
                             BN_MaBenhNhan, BS_MaBacSi, ND_IdNguoiDung, TK_NgayTao)
                        VALUES
                            (:tkId,
                             PKG_SECURITY.AES_ENCRYPT_B64(:u),       -- *** ĐÃ ĐỔI: username mã hoá đối xứng
                             PKG_SECURITY.HASH_PASSWORD(:p),         -- *** ĐÃ ĐỔI: password hash
                             :role,
                             :status,
                             :st,
                             :bn,
                             :bs,
                             :nd,
                            SYSDATE)", conn))
                            {
                                cmdTk.Transaction = tx;
                                cmdTk.BindByName = true;

                                cmdTk.Parameters.Add("tkId", tkId);
                                cmdTk.Parameters.Add("u", model.Username?.Trim());
                                cmdTk.Parameters.Add("p", model.Password?.Trim());

                                cmdTk.Parameters.Add("role",
                                    string.IsNullOrEmpty(model.Role) ? "USER" : model.Role);

                                cmdTk.Parameters.Add("status", "ACTIVE");
                                cmdTk.Parameters.Add("st",
                                    string.IsNullOrEmpty(model.StaffType) ? "BenhNhan" : model.StaffType);

                                cmdTk.Parameters.Add("bn", bnId);
                                cmdTk.Parameters.Add("bs", DBNull.Value);
                                cmdTk.Parameters.Add("nd", ndId);

                                cmdTk.ExecuteNonQuery();
                            }

                            tx.Commit();
                            TempData["Msg"] = "Tài khoản của bạn đã được tạo thành công.";
                            return RedirectToAction("Login");
                        }
                        catch (Exception ex)
                        {
                            tx.Rollback();
                            ModelState.AddModelError("", "Lỗi khi lưu tài khoản: " + ex.Message);
                            return View(model);
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                ModelState.AddModelError("", $"Lỗi Oracle ORA-{ex.Number}: {ex.Message}");
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi: " + ex.Message);
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


        [HttpGet, AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            // Trả về model rỗng để view binding
            return View(new ForgotPasswordOtpViewModel());
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(ForgotPasswordOtpViewModel model)
        {
            // Xác định đang ở bước nào: nhập OTP hay chưa
            bool isVerifyPhase = !string.IsNullOrWhiteSpace(model.Otp);

            // Validate cơ bản: luôn cần Username + Email
            if (string.IsNullOrWhiteSpace(model.UserName))
                ModelState.AddModelError("UserName", "Vui lòng nhập tài khoản.");

            if (string.IsNullOrWhiteSpace(model.Email))
                ModelState.AddModelError("Email", "Vui lòng nhập email đã đăng ký.");

            // Nếu đang ở bước xác nhận OTP → bắt buộc mật khẩu mới + confirm
            if (isVerifyPhase)
            {
                if (string.IsNullOrWhiteSpace(model.NewPassword))
                    ModelState.AddModelError("NewPassword", "Vui lòng nhập mật khẩu mới.");

                if (string.IsNullOrWhiteSpace(model.ConfirmPassword))
                    ModelState.AddModelError("ConfirmPassword", "Vui lòng xác nhận mật khẩu mới.");

                if (!string.Equals(model.NewPassword?.Trim(), model.ConfirmPassword?.Trim()))
                    ModelState.AddModelError("ConfirmPassword", "Mật khẩu xác nhận không khớp.");
            }

            if (!ModelState.IsValid)
            {
                // Nếu đã gửi OTP rồi thì vẫn hiện ô OTP
                model.OtpSent = isVerifyPhase || model.OtpSent;
                return View(model);
            }

            string N(string s) => (s ?? "").Trim();

            try
            {
                var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
                using (var conn = new OracleConnection(cs))
                {
                    conn.Open();

                    // Mã hoá username để so khớp với TK_UserName đã lưu (AES_ENCRYPT_B64)
                    string encryptedUser = EncryptUser(N(model.UserName), conn);

                    // Lấy TK_MaTK + email đã mã hoá + BN_MaBenhNhan + BS_MaBacSi + StaffType
                    const string sqlFind = @"
                SELECT tk.TK_MaTK,
                       nd.ND_Email,
                       tk.BN_MaBenhNhan,
                       tk.BS_MaBacSi,
                       tk.TK_StaffType
                FROM   TAIKHOAN tk
                JOIN   NGUOIDUNG nd
                       ON nd.ND_IdNguoiDung = tk.ND_IdNguoiDung
                WHERE  tk.TK_UserName = :pUser";

                    string tkId = null;
                    string encEmailDb = null;
                    string bnId = null;
                    string bsId = null;
                    string staffTypeDb = null;

                    using (var cmd = new OracleCommand(sqlFind, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("pUser", encryptedUser);

                        using (var r = cmd.ExecuteReader())
                        {
                            if (!r.Read())
                            {
                                ViewBag.Error = "Không tìm thấy tài khoản với Username và Email này.";
                                model.OtpSent = false;
                                return View(model);
                            }

                            tkId = r.GetString(0);
                            encEmailDb = r.IsDBNull(1) ? null : r.GetString(1);
                            bnId = r.IsDBNull(2) ? null : r.GetString(2);
                            bsId = r.IsDBNull(3) ? null : r.GetString(3);
                            staffTypeDb = r.IsDBNull(4) ? null : r.GetString(4);
                        }
                    }

                    // Chọn key để giải mã email: BN_MaBenhNhan (bệnh nhân) hoặc BS_MaBacSi (bác sĩ)
                    string keyForEmail = null;
                    if (!string.IsNullOrWhiteSpace(staffTypeDb) && IsDoctor(staffTypeDb))
                    {
                        // Tài khoản bác sĩ → email mã hoá bằng BS_MaBacSi
                        keyForEmail = bsId;
                    }
                    else
                    {
                        // Tài khoản bệnh nhân (hoặc mặc định) → email mã hoá bằng BN_MaBenhNhan
                        keyForEmail = bnId;
                    }

                    // Giải mã email bằng HybridService
                    string emailFromDbPlain = null;
                    try
                    {
                        if (!string.IsNullOrEmpty(encEmailDb) && !string.IsNullOrEmpty(keyForEmail))
                        {
                            emailFromDbPlain = _hybridService.Decrypt(encEmailDb, keyForEmail);
                        }
                    }
                    catch (Exception exDec)
                    {
                        ViewBag.Error = "Lỗi giải mã email trong hệ thống: " + exDec.Message;
                        model.OtpSent = false;
                        return View(model);
                    }

                    // So khớp email người dùng nhập
                    if (!string.Equals(emailFromDbPlain ?? "", N(model.Email), StringComparison.OrdinalIgnoreCase))
                    {
                        ViewBag.Error = "Không tìm thấy tài khoản với Username và Email này.";
                        model.OtpSent = false;
                        return View(model);
                    }

                    // ================= BƯỚC 1: GỬI OTP =================
                    if (!isVerifyPhase)
                    {
                        string otp = GenerateOtpCode(6); // ví dụ 6 chữ số
                        DateTime expire = DateTime.UtcNow.AddMinutes(5);

                        // Lưu OTP vào Session theo TK_MaTK
                        Session["FP_OTP_" + tkId] = otp;
                        Session["FP_OTP_EXP_" + tkId] = expire;

                        // Gửi email OTP
                        SendOtpEmail(N(model.Email), N(model.UserName), otp);

                        ViewBag.Info = "Đã gửi mã OTP đến email của bạn. Vui lòng kiểm tra hộp thư và nhập OTP để xác nhận đổi mật khẩu.";
                        model.OtpSent = true;  // Để view hiện ô nhập OTP

                        // KHÔNG đổi mật khẩu ở bước này
                        return View(model);
                    }

                    // ================= BƯỚC 2: XÁC THỰC OTP + ĐỔI MẬT KHẨU =================

                    string key = "FP_OTP_" + tkId;
                    string expKey = "FP_OTP_EXP_" + tkId;

                    var otpInSession = Session[key] as string;
                    DateTime? exp = null;
                    if (Session[expKey] != null)
                        exp = (DateTime)Session[expKey];

                    if (string.IsNullOrEmpty(otpInSession) || !exp.HasValue)
                    {
                        ViewBag.Error = "Mã OTP không tồn tại hoặc đã hết hạn. Vui lòng yêu cầu gửi lại.";
                        model.OtpSent = true;
                        return View(model);
                    }

                    if (DateTime.UtcNow > exp.Value)
                    {
                        ViewBag.Error = "Mã OTP đã hết hạn. Vui lòng yêu cầu gửi lại.";
                        model.OtpSent = true;
                        return View(model);
                    }

                    if (!string.Equals(N(model.Otp), otpInSession))
                    {
                        ViewBag.Error = "Mã OTP không chính xác.";
                        model.OtpSent = true;
                        return View(model);
                    }

                    // OTP hợp lệ → Hash mật khẩu mới và cập nhật DB
                    string hashedPassword = HashPassword(N(model.NewPassword), conn);

                    const string sqlUpdate = @"
                UPDATE TAIKHOAN
                SET    TK_PassWord = :pPass
                WHERE  TK_MaTK = :pId";

                    using (var up = new OracleCommand(sqlUpdate, conn))
                    {
                        up.BindByName = true;
                        up.Parameters.Add("pPass", hashedPassword);
                        up.Parameters.Add("pId", tkId);
                        up.ExecuteNonQuery();
                    }

                    // Xoá OTP khỏi Session sau khi dùng xong
                    Session.Remove(key);
                    Session.Remove(expKey);

                    TempData["Msg"] = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại.";
                    return RedirectToAction("Login");
                }
            }
            catch (OracleException ex)
            {
                ViewBag.Error = $"Lỗi Oracle ORA-{ex.Number}: {ex.Message}";
                model.OtpSent = true;
                return View(model);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Lỗi: " + ex.Message;
                model.OtpSent = true;
                return View(model);
            }
        }


        // ================== HÀM HỖ TRỢ OTP & EMAIL ==================

        private string GenerateOtpCode(int length)
        {
            var rnd = new Random();
            var sb = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                sb.Append(rnd.Next(0, 10)); // chỉ 0-9 => mã số
            }
            return sb.ToString();
        }

        private void SendOtpEmail(string toEmail, string userName, string otp)
        {
            // Đọc cấu hình SMTP từ web.config
            string smtpUser = ConfigurationManager.AppSettings["SmtpUser"];
            string smtpPass = ConfigurationManager.AppSettings["SmtpPass"];

            var msg = new MailMessage();
            msg.To.Add(new MailAddress(toEmail));
            msg.From = new MailAddress(smtpUser, "UMC CARE");
            msg.Subject = "Mã OTP đổi mật khẩu tài khoản UMC CARE";
            msg.Body = $@"
                        Chào {userName},

                        Bạn vừa yêu cầu đổi mật khẩu cho tài khoản trên hệ thống UMC CARE.

                        Mã OTP của bạn là: {otp}

                        Mã này có hiệu lực trong 5 phút. Vui lòng không chia sẻ mã cho bất kỳ ai.

                        Trân trọng,
                        UMC CARE";
            msg.IsBodyHtml = false;

            using (var client = new SmtpClient())
            {
                client.Host = "smtp.gmail.com";
                client.Port = 587;
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(smtpUser, smtpPass);
                client.Send(msg);
            }
        }

    }

}