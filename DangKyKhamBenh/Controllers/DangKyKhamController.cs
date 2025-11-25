using DangKyKhamBenh.Models.ViewModels;
using DangKyKhamBenh.Services;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace DangKyKhamBenh.Controllers
{
    public class DangKyKhamController : Controller
    {
        // GET: DangKyKham
        private readonly CaesarCipher _caesarCipher;
        private readonly RsaService _rsaService;
        private readonly HybridService _hybridService;

        public DangKyKhamController()
        {
            _caesarCipher = new CaesarCipher();
            _rsaService = new RsaService();
            _hybridService = new HybridService();
        }

        //public ActionResult Index()
        //{
        //    var userId = Session["ND_IdNguoiDung"]?.ToString();
        //    if (string.IsNullOrEmpty(userId))
        //    {
        //        TempData["Err"] = "Bạn chưa đăng nhập.";
        //        return RedirectToAction("Login", "Account");
        //    }

        //    var model = new List<BenhNhan>();

        //    var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
        //    using (var conn = new OracleConnection(cs))
        //    {
        //        conn.Open();

        //        var sql = @"
        //            SELECT BN_MaBenhNhan, ND_HoTen, ND_SoDienThoai
        //            FROM BENHNHAN bn
        //            JOIN NGUOIDUNG nd ON bn.ND_IdNguoiDung = nd.ND_IdNguoiDung
        //            WHERE bn.ND_IdNguoiDung = :userId";

        //        using (var cmd = new OracleCommand(sql, conn))
        //        {
        //            cmd.Parameters.Add("userId", OracleDbType.Varchar2).Value = userId;
        //            using (var reader = cmd.ExecuteReader())
        //            {
        //                while (reader.Read())
        //                {
        //                    model.Add(new BenhNhan
        //                    {
        //                        BN_MaBenhNhan = reader["BN_MaBenhNhan"].ToString(),
        //                        ND_HoTen = reader["ND_HoTen"].ToString(),
        //                        ND_SoDienThoai = reader["ND_SoDienThoai"].ToString()
        //                    });
        //                }

        //            }
        //        }
        //    }

        //    try
        //    {
        //        foreach (var item in model)
        //        {
        //            item.ND_SoDienThoai = _rsaService.Decrypt(item.ND_SoDienThoai);
        //            System.Diagnostics.Debug.WriteLine("Số điện thoại sau giải mã: " + item.ND_SoDienThoai);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        TempData["Err"] = "Lỗi giải mã số điện thoại: " + ex.Message;
        //        return View(model);
        //    }
        //    return View(model);
        //}
        public ActionResult Index()
        {
            var userId = Session["ND_IdNguoiDung"]?.ToString();
            if (string.IsNullOrEmpty(userId))
            {
                TempData["Err"] = "Bạn chưa đăng nhập.";
                return RedirectToAction("Login", "Account");
            }

            var model = new List<BenhNhan>();

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            using (var conn = new OracleConnection(cs))
            {
                conn.Open();

                var sql = @"
                    SELECT BN_MaBenhNhan, ND_HoTen, ND_SoDienThoai, 
                           BN_SoBaoHiemYT, BN_TieuSuBenhAn, ND_CCCD, ND_GioiTinh, 
                           ND_QuocGia, ND_DanToc, ND_NgheNghiep, ND_TinhThanh, 
                           ND_QuanHuyen, ND_PhuongXa
                    FROM BENHNHAN bn
                    JOIN NGUOIDUNG nd ON bn.ND_IdNguoiDung = nd.ND_IdNguoiDung
                    WHERE bn.ND_IdNguoiDung = :userId";

                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.Parameters.Add("userId", OracleDbType.Varchar2).Value = userId;
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var benhNhan = new BenhNhan
                            {
                                BN_MaBenhNhan = reader["BN_MaBenhNhan"].ToString(),
                                ND_HoTen = reader["ND_HoTen"].ToString(),
                                ND_SoDienThoai = reader["ND_SoDienThoai"].ToString(),
                                BN_SoBaoHiemYT = reader["BN_SoBaoHiemYT"] as string,
                                BN_TieuSuBenhAn = reader["BN_TieuSuBenhAn"] as string,
                                ND_CCCD = reader["ND_CCCD"] as string,
                                ND_GioiTinh = reader["ND_GioiTinh"] as string,
                                ND_QuocGia = reader["ND_QuocGia"] as string,
                                ND_DanToc = reader["ND_DanToc"] as string,
                                ND_NgheNghiep = reader["ND_NgheNghiep"] as string,
                                ND_TinhThanh = reader["ND_TinhThanh"] as string,
                                ND_QuanHuyen = reader["ND_QuanHuyen"] as string,
                                ND_PhuongXa = reader["ND_PhuongXa"] as string
                            };
                            model.Add(benhNhan);
                        }
                    }
                }
            }

            // Kiểm tra hồ sơ đầy đủ
            var hasCompleteProfile = model.All(item =>
                !string.IsNullOrEmpty(item.BN_SoBaoHiemYT) &&
                !string.IsNullOrEmpty(item.BN_TieuSuBenhAn) &&
                !string.IsNullOrEmpty(item.ND_CCCD) &&
                !string.IsNullOrEmpty(item.ND_GioiTinh) &&
                !string.IsNullOrEmpty(item.ND_QuocGia) &&
                !string.IsNullOrEmpty(item.ND_DanToc) &&
                !string.IsNullOrEmpty(item.ND_NgheNghiep) &&
                !string.IsNullOrEmpty(item.ND_TinhThanh) &&
                !string.IsNullOrEmpty(item.ND_QuanHuyen) &&
                !string.IsNullOrEmpty(item.ND_PhuongXa));

            // Nếu hồ sơ chưa đầy đủ, hiển thị thông báo yêu cầu tạo hồ sơ
            if (!hasCompleteProfile)
            {
                TempData["Err"] = "Bạn chưa có hồ sơ đầy đủ. Vui lòng tạo hồ sơ.";
                return View(model);  // Quay lại trang với thông báo
            }

            try
            {
                foreach (var item in model)
                {
                    item.ND_SoDienThoai = _rsaService.Decrypt(item.ND_SoDienThoai);
                    System.Diagnostics.Debug.WriteLine("Số điện thoại sau giải mã: " + item.ND_SoDienThoai);
                }
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Lỗi giải mã số điện thoại: " + ex.Message;
                return View(model);
            }

            return View(model); // Trả về danh sách hồ sơ
        }



        public ActionResult ChonChuyenKhoa(string bnId,string TuKhoa = null)
        {
            var model = new ChonChuyenKhoaVM { TuKhoa = TuKhoa };
            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;

            using (var conn = new OracleConnection(cs))
            {
                conn.Open();

                var sql = @"
                SELECT dv.DV_MaDichVu,
                       k.K_TenKhoa,
                       dv.DV_TenDichVu,
                       dv.DV_GiaTien
                FROM   DICHVU dv
                JOIN   KHOA k ON k.K_MaKhoa = dv.K_MaKhoa
                WHERE  (:kw IS NULL 
                        OR LOWER(k.K_TenKhoa) LIKE '%' || LOWER(:kw) || '%')
                ORDER BY k.K_TenKhoa";

                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add("kw",
                        string.IsNullOrWhiteSpace(TuKhoa) ? (object)DBNull.Value : TuKhoa);

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            model.Items.Add(new ChonChuyenKhoaItemVM
                            {
                                DV_MaDichVu = r["DV_MaDichVu"].ToString(),
                                TenChuyenKhoa = r["K_TenKhoa"].ToString(),
                                MoTa = r["DV_TenDichVu"].ToString(),   // dòng nhỏ phía dưới
                                GiaTien = Convert.ToDecimal(r["DV_GiaTien"])
                            });
                        }
                    }
                }
            }

            return View(model);
        }

        public ActionResult DangKyTheoChuyenKhoa(string dvId)
        {
            return RedirectToAction("ChonNgayKham", new { dvId });
        }
        /////////////////////////////////////////////////////////////
        public ActionResult ChonNgayKham(string dvId, int? year, int? month)
        {
            if (string.IsNullOrEmpty(dvId))
                return RedirectToAction("ChonChuyenKhoa");

            var today = DateTime.Today;

            int y = year ?? today.Year;
            int m = month ?? today.Month;

            var firstDayOfMonth = new DateTime(y, m, 1);
            int daysInMonth = DateTime.DaysInMonth(y, m);

            var model = new ChonNgayKhamVM
            {
                DV_MaDichVu = dvId,
                Year = y,
                Month = m
            };

            for (int d = 1; d <= daysInMonth; d++)
            {
                var date = new DateTime(y, m, d);

               
                bool selectable = date >= today;

                model.Days.Add(new NgayKhamItemVM
                {
                    Date = date,
                    IsSelectable = selectable,
                    IsToday = date == today
                });
            }

            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult XacNhanNgayKham(string dvId, DateTime ngayKham)
        {
           
            return RedirectToAction("ChonKhungGio", new { dvId = dvId, ngay = ngayKham.ToString("yyyy-MM-dd") });
        }



    }
}