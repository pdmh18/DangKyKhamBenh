using Oracle.ManagedDataAccess.Client;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using System.Web.Mvc;
using System;
using DangKyKhamBenh.Models.ViewModels;

namespace DangKyKhamBenh.Controllers
{
    [DoctorOnly]
    public class DoctorController : Controller
    {
        public async Task<ActionResult> Dashboard()
        {
            var maBs = (Session["BS_MaBacSi"] ?? "").ToString();
            if (string.IsNullOrWhiteSpace(maBs))
                return RedirectToAction("Login", "Account");

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;

            using (var con = new OracleConnection(cs))
            {
                await con.OpenAsync();

                // 1) Tên bác sĩ
                string ten;
                using (var cmd = new OracleCommand(@"
                    SELECT NVL(ND.ND_HoTen, BS.BS_MaBacSi)
                    FROM BACSI BS 
                    JOIN NGUOIDUNG ND ON ND.ND_IdNguoiDung = BS.ND_IdNguoiDung
                    WHERE BS.BS_MaBacSi = :ma", con))
                {
                    cmd.Parameters.Add(":ma", maBs);
                    ten = (await cmd.ExecuteScalarAsync())?.ToString();
                }

                // 2) Lịch trực 7 ngày
                var lich = new List<LichTrucItem>();
                using (var cmd = new OracleCommand(@"
    SELECT 
        pc.PC_Id,
        pc.PC_Ngay,
        pc.PC_CaTruc,
        pk.PK_TenPhong,
        k.K_TenKhoa,
        pk.PK_ViTri
    FROM PHANCONG pc
    JOIN PHONGKHAM pk ON pk.PK_MaPK = pc.PK_MaPK
    JOIN KHOA k       ON k.K_MaKhoa = pk.K_MaKhoa
    WHERE pc.BS_MaBacSi = :ma
      AND pc.PC_Ngay BETWEEN TRUNC(SYSDATE) AND TRUNC(SYSDATE) + 6
    ORDER BY pc.PC_Ngay, pc.PC_CaTruc", con))
                {
                    cmd.Parameters.Add(":ma", maBs);
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        while (await rd.ReadAsync())
                        {
                            lich.Add(new LichTrucItem
                            {
                                PC_Id = rd.GetString(0),
                                Ngay = rd.GetDateTime(1),
                                CaTruc = rd.IsDBNull(2) ? null : rd.GetString(2),
                                PhongKham = rd.IsDBNull(3) ? null : rd.GetString(3),
                                Khoa = rd.IsDBNull(4) ? null : rd.GetString(4),
                                ViTri = rd.IsDBNull(5) ? null : rd.GetString(5)
                            });
                        }
                    }
                }

                // 3) Thống kê 30 ngày
                int soBn;
                using (var cmd = new OracleCommand(@"
                    SELECT COUNT(DISTINCT BN_MaBenhNhan)
                    FROM PHIEUDICHVU 
                    WHERE BS_MaBacSi = :ma AND PDV_NgayChiDinh >= TRUNC(SYSDATE) - 30", con))
                {
                    cmd.Parameters.Add(":ma", maBs);
                    soBn = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                int soDv;
                using (var cmd = new OracleCommand(@"
                    SELECT COUNT(*) 
                    FROM PHIEUDICHVU
                    WHERE BS_MaBacSi = :ma 
                      AND PDV_NgayChiDinh >= TRUNC(SYSDATE) - 30", con))
                {
                    cmd.Parameters.Add(":ma", maBs);
                    soDv = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                // Doanh thu: tránh double-count
                decimal doanhThu;
                using (var cmd = new OracleCommand(@"
                    SELECT NVL(SUM(hd.HD_TongTien),0)
                    FROM HOADON hd
                    WHERE hd.DK_MaPhieuKham IN (
                        SELECT DISTINCT dk.DK_MaPhieuKham
                        FROM PHIEUDANGKY dk
                        WHERE EXISTS (
                            SELECT 1 FROM PHIEUDICHVU pdv
                            WHERE pdv.DK_MaPhieuKham = dk.DK_MaPhieuKham
                              AND pdv.BS_MaBacSi = :ma
                              AND pdv.PDV_NgayChiDinh >= TRUNC(SYSDATE) - 30
                        )
                    )", con))
                {
                    cmd.Parameters.Add(":ma", maBs);
                    doanhThu = Convert.ToDecimal(await cmd.ExecuteScalarAsync());
                }


                var vm = new DoctorDashboardVm
                {
                    BS_MaBacSi = maBs,
                    TenBacSi = ten,
                    LichTruc7Ngay = lich,
                    ThongKe = new ThongKeBlock
                    {
                        SoBenhNhanDaKham_30Ngay = soBn,
                        DoanhThuDuKien_30Ngay = doanhThu,
                        SoChiDinhDichVu_30Ngay = soDv
                    }
                };

                return View("~/Views/Doctor/Dashboard.cshtml", vm);
            }
        }

        [HttpGet]
        public async Task<ActionResult> ScheduleDetail(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return RedirectToAction("Dashboard");

            var maBs = (Session["BS_MaBacSi"] ?? "").ToString();
            if (string.IsNullOrWhiteSpace(maBs))
                return RedirectToAction("Login", "Account");

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            using (var con = new OracleConnection(cs))
            {
                await con.OpenAsync();

                // 1) Lấy info PHANCONG + phòng/khoa
                ScheduleDetailVm vm = null;
                using (var cmd = new OracleCommand(@"
            SELECT 
                pc.PC_Id,
                pc.PC_Ngay,
                pc.PC_CaTruc,
                pk.PK_TenPhong,
                k.K_TenKhoa,
                pk.PK_ViTri
            FROM PHANCONG pc
            JOIN PHONGKHAM pk ON pk.PK_MaPK = pc.PK_MaPK
            JOIN KHOA k       ON k.K_MaKhoa = pk.K_MaKhoa
            WHERE pc.PC_Id = :id
              AND pc.BS_MaBacSi = :ma", con))
                {
                    cmd.Parameters.Add(":id", id);
                    cmd.Parameters.Add(":ma", maBs);

                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (await rd.ReadAsync())
                        {
                            vm = new ScheduleDetailVm
                            {
                                PC_Id = rd.GetString(0),
                                Ngay = rd.GetDateTime(1),
                                CaTruc = rd.IsDBNull(2) ? null : rd.GetString(2),
                                PhongKham = rd.IsDBNull(3) ? null : rd.GetString(3),
                                Khoa = rd.IsDBNull(4) ? null : rd.GetString(4),
                                ViTri = rd.IsDBNull(5) ? null : rd.GetString(5)
                            };
                        }
                    }
                }

                if (vm == null)
                    return HttpNotFound(); // hoặc RedirectToAction("Dashboard")

                // 2) Lấy danh sách slot từ SLOTKHAM
                vm.Slots = new List<SlotKhamItem>();
                using (var cmd = new OracleCommand(@"
            SELECT 
                SLOT_Id,
                SLOT_GioBD,
                SLOT_GioKT,
                NVL(SLOT_GioiHan,0),
                NVL(SLOT_SoDaDK,0)
            FROM SLOTKHAM
            WHERE PC_Id = :id
            ORDER BY SLOT_GioBD", con))
                {
                    cmd.Parameters.Add(":id", id);
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        while (await rd.ReadAsync())
                        {
                            vm.Slots.Add(new SlotKhamItem
                            {
                                SlotId = rd.GetString(0),
                                GioBD = rd.IsDBNull(1) ? null : rd.GetString(1),
                                GioKT = rd.IsDBNull(2) ? null : rd.GetString(2),
                                GioiHan = rd.GetInt32(3),
                                SoDaDK = rd.GetInt32(4)
                            });
                        }
                    }
                }

                return View("~/Views/Doctor/ScheduleDetail.cshtml", vm);
            }
        }

    }
}
