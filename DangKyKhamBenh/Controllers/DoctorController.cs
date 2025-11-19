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
                    SELECT pc.PC_Ngay, pk.PK_TenPhong, k.K_TenKhoa, pk.PK_ViTri
                    FROM PHANCONG pc
                    JOIN PHONGKHAM pk ON pk.PK_MaPK = pc.PK_MaPK
                    JOIN KHOA k ON k.K_MaKhoa = pk.K_MaKhoa
                    WHERE pc.BS_MaBacSi = :ma
                      AND pc.PC_Ngay BETWEEN TRUNC(SYSDATE) AND TRUNC(SYSDATE)+6
                    ORDER BY pc.PC_Ngay, pc.PC_CaTruc", con))
                {
                    cmd.Parameters.Add(":ma", maBs);
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        while (await rd.ReadAsync())
                        {
                            lich.Add(new LichTrucItem
                            {
                                Ngay = rd.GetDateTime(0),
                                CaTruc = rd.GetString(1),
                                PhongKham = rd.GetString(2),
                                Khoa = rd.GetString(3),
                                ViTri = rd.GetString(4)
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
    }
}
