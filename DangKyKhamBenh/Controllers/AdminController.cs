using DangKyKhamBenh.Filters;                       // Dùng attribute [AdminOnly] để khóa controller cho ADMIN
using DangKyKhamBenh.Models;
using DangKyKhamBenh.Models.ViewModels;
using DangKyKhamBenh.Services;
using Oracle.ManagedDataAccess.Client;              
using System;
using System.Collections.Generic;
using System.Configuration;                         
using System.Data;
using System.Linq;
using System.Reflection;
using System.Web.Mvc;
using System.Web.Razor.Tokenizer.Symbols;
using static System.Collections.Specialized.BitVector32;


namespace DangKyKhamBenh.Controllers
{
    [AdminOnly]   // Chỉ ADMIN (Session["Role"] == "ADMIN") mới vào được toàn bộ controller này
    public class AdminController : Controller
    {
        private readonly CaesarCipher _caesarCipher;
        private readonly RsaService _rsaService;
        private readonly HybridService _hybridService;
        private readonly MaHoa_GiaiMa_Sql _maHoa_GiaiMa_Sql;

        public AdminController()
        {
            _caesarCipher = new CaesarCipher();
            _rsaService = new RsaService();
            _hybridService = new HybridService();
            _maHoa_GiaiMa_Sql = new MaHoa_GiaiMa_Sql();
        }


        [HttpGet]
        public ActionResult Home()
        {
            ViewBag.Active = "Home";
            return View();
        }


        // ====== LỊCH SỬ ĐĂNG KÝ ======
        [AdminOnly]
        public ActionResult History(DateTime? from = null, DateTime? to = null, string keyword = null, string sortBy = "date", string sortDir = "desc")
        {
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            var tb = new System.Data.DataTable();

            string orderClause = "ORDER BY ";
            if (sortBy == "username")
                orderClause += $"PKG_SECURITY.AES_DECRYPT_B64(tk.TK_UserName) {sortDir}";
            else
                orderClause += $"tk.TK_NgayTao {sortDir}";


            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                var sql = $@"
            SELECT
                    tk.TK_MaTK                              AS TK_MaTK,
                    PKG_SECURITY.AES_DECRYPT_B64(tk.TK_UserName) AS TK_UserName,
                    tk.TK_StaffType                         AS TK_StaffType,
                    tk.TK_Role                              AS TK_Role,
                    tk.TK_TrangThai                         AS TrangThai,
                    tk.TK_NgayTao                           AS TK_NgayTao
            FROM    TAIKHOAN tk
            WHERE (:d1 IS NULL OR tk.TK_NgayTao >= :d1)
              AND (:d2 IS NULL OR tk.TK_NgayTao <  :d2 + 1)
              AND (
                    :kw IS NULL 
                 OR PKG_SECURITY.AES_DECRYPT_B64(tk.TK_UserName) LIKE '%' || :kw || '%'
                 OR tk.TK_MaTK LIKE '%' || :kw || '%'
              )
            {orderClause}";

                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add("d1", OracleDbType.Date).Value = (object)from ?? DBNull.Value;
                    cmd.Parameters.Add("d2", OracleDbType.Date).Value = (object)to ?? DBNull.Value;
                    cmd.Parameters.Add("kw", OracleDbType.Varchar2).Value = (object)keyword ?? DBNull.Value;

                    using (var r = cmd.ExecuteReader())
                        tb.Load(r);
                }
            }
            ViewBag.SortBy = sortBy;
            ViewBag.SortDir = sortDir == "asc" ? "desc" : "asc";

            return View(tb);
        }



        // ====== E) TẠO TÀI KHOẢN BÁC SĨ (ADMIN TỰ TẠO) ======
        private void LoadKhoaDropDown(string selected = null)
        {
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            var list = new List<SelectListItem>();

            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                using (var cmd = new OracleCommand(
                    "SELECT K_MaKhoa, K_TenKhoa FROM KHOA ORDER BY K_TenKhoa", conn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var ma = r["K_MaKhoa"]?.ToString();
                        var ten = r["K_TenKhoa"]?.ToString();

                        list.Add(new SelectListItem
                        {
                            Value = ma,
                            Text = ten,
                            Selected = (selected != null && selected == ma)
                        });
                    }
                }
            }

            ViewBag.KhoaList = list;
        }

        [HttpGet]
        public ActionResult CreateDoctor()
        {
            ViewBag.Title = "Tạo tài khoản bác sĩ";
            ViewBag.Active = "CreateDoctor";
            LoadKhoaDropDown();
            return View();
        }

        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateDoctor(string username, string password, string fullName, string kMaKhoa)
        {
            // Validate tối thiểu
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                TempData["Err"] = "Vui lòng nhập Username và Password.";
                LoadKhoaDropDown();

                return RedirectToAction("CreateDoctor");
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                TempData["Err"] = "Vui lòng nhập Họ tên bác sĩ.";
                LoadKhoaDropDown();

                return RedirectToAction("CreateDoctor");
            }

            if (string.IsNullOrWhiteSpace(kMaKhoa))
            {
                TempData["Err"] = "Vui lòng chọn khoa.";
                LoadKhoaDropDown();

                return RedirectToAction("CreateDoctor");
            }

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;

            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        string u = username.Trim();
                        string p = password.Trim();


                        // B1: Check trùng username t   heo ciphertext
                        using (var check = new OracleCommand(@"
                            SELECT COUNT(*)
                            FROM   TAIKHOAN
                            WHERE  TK_UserName = PKG_SECURITY.AES_ENCRYPT_B64(:u)", conn))
                        {
                            check.Transaction = tx;
                            check.BindByName = true;
                            check.Parameters.Add("u", u);

                            if (Convert.ToInt32(check.ExecuteScalar()) > 0)
                            {
                                tx.Rollback();
                                TempData["Err"] = "Username đã tồn tại.";
                                return RedirectToAction("CreateDoctor");
                            }
                        }
                        string tenKhoa = null;
                        using (var cmdTen = new OracleCommand(
                            "SELECT K_TenKhoa FROM KHOA WHERE K_MaKhoa = :ma", conn))
                        {
                            cmdTen.Transaction = tx;
                            cmdTen.BindByName = true;
                            cmdTen.Parameters.Add("ma", kMaKhoa);
                            var o = cmdTen.ExecuteScalar();
                            if (o != null && o != DBNull.Value)
                                tenKhoa = o.ToString();
                        }

                        // B2: Sinh mã
                        string ndId = NextId(conn, tx, "NGUOIDUNG", "ND_IdNguoiDung", "ND");
                        string bsId = NextId(conn, tx, "BACSI", "BS_MaBacSi", "BS");
                        //string bnId = NextId(conn, tx, "BENHNHAN", "BN_MaBenhNhan", "BN");
                        string tkId = NextId(conn, tx, "TAIKHOAN", "TK_MaTK", "TK");

                        // B3: Insert NGUOIDUNG (chỉ có họ tên, còn lại null)
                        using (var cmdNd = new OracleCommand(@"
                            INSERT INTO NGUOIDUNG
                        (ND_IdNguoiDung, ND_HoTen,
                         ND_SoDienThoai, ND_Email, ND_CCCD, ND_NgaySinh,
                         ND_GioiTinh, ND_QuocGia, ND_DanToc, ND_NgheNghiep,
                         ND_TinhThanh, ND_QuanHuyen, ND_PhuongXa, ND_DiaChiThuongChu)
                    VALUES
                        (:id, :hoten,
                         NULL, NULL, NULL, NULL,
                         NULL, NULL, NULL, NULL,
                         NULL, NULL, NULL, NULL)", conn))
                        {
                            cmdNd.Transaction = tx;
                            cmdNd.BindByName = true;
                            cmdNd.Parameters.Add("id", ndId);
                            cmdNd.Parameters.Add("hoten", fullName.Trim());
                            cmdNd.ExecuteNonQuery();
                        }

                        // B4: Insert BACSI
                        using (var cmdBs = new OracleCommand(@"
                            INSERT INTO BACSI
                                (BS_MaBacSi,
                                 BS_ChuyenKhoa,
                                 BS_ChucDanh,
                                 BS_NamKinhNghiem,
                                 ND_IdNguoiDung,
                                 K_MaKhoa)
                            VALUES
                                (:bs,
                                 :ck,
                                 :cd,
                                 :nam,
                                 :nd,
                                 :khoa)", conn))
                        {
                            cmdBs.Transaction = tx;
                            cmdBs.BindByName = true;
                            cmdBs.Parameters.Add("bs", bsId);
                            cmdBs.Parameters.Add("ck", (object)tenKhoa ?? DBNull.Value);   // hiển thị trên UI
                            cmdBs.Parameters.Add("cd", "BS");
                            cmdBs.Parameters.Add("nam", OracleDbType.Int32).Value = 0;
                            cmdBs.Parameters.Add("nd", ndId);
                            cmdBs.Parameters.Add("khoa", kMaKhoa);
                            cmdBs.ExecuteNonQuery();
                        }

                        

                        // B6: Insert TAIKHOAN (mã hóa username + hash password)
                        using (var cmdTk = new OracleCommand(@"
                            INSERT INTO TAIKHOAN
                                (TK_MaTK,
                                 TK_UserName,
                                 TK_PassWord,
                                 TK_Role,
                                 TK_TrangThai,
                                 TK_StaffType,
                                 BN_MaBenhNhan,
                                 BS_MaBacSi,
                                 ND_IdNguoiDung)
                            VALUES
                                (:tkId,
                                 PKG_SECURITY.AES_ENCRYPT_B64(:u),
                                 PKG_SECURITY.HASH_PASSWORD(:p),
                                 :role,
                                 :status,
                                 :st,
                                 NULL,
                                 :bs,
                                 :nd)", conn))
                        {
                            cmdTk.Transaction = tx;
                            cmdTk.BindByName = true;
                            cmdTk.Parameters.Add("tkId", tkId);
                            cmdTk.Parameters.Add("u", u);
                            cmdTk.Parameters.Add("p", p);
                            cmdTk.Parameters.Add("role", "User");
                            cmdTk.Parameters.Add("status", "ACTIVE");
                            cmdTk.Parameters.Add("st", "BacSi");
                            //cmdTk.Parameters.Add("bn", bnId);
                            cmdTk.Parameters.Add("bs", bsId);
                            cmdTk.Parameters.Add("nd", ndId);
                            cmdTk.ExecuteNonQuery();
                        }

                        tx.Commit();
                        TempData["Msg"] = "Tạo tài khoản bác sĩ thành công.";
                        return RedirectToAction("CreateDoctor");
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        tx.Rollback();
                        System.Diagnostics.Debug.WriteLine("===== ARGUMENT OUT OF RANGE =====");
                        System.Diagnostics.Debug.WriteLine(ex.ToString());
                        TempData["Err"] = "Lỗi tạo bác sĩ (ArgumentOutOfRangeException): " + ex.Message;
                        return RedirectToAction("CreateDoctor");
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        System.Diagnostics.Debug.WriteLine(ex.ToString());
                        TempData["Err"] = "Lỗi tạo bác sĩ (" + ex.GetType().Name + "): " + ex.Message;
                        return RedirectToAction("CreateDoctor");
                    }
                }
            }
        }

        // ==============================================================
        // HÀM PHỤ: Sinh mã ID theo prefix (ND, BN, BS, TK)
        // ==============================================================

        private static string NextId(
            OracleConnection conn,
            OracleTransaction tx,
            string table,
            string idColumn,
            string prefix)
        {
            using (var lockCmd = new OracleCommand($"LOCK TABLE {table} IN EXCLUSIVE MODE", conn))
            {
                lockCmd.Transaction = tx;
                lockCmd.ExecuteNonQuery();
            }

            var sqlMax = $@"
                SELECT NVL(MAX(TO_NUMBER(SUBSTR({idColumn}, -8))), 0)
                FROM   {table}
                WHERE  {idColumn} LIKE :pfx";

            decimal maxTail;

            using (var cmd = new OracleCommand(sqlMax, conn))
            {
                cmd.Transaction = tx;
                cmd.BindByName = true;
                cmd.Parameters.Add("pfx", OracleDbType.Varchar2).Value = prefix + "%";

                var ret = cmd.ExecuteScalar();
                maxTail = Convert.ToDecimal(ret);
            }

            var next = maxTail + 1;
            return prefix + next.ToString("00000000");   // ND00000001, BS00000001,...
        }




        // ==============================================================
        // ====== F) SỬA THÔNG TIN BÁC SĨ ======
        [HttpGet]
        public ActionResult EditDoctor(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return RedirectToAction("Doctors");
            ViewBag.Title = "Sửa bác sĩ";
            ViewBag.Active = "Doctors"; 
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            BacSi vm = null;
            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                var sql = @"
                SELECT b.BS_MaBacSi, b.BS_ChuyenKhoa, b.BS_ChucDanh, b.BS_NamKinhNghiem,b.K_MaKhoa,
                       nd.ND_HoTen, nd.ND_SoDienThoai, nd.ND_Email,nd.ND_CCCD, nd.ND_NgaySinh,nd.ND_GioiTinh,
                       nd.ND_QuocGia,nd.ND_DanToc,nd.ND_NgheNghiep,nd.ND_TinhThanh,nd.ND_QuanHuyen,
                       nd.ND_PhuongXa,nd.ND_DiaChiThuongChu,
                       PKG_SECURITY.AES_DECRYPT_B64(tk.TK_UserName) AS TK_UserName,
                       NVL(tk.TK_TrangThai, 'PENDING')              AS TK_TrangThai
                  FROM BACSI b
                  JOIN NGUOIDUNG nd ON nd.ND_IdNguoiDung = b.ND_IdNguoiDung
                  JOIN TAIKHOAN tk ON tk.BS_MaBacSi = b.BS_MaBacSi
                 WHERE b.BS_MaBacSi = :id";

                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add("id", id);

                    using (var r = cmd.ExecuteReader())
                    {
                        
                        if (r.Read())
                        {
                            vm = new BacSi()
                            {
                                BS_MaBacSi = r["BS_MaBacSi"]?.ToString(),
                                ND_HoTen = r["ND_HoTen"]?.ToString(),
                                ND_SoDienThoai = r["ND_SoDienThoai"]?.ToString(),
                                ND_Email = r["ND_Email"]?.ToString(),
                                ND_CCCD = r["ND_CCCD"]?.ToString(),
                                ND_NgaySinh = r.IsDBNull(r.GetOrdinal("ND_NgaySinh")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("ND_NgaySinh")),
                                ND_GioiTinh = r.IsDBNull(r.GetOrdinal("ND_GioiTinh")) ? (string)null : r.GetString(r.GetOrdinal("ND_GioiTinh")),
                                ND_QuocGia = r["ND_QuocGia"]?.ToString(),
                                ND_DanToc = r["ND_DanToc"]?.ToString(),
                                ND_NgheNghiep = r["ND_NgheNghiep"]?.ToString(),
                                ND_TinhThanh = r["ND_TinhThanh"]?.ToString(),
                                ND_QuanHuyen = r["ND_QuanHuyen"]?.ToString(),
                                ND_PhuongXa = r["ND_PhuongXa"]?.ToString(),
                                ND_DiaChiThuongChu = r["ND_DiaChiThuongChu"]?.ToString(),
                                TK_UserName = r["TK_UserName"]?.ToString(),
                                TK_TrangThai = r["TK_TrangThai"]?.ToString(),
                                BS_NamKinhNghiem = r.IsDBNull(r.GetOrdinal("BS_NamKinhNghiem"))
                                               ? (int?)null
                                               : Convert.ToInt32(r.GetDecimal(r.GetOrdinal("BS_NamKinhNghiem"))),
                                BS_ChuyenKhoa = r["BS_ChuyenKhoa"]?.ToString(),
                                BS_ChucDanh = r["BS_ChucDanh"]?.ToString(),
                                K_MaKhoa = r["K_MaKhoa"]?.ToString()



                            };
                        }

                    }
                }
            }
            if (vm == null) return RedirectToAction("Doctors");
            LoadKhoaDropDown(vm.K_MaKhoa);
            return View(vm);         
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditDoctor(BacSi vm, string action)
        {
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;

            // ===== 1) NHÁNH GIẢI MÃ =====
            if (action == "Decrypt")
            {
                try
                {
                    // Caesar
                    if (!string.IsNullOrEmpty(vm.ND_TinhThanh))
                        vm.ND_TinhThanh = _caesarCipher.Decrypt(vm.ND_TinhThanh, 15);
                    if (!string.IsNullOrEmpty(vm.ND_QuanHuyen))
                        vm.ND_QuanHuyen = _caesarCipher.Decrypt(vm.ND_QuanHuyen, 15);
                    if (!string.IsNullOrEmpty(vm.ND_PhuongXa))
                        vm.ND_PhuongXa = _caesarCipher.Decrypt(vm.ND_PhuongXa, 15);

                    // RSA
                    if (!string.IsNullOrEmpty(vm.ND_SoDienThoai))
                        vm.ND_SoDienThoai = _rsaService.Decrypt(vm.ND_SoDienThoai);

                    if (!string.IsNullOrEmpty(vm.ND_DiaChiThuongChu))
                        vm.ND_DiaChiThuongChu = _rsaService.Decrypt(vm.ND_DiaChiThuongChu);

                    // Hybrid (key = BS_MaBacSi, giống lúc em mã hóa bên HoSoBacSi)
                    if (!string.IsNullOrEmpty(vm.BS_MaBacSi))
                    {
                        if (!string.IsNullOrEmpty(vm.ND_Email))
                            vm.ND_Email = _hybridService.Decrypt(vm.ND_Email, vm.BS_MaBacSi);

                        if (!string.IsNullOrEmpty(vm.ND_CCCD))
                            vm.ND_CCCD = _hybridService.Decrypt(vm.ND_CCCD, vm.BS_MaBacSi);
                    }

                    TempData["Msg"] = "Giải mã thành công.";
                }
                catch (Exception ex)
                {
                    TempData["Err"] = "Lỗi giải mã: " + ex.Message;
                }

                // load lại dropdown Khoa
                LoadKhoaDropDown(vm.K_MaKhoa);

                // Xóa ModelState để Razor lấy value mới từ vm thay vì value cũ
                ModelState.Clear();
                return View(vm);
            }

            // ===== 2) NHÁNH LƯU (SAVE) =====
            if (action == "Save")
            {
                if (!ModelState.IsValid)
                {
                    //TempData["Err"] = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.";
                    //LoadKhoaDropDown(vm.K_MaKhoa);
                    //return View(vm);
                    var errors = ModelState
                                       .Where(x => x.Value.Errors.Count > 0)
                                       .Select(x => x.Key + ": " + x.Value.Errors.First().ErrorMessage)
                                       .ToList();

                    TempData["Err"] = string.Join("<br>", errors);

                    LoadKhoaDropDown(vm.K_MaKhoa);
                    return View(vm);
                }
                try
                {
                    // Mã hóa lại trước khi lưu
                    vm.ND_TinhThanh = _caesarCipher.Encrypt(vm.ND_TinhThanh, 15);
                    vm.ND_QuanHuyen = _caesarCipher.Encrypt(vm.ND_QuanHuyen, 15);
                    vm.ND_PhuongXa = _caesarCipher.Encrypt(vm.ND_PhuongXa, 15);

                    vm.ND_SoDienThoai = _rsaService.Encrypt(vm.ND_SoDienThoai);
                    vm.ND_DiaChiThuongChu = _rsaService.Encrypt(vm.ND_DiaChiThuongChu);

                    vm.ND_Email = _hybridService.Encrypt(vm.ND_Email, vm.BS_MaBacSi);
                    vm.ND_CCCD = _hybridService.Encrypt(vm.ND_CCCD, vm.BS_MaBacSi);

                    using (var conn = new OracleConnection(cs))
                    {
                        conn.Open();
                        using (var tx = conn.BeginTransaction())
                        {
                            try
                            {
                               

                                // 2) Update BACSI
                                using (var cmd = new OracleCommand(@"
                            UPDATE BACSI
                               SET BS_ChuyenKhoa    = :ck,
                                   BS_ChucDanh      = :cd,
                                   BS_NamKinhNghiem = :nam,
                                   K_MaKhoa         = :khoa
                             WHERE BS_MaBacSi       = :id", conn))
                                {
                                    cmd.Transaction = tx;
                                    cmd.BindByName = true;
                                    cmd.Parameters.Add("ck", (object)vm.BS_ChuyenKhoa ?? DBNull.Value);
                                    cmd.Parameters.Add("cd", (object)vm.BS_ChucDanh ?? DBNull.Value);
                                    cmd.Parameters.Add("nam", vm.BS_NamKinhNghiem.HasValue ? (object)vm.BS_NamKinhNghiem.Value : DBNull.Value);
                                    cmd.Parameters.Add("khoa", (object)vm.K_MaKhoa ?? DBNull.Value);
                                    cmd.Parameters.Add("id", vm.BS_MaBacSi);

                                    cmd.ExecuteNonQuery();
                                }

                                // 3) Update NGUOIDUNG (JOIN đúng với BACSI chứ không phải BN)
                                var sqlND = @"
                            UPDATE NGUOIDUNG nd
                               SET nd.ND_HoTen           = :hoten,
                                   nd.ND_SoDienThoai     = :sdt,
                                   nd.ND_Email           = :email,
                                   nd.ND_CCCD            = :cccd,
                                   nd.ND_NgaySinh        = :ngaysinh,
                                   nd.ND_GioiTinh        = :gioitinh,
                                   nd.ND_QuocGia         = :quocgia,
                                   nd.ND_DanToc          = :dantoc,
                                   nd.ND_NgheNghiep      = :nghenghiep,
                                   nd.ND_TinhThanh       = :tinhthanh,
                                   nd.ND_QuanHuyen       = :quanhuyen,
                                   nd.ND_PhuongXa        = :phuongxa,
                                   nd.ND_DiaChiThuongChu = :diachi
                             WHERE nd.ND_IdNguoiDung = (
                                   SELECT ND_IdNguoiDung
                                   FROM   BACSI
                                   WHERE  BS_MaBacSi = :bsid)";

                                using (var cmd = new OracleCommand(sqlND, conn))
                                {
                                    cmd.Transaction = tx;
                                    cmd.BindByName = true;
                                    cmd.Parameters.Add("hoten", (object)vm.ND_HoTen ?? DBNull.Value);
                                    cmd.Parameters.Add("sdt", (object)vm.ND_SoDienThoai ?? DBNull.Value);
                                    cmd.Parameters.Add("email", (object)vm.ND_Email ?? DBNull.Value);
                                    cmd.Parameters.Add("cccd", (object)vm.ND_CCCD ?? DBNull.Value);
                                    cmd.Parameters.Add("gioitinh", (object)vm.ND_GioiTinh ?? DBNull.Value);
                                    cmd.Parameters.Add("quocgia", (object)vm.ND_QuocGia ?? DBNull.Value);
                                    cmd.Parameters.Add("dantoc", (object)vm.ND_DanToc ?? DBNull.Value);
                                    cmd.Parameters.Add("nghenghiep", (object)vm.ND_NgheNghiep ?? DBNull.Value);
                                    cmd.Parameters.Add("tinhthanh", (object)vm.ND_TinhThanh ?? DBNull.Value);
                                    cmd.Parameters.Add("quanhuyen", (object)vm.ND_QuanHuyen ?? DBNull.Value);
                                    cmd.Parameters.Add("phuongxa", (object)vm.ND_PhuongXa ?? DBNull.Value);
                                    cmd.Parameters.Add("ngaysinh", (object)vm.ND_NgaySinh ?? DBNull.Value);
                                    cmd.Parameters.Add("diachi", (object)vm.ND_DiaChiThuongChu ?? DBNull.Value);
                                    cmd.Parameters.Add("bsid", vm.BS_MaBacSi);   // ⚠️ Đặt đúng tên tham số
                                    cmd.ExecuteNonQuery();
                                }

                                // 4) Update TAIKHOAN
                                using (var cmd = new OracleCommand(@"
                            UPDATE TAIKHOAN
                               SET TK_TrangThai = :tt
                             WHERE BS_MaBacSi   = :id", conn))
                                {
                                    cmd.Transaction = tx;
                                    cmd.BindByName = true;
                                    cmd.Parameters.Add("tt", (object)vm.TK_TrangThai ?? "ACTIVE");
                                    cmd.Parameters.Add("id", vm.BS_MaBacSi);
                                    cmd.ExecuteNonQuery();
                                }

                                tx.Commit();
                                TempData["Msg"] = "Cập nhật bác sĩ thành công.";
                                return RedirectToAction("EditDoctor", new { id = vm.BS_MaBacSi });
                            }
                            catch (Exception exTx)
                            {
                                tx.Rollback();
                                TempData["Err"] = "Lỗi cập nhật: " + exTx.Message;
                                LoadKhoaDropDown(vm.K_MaKhoa);
                                return View(vm);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    TempData["Err"] = "Lỗi lưu: " + ex.Message;
                    LoadKhoaDropDown(vm.K_MaKhoa);
                    return View(vm);
                }

            }


            // Nếu action không phải Save / Decrypt thì quay về danh sách
            return RedirectToAction("Doctors");
        }

        // ====== F) DANH SÁCH BÁC SĨ ======
        [AdminOnly]
        public ActionResult Doctors(string user, string kw = null)
        {
            ViewBag.Title = "Quản lý bác sĩ";
            ViewBag.Active = "Doctors";

            var list = new List<BacSi>();
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;

            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                string decryptedUser = _maHoa_GiaiMa_Sql.DecryptUser(user?.Trim(), conn);
                var sql = @"
            SELECT
                bs.BS_MaBacSi           AS BS_MaBacSi,
                nd.ND_HoTen             AS ND_HoTen,
                bs.BS_ChuyenKhoa        AS BS_ChuyenKhoa,
                bs.BS_ChucDanh          AS BS_ChucDanh,
                bs.BS_NamKinhNghiem     AS BS_NamKinhNghiem,
                 PKG_SECURITY.AES_DECRYPT_B64(tk.TK_UserName) AS TK_UserName,
                NVL(tk.TK_TrangThai,'') AS TK_TrangThai,
                nd.ND_SoDienThoai       AS ND_SoDienThoai,
                nd.ND_Email             AS ND_Email
            FROM BACSI bs
            JOIN NGUOIDUNG nd ON nd.ND_IdNguoiDung = bs.ND_IdNguoiDung
            LEFT JOIN TAIKHOAN tk ON tk.BS_MaBacSi = bs.BS_MaBacSi
            WHERE ( :kw IS NULL
                 OR UPPER(bs.BS_MaBacSi) LIKE '%'||UPPER(:kw)||'%'
                 OR UPPER(nd.ND_HoTen)    LIKE '%'||UPPER(:kw)||'%'
                 OR UPPER(tk.TK_UserName) LIKE '%'||UPPER(:kw)||'%' )
            ORDER BY nd.ND_HoTen";

                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add("pUser", decryptedUser);
                    // có thể truyền 1 lần; Oracle cho phép lặp lại tên tham số khi BindByName = true
                    cmd.Parameters.Add("kw", string.IsNullOrWhiteSpace(kw) ? (object)DBNull.Value : kw.Trim());

                    using (var r = cmd.ExecuteReader())
                    {
                        // lấy sẵn ordinal để tránh sai alias & tăng tốc
                        int cMaBS = r.GetOrdinal("BS_MaBacSi");
                        int cHoTen = r.GetOrdinal("ND_HoTen");
                        int cCK = r.GetOrdinal("BS_ChuyenKhoa");
                        int cCD = r.GetOrdinal("BS_ChucDanh");
                        int cKN = r.GetOrdinal("BS_NamKinhNghiem");
                        int cUser = r.GetOrdinal("TK_UserName");
                        int cTrang = r.GetOrdinal("TK_TrangThai");
                        int cSdt = r.GetOrdinal("ND_SoDienThoai");
                        int cEmail = r.GetOrdinal("ND_Email");

                        while (r.Read())
                        {
                            var bacSi = new BacSi
                            {
                                BS_MaBacSi = r.IsDBNull(cMaBS) ? null : r.GetString(cMaBS),
                                ND_HoTen = r.IsDBNull(cHoTen) ? null : r.GetString(cHoTen),
                                BS_ChuyenKhoa = r.IsDBNull(cCK) ? null : r.GetString(cCK),
                                BS_ChucDanh = r.IsDBNull(cCD) ? null : r.GetString(cCD),
                                BS_NamKinhNghiem = r.IsDBNull(cKN) ? (int?)null : Convert.ToInt32(r.GetDecimal(cKN)),
                                TK_UserName = r.IsDBNull(cUser) ? null : r.GetString(cUser),
                                TK_TrangThai = r.IsDBNull(cTrang) ? null : r.GetString(cTrang),
                                ND_SoDienThoai = r.IsDBNull(cSdt) ? null : r.GetString(cSdt),
                                ND_Email = r.IsDBNull(cEmail) ? null : r.GetString(cEmail),
                            };


                            // Giải mã các trường cần thiết
                            try
                            {
                                if (!string.IsNullOrEmpty(bacSi.ND_SoDienThoai))
                                {
                                    bacSi.ND_SoDienThoai = _rsaService.Decrypt(bacSi.ND_SoDienThoai);  // Giải mã số điện thoại
                                }
                                if (!string.IsNullOrEmpty(bacSi.BS_MaBacSi))
                                {
                                    bacSi.ND_Email = _hybridService.Decrypt(bacSi.ND_Email, bacSi.BS_MaBacSi);
                                    
                                }
                            }
                            catch (FormatException ex)
                            {
                                
                                Console.WriteLine($"Lỗi khi giải mã số điện thoại: {ex.Message}");
                                bacSi.ND_SoDienThoai = " "; 
                                bacSi.ND_Email = " ";
                            }
                            

                            list.Add(bacSi);


       
                        }
                    }
                }
            }

            return View("Doctors", list);
        }


        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ToggleDoctorStatus(string id)
        {
            // id = BS_MaBacSi
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            try
            {
                using (var conn = new OracleConnection(cs))
                {
                    conn.Open();
                    var sql = @"
                    UPDATE TAIKHOAN
                       SET TK_TrangThai = CASE UPPER(NVL(TK_TrangThai,'PENDING'))
                                            WHEN 'ACTIVE' THEN 'LOCKED'
                                            ELSE 'ACTIVE'
                                          END
                     WHERE BS_MaBacSi = :id";
                    using (var cmd = new OracleCommand(sql, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("id", id);
                        var aff = cmd.ExecuteNonQuery();
                        TempData["Msg"] = aff > 0 ? "Đã cập nhật trạng thái tài khoản bác sĩ." : "Không tìm thấy tài khoản.";
                    }
                }
            }
            catch (OracleException ex)
            {
                TempData["Err"] = $"ORA-{ex.Number}: {ex.Message}";
            }
            return RedirectToAction("Doctors");
        }


        // Quanly benh nhan
        // ====== A. Danh sách / tìm kiếm bệnh nhân ======
        [HttpGet]
        public ActionResult Patients(string kw = null, int page = 1, int pageSize = 20)
        {
            ViewBag.Title = "Quản lý bệnh nhân";
            ViewBag.Active = "Patients";
            kw = (kw ?? "").Trim();

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            var list = new List<BenhNhan>();

            using (var conn = new OracleConnection(cs))
            {
                conn.Open();

                // Chú ý: StaffType có thể lưu 'Bệnh nhân' hoặc 'BenhNhan' tùy trước đó.
                var sql = @"
                            SELECT  bn.BN_MaBenhNhan,
                                    nd.ND_HoTen,
                                    nd.ND_SoDienThoai,
                                    nd.ND_Email,
                                    nd.ND_NgaySinh,
                                    nd.ND_DiaChiThuongChu,
                                    PKG_SECURITY.AES_DECRYPT_B64(tk.TK_UserName) AS TK_UserName,
                                    NVL(tk.TK_TrangThai, 'PENDING')             AS TK_TrangThai,
                                    bn.BN_SoBaoHiemYT,
                                    bn.BN_NhomMau,
                                    bn.BN_TieuSuBenhAn
                            FROM    BENHNHAN bn
                                    JOIN NGUOIDUNG nd
                                        ON nd.ND_IdNguoiDung = bn.ND_IdNguoiDung
                                    LEFT JOIN TAIKHOAN tk
                                        ON tk.BN_MaBenhNhan = bn.BN_MaBenhNhan
                            WHERE   -- 1) Không lấy bất kỳ BN nào gắn với tài khoản Admin
                                    NOT EXISTS (
                                        SELECT 1
                                        FROM   TAIKHOAN tk2
                                        WHERE  tk2.BN_MaBenhNhan = bn.BN_MaBenhNhan
                                        AND    UPPER(tk2.TK_Role) = 'ADMIN'
                                    )
                              AND (  :kw IS NULL OR :kw = '' OR
                                     UPPER(nd.ND_HoTen) LIKE UPPER('%' || :kw || '%') OR
                                     UPPER(PKG_SECURITY.AES_DECRYPT_B64(tk.TK_UserName))
                                            LIKE UPPER('%' || :kw || '%') OR
                                     UPPER(bn.BN_MaBenhNhan) LIKE UPPER('%' || :kw || '%')
                                  )
                            ORDER BY nd.ND_HoTen";
                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add("kw", kw);

                    //using (var r = cmd.ExecuteReader())
                    //{
                    //    while (r.Read())
                    //    {
                    //        list.Add(new BenhNhan
                    //        {
                    //            BN_MaBenhNhan = r["BN_MaBenhNhan"]?.ToString(),
                    //            ND_HoTen = r["ND_HoTen"]?.ToString(),
                    //            ND_SoDienThoai = r["ND_SoDienThoai"]?.ToString(),
                    //            ND_Email = r["ND_Email"]?.ToString(),
                    //            ND_NgaySinh = r.IsDBNull(r.GetOrdinal("ND_NgaySinh")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("ND_NgaySinh")),
                    //            ND_DiaChiThuongChu = r["ND_DiaChiThuongChu"]?.ToString(),
                    //            UserName = r["TK_UserName"]?.ToString(),
                    //            TrangThai = r["TK_TrangThai"]?.ToString(),
                    //            BN_SoBaoHiemYT = r["BN_SoBaoHiemYT"]?.ToString(),
                    //            BN_NhomMau = r["BN_NhomMau"]?.ToString(),
                    //            BN_TieuSuBenhAn = r["BN_TieuSuBenhAn"]?.ToString()


                    //        });

                    //    }
                    //}
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var benhNhan = new BenhNhan
                            {
                                BN_MaBenhNhan = r["BN_MaBenhNhan"]?.ToString(),
                                ND_HoTen = r["ND_HoTen"]?.ToString(),
                                ND_SoDienThoai = r["ND_SoDienThoai"]?.ToString(),
                                ND_Email = r["ND_Email"]?.ToString(),
                                ND_NgaySinh = r.IsDBNull(r.GetOrdinal("ND_NgaySinh")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("ND_NgaySinh")),
                                ND_DiaChiThuongChu = r["ND_DiaChiThuongChu"]?.ToString(),
                                UserName = r["TK_UserName"]?.ToString(),
                                TrangThai = r["TK_TrangThai"]?.ToString(),
                                BN_SoBaoHiemYT = r["BN_SoBaoHiemYT"]?.ToString(),
                                BN_NhomMau = r["BN_NhomMau"]?.ToString(),
                                BN_TieuSuBenhAn = r["BN_TieuSuBenhAn"]?.ToString()
                            };

                            // Giải mã các trường cần thiết (Giải mã RSA cho các trường nhạy cảm)
                            try
                            {
                                if (!string.IsNullOrEmpty(benhNhan.ND_SoDienThoai))
                                {
                                    benhNhan.ND_SoDienThoai = _rsaService.Decrypt(benhNhan.ND_SoDienThoai);
                                }

                                if (!string.IsNullOrEmpty(benhNhan.ND_Email))
                                {
                                    benhNhan.ND_Email = _hybridService.Decrypt(benhNhan.ND_Email, benhNhan.BN_MaBenhNhan);
                                }

                                // Nếu cần giải mã thêm các trường khác, bạn có thể tiếp tục sử dụng các phương thức tương tự.
                                // Ví dụ: Giải mã địa chỉ
                                if (!string.IsNullOrEmpty(benhNhan.ND_DiaChiThuongChu))
                                {
                                    benhNhan.ND_DiaChiThuongChu = _rsaService.Decrypt(benhNhan.ND_DiaChiThuongChu);
                                }

                                // Giải mã các trường Hybrid (nếu có)
                                //if (!string.IsNullOrEmpty(benhNhan.BN_SoBaoHiemYT))
                                //{
                                //    benhNhan.BN_SoBaoHiemYT = _hybridService.Decrypt(benhNhan.BN_SoBaoHiemYT, benhNhan.BN_MaBenhNhan);
                                //}
                                //if (!string.IsNullOrEmpty(benhNhan.BN_TieuSuBenhAn))
                                //{
                                //    benhNhan.BN_TieuSuBenhAn = _rsaService.Decrypt(benhNhan.BN_TieuSuBenhAn);
                                //}
                            }
                            catch (Exception ex)
                            {
                                // Nếu có lỗi trong quá trình giải mã, bạn có thể log lỗi hoặc thực hiện các hành động cần thiết
                                System.Diagnostics.Debug.WriteLine($"Lỗi giải mã: {ex.Message}");
                            }

                            // Thêm vào danh sách bệnh nhân
                            list.Add(benhNhan);
                        }
                    }

                }
            }

            // (tuỳ) phân trang phía server sau
            return View(list);
        }

        // ====== B. Sửa bệnh nhân (GET) ======
        [HttpGet]
        public ActionResult EditPatient(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return RedirectToAction("Patients");

            ViewBag.Title = "Sửa bệnh nhân";
            ViewBag.Active = "Patients";

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            BenhNhan bn = null;

            using (var conn = new OracleConnection(cs))
            {
                conn.Open();
                var sql = @"
                            SELECT  bn.BN_MaBenhNhan,
                                    nd.ND_HoTen, nd.ND_SoDienThoai, nd.ND_Email,nd.ND_CCCD, nd.ND_NgaySinh,nd.ND_GioiTinh,nd.ND_QuocGia,nd.ND_DanToc,nd.ND_NgheNghiep,nd.ND_TinhThanh,nd.ND_QuanHuyen,nd.ND_PhuongXa,nd.ND_DiaChiThuongChu,
                                    tk.TK_UserName, NVL(tk.TK_TrangThai, 'PENDING') AS TK_TrangThai,
                                    bn.BN_SoBaoHiemYT, bn.BN_NhomMau, bn.BN_TieuSuBenhAn
                            FROM    BENHNHAN bn
                            JOIN    NGUOIDUNG nd ON nd.ND_IdNguoiDung = bn.ND_IdNguoiDung
                            LEFT JOIN TAIKHOAN tk ON tk.BN_MaBenhNhan = bn.BN_MaBenhNhan
                            WHERE   bn.BN_MaBenhNhan = :id";

                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add("id", id);

                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            bn = new BenhNhan
                            {
                                BN_MaBenhNhan = r["BN_MaBenhNhan"]?.ToString(),
                                ND_HoTen = r["ND_HoTen"]?.ToString(),
                                ND_SoDienThoai = r["ND_SoDienThoai"]?.ToString(),
                                ND_Email = r["ND_Email"]?.ToString(),
                                ND_CCCD = r["ND_CCCD"]?.ToString(),
                                ND_NgaySinh = r.IsDBNull(r.GetOrdinal("ND_NgaySinh")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("ND_NgaySinh")),
                                ND_GioiTinh = r.IsDBNull(r.GetOrdinal("ND_GioiTinh")) ? (string)null : r.GetString(r.GetOrdinal("ND_GioiTinh")),
                                ND_QuocGia = r["ND_QuocGia"]?.ToString(),
                                ND_DanToc = r["ND_DanToc"]?.ToString(),
                                ND_NgheNghiep = r["ND_NgheNghiep"]?.ToString(),
                                ND_TinhThanh = r["ND_TinhThanh"]?.ToString(),
                                ND_QuanHuyen = r["ND_QuanHuyen"]?.ToString(),
                                ND_PhuongXa = r["ND_PhuongXa"]?.ToString(),
                                ND_DiaChiThuongChu = r["ND_DiaChiThuongChu"]?.ToString(),
                                UserName = r["TK_UserName"]?.ToString(),
                                TrangThai = r["TK_TrangThai"]?.ToString(),
                                BN_SoBaoHiemYT = r["BN_SoBaoHiemYT"]?.ToString(),
                                BN_NhomMau = r["BN_NhomMau"]?.ToString(),
                                BN_TieuSuBenhAn = r["BN_TieuSuBenhAn"]?.ToString()
                            };
                        }
                    }
                }
            }

            if (bn == null) return RedirectToAction("Patients");
            return View(bn);
        }

        // ====== C. Sửa bệnh nhân (POST) ======
        [HttpPost, ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult EditPatient(DangKyKhamBenh.Models.ViewModels.BenhNhan model, string action)
        {
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;

            if (action == "Decrypt")
            {

                try
                {
                    // Giải mã dữ liệu Caesar
                    //model.ND_HoTen = _caesarCipher.Decrypt(model.ND_HoTen, 15);
                    model.ND_TinhThanh = _caesarCipher.Decrypt(model.ND_TinhThanh, 15);
                    model.ND_QuanHuyen = _caesarCipher.Decrypt(model.ND_QuanHuyen, 15);
                    model.ND_PhuongXa = _caesarCipher.Decrypt(model.ND_PhuongXa, 15);

                    // Giải mã dữ liệu RSA
                    try
                    {
                        model.ND_SoDienThoai = _rsaService.Decrypt(model.ND_SoDienThoai);
                        System.Diagnostics.Debug.WriteLine("Số điện thoại sau giải mã: " + model.ND_SoDienThoai);
                    }
                    catch (Exception ex)
                    {
                        TempData["Err"] = "Lỗi giải mã số điện thoại: " + ex.Message;
                        return View(model);
                    }
                    model.ND_DiaChiThuongChu = _rsaService.Decrypt(model.ND_DiaChiThuongChu);
                    model.BN_TieuSuBenhAn = _rsaService.Decrypt(model.BN_TieuSuBenhAn);

                    // Giải mã dữ liệu Hybrid (không cần gọi key từ DB)
                    model.ND_Email = _hybridService.Decrypt(model.ND_Email, model.BN_MaBenhNhan);

                    model.ND_CCCD = _hybridService.Decrypt(model.ND_CCCD, model.BN_MaBenhNhan);
                    model.BN_SoBaoHiemYT = _hybridService.Decrypt(model.BN_SoBaoHiemYT, model.BN_MaBenhNhan);

                    TempData["Msg"] = "Giải mã thành công.";
                }
                catch (Exception ex)
                {
                    TempData["Err"] = "Lỗi giải mã: " + ex.Message;
                }

                ModelState.Clear(); // Bỏ qua lỗi validation do dữ liệu đang mã hóa
                return View(model);

            }



            if (action == "Save")
            {
                if (!ModelState.IsValid)
                {
                    TempData["Err"] = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.";
                    return View(model);
                }


            }
            try
            {
                //  Mã hóa lại trước khi lưu
                //model.ND_HoTen = _caesarCipher.Encrypt(model.ND_HoTen, 15);
                model.ND_TinhThanh = _caesarCipher.Encrypt(model.ND_TinhThanh, 15);
                model.ND_QuanHuyen = _caesarCipher.Encrypt(model.ND_QuanHuyen, 15);
                model.ND_PhuongXa = _caesarCipher.Encrypt(model.ND_PhuongXa, 15);

                model.ND_SoDienThoai = _rsaService.Encrypt(model.ND_SoDienThoai);
                model.ND_DiaChiThuongChu = _rsaService.Encrypt(model.ND_DiaChiThuongChu);
                model.BN_TieuSuBenhAn = _rsaService.Encrypt(model.BN_TieuSuBenhAn);

                model.ND_Email = _hybridService.Encrypt(model.ND_Email, model.BN_MaBenhNhan);
                model.ND_CCCD = _hybridService.Encrypt(model.ND_CCCD, model.BN_MaBenhNhan);
                model.BN_SoBaoHiemYT = _hybridService.Encrypt(model.BN_SoBaoHiemYT, model.BN_MaBenhNhan);

                try
                {
                    using (var conn = new OracleConnection(cs))
                    {
                        conn.Open();
                        using (var tx = conn.BeginTransaction())
                        {

                            // Update NGUOIDUNG qua subquery (lấy ND_IdNguoiDung từ BN)
                            var sqlND = @"
                                        UPDATE NGUOIDUNG nd
                                        SET nd.ND_HoTen          = :hoten,
                                            nd.ND_SoDienThoai    = :sdt,
                                            nd.ND_Email          = :email,
                                            nd.ND_CCCD          = :cccd,
                                            nd.ND_NgaySinh       = :ngaysinh,
                                            nd.ND_GioiTinh          = :gioitinh,
                                            nd.ND_QuocGia          = :quocgia,
                                            nd.ND_DanToc          = :dantoc,
                                            nd.ND_NgheNghiep          = :nghenghiep,
                                            nd.ND_TinhThanh          = :tinhthanh,
                                            nd.ND_QuanHuyen          = :quanhuyen,
                                            nd.ND_PhuongXa          = :phuongxa,
                                            nd.ND_DiaChiThuongChu= :diachi
                                        WHERE nd.ND_IdNguoiDung = (SELECT ND_IdNguoiDung FROM BENHNHAN WHERE BN_MaBenhNhan = :bnid)";

                            using (var cmd = new OracleCommand(sqlND, conn))
                            {
                                cmd.Transaction = tx; cmd.BindByName = true;
                                cmd.Parameters.Add("hoten", (object)model.ND_HoTen ?? DBNull.Value);
                                cmd.Parameters.Add("sdt", (object)model.ND_SoDienThoai ?? DBNull.Value);
                                cmd.Parameters.Add("email", (object)model.ND_Email ?? DBNull.Value);
                                cmd.Parameters.Add("cccd", (object)model.ND_CCCD ?? DBNull.Value);
                                cmd.Parameters.Add("gioitinh", (object)model.ND_GioiTinh ?? DBNull.Value);
                                cmd.Parameters.Add("quocgia", (object)model.ND_QuocGia ?? DBNull.Value);
                                cmd.Parameters.Add("dantoc", (object)model.ND_DanToc ?? DBNull.Value);
                                cmd.Parameters.Add("nghenghiep", (object)model.ND_NgheNghiep ?? DBNull.Value);
                                cmd.Parameters.Add("tinhthanh", (object)model.ND_TinhThanh ?? DBNull.Value);
                                cmd.Parameters.Add("quanhuyen", (object)model.ND_QuanHuyen ?? DBNull.Value);
                                cmd.Parameters.Add("phuongxa", (object)model.ND_PhuongXa ?? DBNull.Value);
                                cmd.Parameters.Add("ngaysinh", (object)model.ND_NgaySinh ?? DBNull.Value);
                                cmd.Parameters.Add("diachi", (object)model.ND_DiaChiThuongChu ?? DBNull.Value);
                                cmd.Parameters.Add("bnid", model.BN_MaBenhNhan);
                                cmd.ExecuteNonQuery();
                            }

                            // Update BN (nếu bạn dùng các field phụ)
                            var sqlBN = @"
                                        UPDATE BENHNHAN
                                        SET BN_SoBaoHiemYT = :bh,
                                            BN_NhomMau     = :nm,
                                            BN_TieuSuBenhAn= :ts
                                        WHERE BN_MaBenhNhan = :bnid";

                            using (var cmd = new OracleCommand(sqlBN, conn))
                            {
                                cmd.Transaction = tx; cmd.BindByName = true;
                                cmd.Parameters.Add("bh", (object)model.BN_SoBaoHiemYT ?? DBNull.Value);
                                cmd.Parameters.Add("nm", (object)model.BN_NhomMau ?? DBNull.Value);
                                cmd.Parameters.Add("ts", (object)model.BN_TieuSuBenhAn ?? DBNull.Value);
                                cmd.Parameters.Add("bnid", model.BN_MaBenhNhan);
                                cmd.ExecuteNonQuery();
                            }

                            tx.Commit();
                            TempData["Msg"] = "Cập nhật bệnh nhân thành công.";
                            return RedirectToAction("Patients");
                        }
                    }
                }
                catch (Exception ex)
                {
                    TempData["Err"] = "Lỗi cập nhật: " + ex.Message;
                    return View(model);
                }

                
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Lỗi lưu: " + ex.Message;
                return View(model);
            }

            
        }
       

        // ====== D. Khoá/Mở tài khoản bệnh nhân (TOGGLE) ======
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult TogglePatientStatus(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return RedirectToAction("Patients");

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;

            try
            {
                using (var conn = new OracleConnection(cs))
                {
                    conn.Open();

                    // Lấy trạng thái hiện tại
                    string status = null;
                    using (var get = new OracleCommand(
                        "SELECT TK_TrangThai FROM TAIKHOAN WHERE BN_MaBenhNhan = :id", conn))
                    {
                        get.BindByName = true;
                        get.Parameters.Add("id", id);
                        var o = get.ExecuteScalar();
                        status = (o == null || o == DBNull.Value) ? null : o.ToString();
                    }

                    if (string.IsNullOrEmpty(status))
                    {
                        TempData["Err"] = "Bệnh nhân chưa có tài khoản đăng nhập.";
                        return RedirectToAction("Patients");
                    }

                    var newStatus = string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                                    ? "LOCKED" : "ACTIVE";

                    using (var upd = new OracleCommand(
                        "UPDATE TAIKHOAN SET TK_TrangThai = :st WHERE BN_MaBenhNhan = :id", conn))
                    {
                        upd.BindByName = true;
                        upd.Parameters.Add("st", newStatus);
                        upd.Parameters.Add("id", id);
                        upd.ExecuteNonQuery();
                    }
                }

                TempData["Msg"] = "Cập nhật trạng thái tài khoản thành công.";
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Lỗi: " + ex.Message;
            }

            return RedirectToAction("Patients");
        }

        // xóa bệnh nhân
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeletePatient(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Err"] = "Thiếu mã bệnh nhân.";
                return RedirectToAction("Patients");
            }

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;

            try
            {
                using (var conn = new OracleConnection(cs))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        try
                        {
                            // 0) Lấy ND_IdNguoiDung của bệnh nhân
                            string ndId = null;
                            using (var cmd = new OracleCommand(
                                "SELECT ND_IdNguoiDung FROM BENHNHAN WHERE BN_MaBenhNhan = :id", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                var o = cmd.ExecuteScalar();
                                if (o == null || o == DBNull.Value)
                                {
                                    tx.Rollback();
                                    TempData["Err"] = "Không tìm thấy bệnh nhân.";
                                    return RedirectToAction("Patients");
                                }
                                ndId = o.ToString();
                            }

                            // 1) XÓA KẾT QUẢ (KETQUA) liên quan bệnh nhân này
                            using (var cmd = new OracleCommand(
                                "DELETE FROM KETQUA WHERE BN_MaBenhNhan = :id", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                cmd.ExecuteNonQuery();
                            }

                            // 2) XÓA CHI TIẾT ĐƠN THUỐC (CHITIETDONTHUOC)
                            using (var cmd = new OracleCommand(@"
                        DELETE FROM CHITIETDONTHUOC
                        WHERE DT_MaDT IN (
                            SELECT d.DT_MaDT
                            FROM DONTHUOC d
                            JOIN PHIEUDANGKY dk 
                              ON dk.DK_MaPhieuKham = d.DK_MaPhieuKham
                            WHERE dk.BN_MaBenhNhan = :id
                        )", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                cmd.ExecuteNonQuery();
                            }

                            // 3) XÓA PHIẾU DỊCH VỤ (PHIEUDICHVU)
                            using (var cmd = new OracleCommand(
                                "DELETE FROM PHIEUDICHVU WHERE BN_MaBenhNhan = :id", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                cmd.ExecuteNonQuery();
                            }

                            // 4) XÓA PHIẾU CẬN LÂM SÀN (PHIEUCANLAMSAN) của các phiếu khám BN này
                            using (var cmd = new OracleCommand(@"
                        DELETE FROM PHIEUCANLAMSAN
                        WHERE DK_MaPhieuKham IN (
                            SELECT DK_MaPhieuKham
                            FROM PHIEUDANGKY
                            WHERE BN_MaBenhNhan = :id
                        )", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                cmd.ExecuteNonQuery();
                            }

                            // 5) XÓA ĐƠN THUỐC (DONTHUOC)
                            using (var cmd = new OracleCommand(@"
                        DELETE FROM DONTHUOC
                        WHERE DK_MaPhieuKham IN (
                            SELECT DK_MaPhieuKham
                            FROM PHIEUDANGKY
                            WHERE BN_MaBenhNhan = :id
                        )", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                cmd.ExecuteNonQuery();
                            }

                            // 6) XÓA HÓA ĐƠN (HOADON)
                            using (var cmd = new OracleCommand(@"
                        DELETE FROM HOADON
                        WHERE DK_MaPhieuKham IN (
                            SELECT DK_MaPhieuKham
                            FROM PHIEUDANGKY
                            WHERE BN_MaBenhNhan = :id
                        )", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                cmd.ExecuteNonQuery();
                            }

                            // 7) XÓA PHIẾU ĐĂNG KÝ (PHIEUDANGKY) – đây chính là FKPHIEUDANGK813705
                            using (var cmd = new OracleCommand(
                                "DELETE FROM PHIEUDANGKY WHERE BN_MaBenhNhan = :id", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                cmd.ExecuteNonQuery();
                            }

                            // 8) XÓA TÀI KHOẢN (TAIKHOAN)
                            using (var cmd = new OracleCommand(@"
                        DELETE FROM TAIKHOAN
                        WHERE BN_MaBenhNhan = :id
                           OR ND_IdNguoiDung = :nd", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                cmd.Parameters.Add("nd", ndId);
                                cmd.ExecuteNonQuery();
                            }

                            // 9) XÓA BỆNH NHÂN (BENHNHAN)
                            using (var cmd = new OracleCommand(
                                "DELETE FROM BENHNHAN WHERE BN_MaBenhNhan = :id", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                cmd.ExecuteNonQuery();
                            }

                            // 10) XÓA NGƯỜI DÙNG (NGUOIDUNG) – nếu chắc chắn ND này không dùng nơi khác
                            using (var cmd = new OracleCommand(
                                "DELETE FROM NGUOIDUNG WHERE ND_IdNguoiDung = :nd", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("nd", ndId);
                                cmd.ExecuteNonQuery();
                            }

                            tx.Commit();
                            TempData["Msg"] = "Đã xóa bệnh nhân và toàn bộ dữ liệu liên quan.";
                        }
                        catch (Exception ex)
                        {
                            tx.Rollback();
                            TempData["Err"] = "Lỗi xóa: " + ex.Message;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Lỗi kết nối: " + ex.Message;
            }

            return RedirectToAction("Patients");
        }


        // xóa bs
        // xóa bs
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteDoctor(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Err"] = "Thiếu mã bác sĩ.";
                return RedirectToAction("Doctors");
            }

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;

            try
            {
                using (var conn = new OracleConnection(cs))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        try
                        {
                            // 0) Không cho xóa nếu BS này là admin
                            using (var checkCmd = new OracleCommand(@"
                        SELECT COUNT(*)
                        FROM   TAIKHOAN
                        WHERE  BS_MaBacSi = :id
                        AND    UPPER(TK_Role) = 'ADMIN'", conn))
                            {
                                checkCmd.Transaction = tx;
                                checkCmd.BindByName = true;
                                checkCmd.Parameters.Add("id", id);

                                var cnt = Convert.ToInt32(checkCmd.ExecuteScalar() ?? 0);
                                if (cnt > 0)
                                {
                                    tx.Rollback();
                                    TempData["Err"] = "Không thể xoá: đây là tài khoản Admin.";
                                    return RedirectToAction("Doctors");
                                }
                            }

                            // 1) Lấy ND_IdNguoiDung của bác sĩ
                            string ndId = null;
                            using (var cmd = new OracleCommand(
                                "SELECT ND_IdNguoiDung FROM BACSI WHERE BS_MaBacSi = :id", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                var o = cmd.ExecuteScalar();
                                if (o == null || o == DBNull.Value)
                                {
                                    tx.Rollback();
                                    TempData["Err"] = "Không tìm thấy bác sĩ.";
                                    return RedirectToAction("Doctors");
                                }
                                ndId = o.ToString();
                            }

                            // 2) Xóa KETQUA liên quan tới PHIEUDICHVU của bác sĩ này
                            using (var cmd = new OracleCommand(@"
                        DELETE FROM KETQUA k
                        WHERE EXISTS (
                            SELECT 1
                            FROM   PHIEUDICHVU p
                            WHERE  p.PDV_MaPhieu   = k.DV_MaPhieu
                            AND    p.BN_MaBenhNhan = k.BN_MaBenhNhan
                            AND    p.DV_MaDichVu   = k.DV_MaDichVu
                            AND    p.BS_MaBacSi    = :id
                        )", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                cmd.ExecuteNonQuery();
                            }

                            // 3) Xóa PHIEUDICHVU của bác sĩ
                            using (var cmd = new OracleCommand(
                                "DELETE FROM PHIEUDICHVU WHERE BS_MaBacSi = :id", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                cmd.ExecuteNonQuery();
                            }

                            // 4) Xóa SLOTKHAM liên quan
                            using (var cmd = new OracleCommand(@"
                        DELETE FROM SLOTKHAM
                        WHERE PC_Id IN (
                            SELECT PC_Id FROM PHANCONG WHERE BS_MaBacSi = :id
                        )", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                cmd.ExecuteNonQuery();
                            }

                            // 5) Xóa PHANCONG của bác sĩ
                            using (var cmd = new OracleCommand(
                                "DELETE FROM PHANCONG WHERE BS_MaBacSi = :id", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                cmd.ExecuteNonQuery();
                            }

                            // 6) Khoa nào đang dùng bác sĩ này làm trưởng khoa -> NULL
                            using (var cmd = new OracleCommand(
                                "UPDATE KHOA SET K_TruongKhoa = NULL WHERE K_TruongKhoa = :id", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                cmd.ExecuteNonQuery();
                            }

                            // 7) Xóa tài khoản login
                            using (var cmd = new OracleCommand(@"
                        DELETE FROM TAIKHOAN
                        WHERE BS_MaBacSi = :id OR ND_IdNguoiDung = :nd", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                cmd.Parameters.Add("nd", ndId);
                                cmd.ExecuteNonQuery();
                            }

                            // 8) Xóa bác sĩ
                            using (var cmd = new OracleCommand(
                                "DELETE FROM BACSI WHERE BS_MaBacSi = :id", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", id);
                                cmd.ExecuteNonQuery();
                            }

                            // 9) Xóa NGUOIDUNG nếu không còn liên kết
                            using (var cmd = new OracleCommand(@"
                        DELETE FROM NGUOIDUNG nd
                        WHERE nd.ND_IdNguoiDung = :nd
                          AND NOT EXISTS (SELECT 1 FROM BACSI    b  WHERE b.ND_IdNguoiDung = nd.ND_IdNguoiDung)
                          AND NOT EXISTS (SELECT 1 FROM BENHNHAN bn WHERE bn.ND_IdNguoiDung = nd.ND_IdNguoiDung)
                          AND NOT EXISTS (SELECT 1 FROM TAIKHOAN tk WHERE tk.ND_IdNguoiDung = nd.ND_IdNguoiDung)", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("nd", ndId);
                                cmd.ExecuteNonQuery();
                            }

                            tx.Commit();
                            TempData["Msg"] = "Đã xóa bác sĩ và toàn bộ dữ liệu liên quan.";
                        }
                        catch (Exception ex)
                        {
                            tx.Rollback();
                            TempData["Err"] = "Lỗi xóa bác sĩ: " + ex.Message;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Lỗi kết nối: " + ex.Message;
            }

            return RedirectToAction("Doctors");
        }

        // ====== G) LỊCH LÀM CỦA BÁC SĨ ======
        [AdminOnly]
        [HttpGet]
        public ActionResult DoctorSchedule(string kw = null, DateTime? ngay = null)
        {
            ViewBag.Title = "Lịch làm của bác sĩ";
            ViewBag.Active = "DoctorSchedule";

            kw = (kw ?? "").Trim();

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            var list = new List<LichBacSi>();

            using (var conn = new OracleConnection(cs))
            {
                conn.Open();

                var sql = @"
            SELECT
                pc.PC_Id,
                pc.PC_Ngay,
                pc.PC_CaTruc,
                b.BS_MaBacSi,
                b.BS_ChuyenKhoa,
                nd.ND_HoTen       AS TEN_BAC_SI,
                pk.PK_TenPhong    AS TEN_PHONG_KHAM,
                k.K_TenKhoa       AS TEN_KHOA,
                NVL(SUM(sl.SLOT_GioiHan), 0) AS TONG_SLOT,
                NVL(SUM(sl.SLOT_SoDaDK), 0)  AS SO_DA_DK
            FROM PHANCONG pc
            JOIN BACSI b       ON b.BS_MaBacSi      = pc.BS_MaBacSi
            JOIN NGUOIDUNG nd  ON nd.ND_IdNguoiDung = b.ND_IdNguoiDung
            JOIN PHONGKHAM pk  ON pk.PK_MaPK        = pc.PK_MaPK
            LEFT JOIN KHOA k   ON k.K_MaKhoa        = pk.K_MaKhoa
            LEFT JOIN SLOTKHAM sl ON sl.PC_Id       = pc.PC_Id
            WHERE (:kw IS NULL OR :kw = '' OR
                   UPPER(b.BS_MaBacSi) LIKE '%'||UPPER(:kw)||'%' OR
                   UPPER(nd.ND_HoTen)  LIKE '%'||UPPER(:kw)||'%')
              AND (:ngay IS NULL OR TRUNC(pc.PC_Ngay) = TRUNC(:ngay))
            GROUP BY
                pc.PC_Id,
                pc.PC_Ngay,
                pc.PC_CaTruc,
                b.BS_MaBacSi,
                b.BS_ChuyenKhoa,
                nd.ND_HoTen,
                pk.PK_TenPhong,
                k.K_TenKhoa
            ORDER BY pc.PC_Ngay, nd.ND_HoTen, pc.PC_CaTruc";

                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add("kw", string.IsNullOrEmpty(kw) ? (object)DBNull.Value : kw);
                    cmd.Parameters.Add("ngay", OracleDbType.Date).Value = (object)ngay ?? DBNull.Value;

                    using (var r = cmd.ExecuteReader())
                    {
                        int cPCId = r.GetOrdinal("PC_ID");
                        int cNgay = r.GetOrdinal("PC_NGAY");
                        int cCa = r.GetOrdinal("PC_CATRUC");
                        int cMaBS = r.GetOrdinal("BS_MABACSI");
                        int cCK = r.GetOrdinal("BS_CHUYENKHOA");
                        int cTen = r.GetOrdinal("TEN_BAC_SI");
                        int cPhong = r.GetOrdinal("TEN_PHONG_KHAM");
                        int cKhoa = r.GetOrdinal("TEN_KHOA");
                        int cTongSlot = r.GetOrdinal("TONG_SLOT");
                        int cSoDaDK = r.GetOrdinal("SO_DA_DK");

                        while (r.Read())
                        {
                            list.Add(new LichBacSi
                            {
                                PC_Id = r.IsDBNull(cPCId) ? null : r.GetString(cPCId),
                                PC_Ngay = r.GetDateTime(cNgay),
                                CaTruc = r.IsDBNull(cCa) ? null : r.GetString(cCa),
                                BS_MaBacSi = r.IsDBNull(cMaBS) ? null : r.GetString(cMaBS),
                                ChuyenKhoa = r.IsDBNull(cCK) ? null : r.GetString(cCK),
                                TenBacSi = r.IsDBNull(cTen) ? null : r.GetString(cTen),
                                TenPhongKham = r.IsDBNull(cPhong) ? null : r.GetString(cPhong),
                                TenKhoa = r.IsDBNull(cKhoa) ? null : r.GetString(cKhoa),
                                TongSoSlot = r.IsDBNull(cTongSlot) ? 0 : Convert.ToInt32(r.GetDecimal(cTongSlot)),
                                SoDaDangKy = r.IsDBNull(cSoDaDK) ? 0 : Convert.ToInt32(r.GetDecimal(cSoDaDK))
                            });
                        }
                    }
                }
            }

            ViewBag.Keyword = kw;
            ViewBag.Ngay = ngay;

            return View(list);
        }

    }
}