using DangKyKhamBenh.Models.ViewModels;
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

                // QUY TẮC CHỌN NGÀY (tạm thời):
                // - Chỉ cho chọn từ hôm nay trở đi
                // - Bạn muốn thêm logic theo PHANCONG thì gắn ở đây
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
            // ở đây bạn có thể:
            // - lưu tạm vào Session
            // - hoặc chuyển tiếp sang chọn giờ khám
            // - hoặc hiện màn hình xác nhận

            // Ví dụ: chuyển sang chọn khung giờ:
            return RedirectToAction("ChonKhungGio", new { dvId = dvId, ngay = ngayKham.ToString("yyyy-MM-dd") });
        }



    }
}