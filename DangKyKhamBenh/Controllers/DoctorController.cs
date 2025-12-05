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




        [HttpGet]
        public async Task<ActionResult> DashboardTruongKhoa()
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
                    },
                    IsTruongKhoa = Convert.ToBoolean(Session["IsTruongKhoa"]) // Thêm kiểm tra xem bác sĩ có phải trưởng khoa
                };

                return View("~/Views/Doctor/DashboardTruongKhoa.cshtml", vm);
            }
        }



        [HttpGet]
        public async Task<ActionResult> CreateSchedule()
        {
            var maKhoa = (Session["MaKhoa"] ?? "").ToString(); // Lấy mã khoa từ session
            if (string.IsNullOrWhiteSpace(maKhoa))
                return RedirectToAction("Login", "Account");

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            using (var con = new OracleConnection(cs))
            {
                await con.OpenAsync();

                // Lấy danh sách bác sĩ trong khoa
                var bacSiList = new List<BacSiItem>();
                using (var cmd = new OracleCommand(@"
            SELECT BS.BS_MaBacSi, ND.ND_HoTen
            FROM BACSI BS
            JOIN NGUOIDUNG ND ON BS.ND_IdNguoiDung = ND.ND_IdNguoiDung
            WHERE BS.K_MaKhoa = :maKhoa", con))
                {
                    cmd.Parameters.Add(":maKhoa", maKhoa);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            bacSiList.Add(new BacSiItem
                            {
                                BS_MaBacSi = reader.GetString(0),
                                HoTen = reader.GetString(1)
                            });
                        }
                    }
                }

                // Lấy thông tin các phòng khám trong khoa
                var phongKhamList = new List<PhongKhamItem>();
                using (var cmd = new OracleCommand(@"
            SELECT PK_MaPK, PK_TenPhong
            FROM PHONGKHAM
            WHERE K_MaKhoa = :maKhoa", con))
                {
                    cmd.Parameters.Add(":maKhoa", maKhoa);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            phongKhamList.Add(new PhongKhamItem
                            {
                                PK_MaPK = reader.GetString(0),
                                TenPhong = reader.GetString(1)
                            });
                        }
                    }
                }

                // Trả về view với thông tin bác sĩ và phòng khám
                var viewModel = new CreateScheduleViewModel
                {
                    BacSiList = bacSiList,
                    PhongKhamList = phongKhamList
                };

                return View(viewModel);
            }
        }



        [HttpPost]
        public async Task<ActionResult> CreateSchedule(CreateScheduleViewModel model)
        {
            var maKhoa = (Session["MaKhoa"] ?? "").ToString(); // Lấy mã khoa từ session
            if (string.IsNullOrWhiteSpace(maKhoa))
                return RedirectToAction("Login", "Account");

            var cs = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
            using (var con = new OracleConnection(cs))
            {
                await con.OpenAsync();

                using (var tx = con.BeginTransaction())  // Bắt đầu transaction
                {
                    try
                    {
                        // Sinh mã PC_ID kế tiếp
                        string pcId = NextId(con, tx, "PHANCONG", "PC_ID", "PC");

                        // Lưu thông tin phân công vào bảng PHANCONG
                        using (var cmd = new OracleCommand(@"
                            INSERT INTO PHANCONG (PC_Id, BS_MaBacSi, PK_MaPK, PC_Ngay, PC_CaTruc)
                            VALUES (:PC_Id, :BS_MaBacSi, :PK_MaPK, :PC_Ngay, :PC_CaTruc)", con))
                        {
                            cmd.Parameters.Add(":PC_Id", pcId);  // ID phân công
                            cmd.Parameters.Add(":BS_MaBacSi", model.BS_MaBacSi);
                            cmd.Parameters.Add(":PK_MaPK", model.PK_MaPK);
                            cmd.Parameters.Add(":PC_Ngay", model.PC_Ngay);
                            cmd.Parameters.Add(":PC_CaTruc", model.PC_CaTruc);
                            await cmd.ExecuteNonQueryAsync();
                        }
                        await InsertSlotsByCaAsync(con, tx, pcId, model.PC_CaTruc, model.GioiHanMoiSlot);

                        // Thêm các slot thời gian vào bảng SLOTKHAM
                        //foreach (var slot in model.Slots)
                        //{
                        //    using (var cmd = new OracleCommand(@"
                        //        INSERT INTO SLOTKHAM (SLOT_Id, PC_Id, SLOT_GioBD, SLOT_GioKT, SLOT_GioiHan, SLOT_SoDaDK)
                        //        VALUES (:SLOT_Id, :PC_Id, :SLOT_GioBD, :SLOT_GioKT, :SLOT_GioiHan, :SLOT_SoDaDK)", con))
                        //    {
                        //        cmd.Parameters.Add(":SLOT_Id", "SLOT" + DateTime.Now.ToString("yyyyMMddHHmmss")); // ID slot mới
                        //        cmd.Parameters.Add(":PC_Id", pcId);  // ID phân công
                        //        cmd.Parameters.Add(":SLOT_GioBD", slot.GioBD);
                        //        cmd.Parameters.Add(":SLOT_GioKT", slot.GioKT);
                        //        cmd.Parameters.Add(":SLOT_GioiHan", slot.GioiHan);
                        //        cmd.Parameters.Add(":SLOT_SoDaDK", 0); // Số lượt đăng ký ban đầu là 0
                        //        await cmd.ExecuteNonQueryAsync();
                        //    }
                        //}

                        // Commit transaction sau khi thành công
                        tx.Commit();

                        // Quay lại trang quản lý lịch trực
                        return RedirectToAction("DashboardTruongKhoa", "Doctor");
                    }
                    catch (Exception ex)
                    {
                        // Nếu có lỗi, rollback transaction
                        tx.Rollback();
                        // Xử lý lỗi
                        ViewBag.Error = "Lỗi khi lưu dữ liệu: " + ex.Message;
                        return View(model);
                    }
                }
            }
        }


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

        private static async Task InsertSlotAsync(
    OracleConnection con, OracleTransaction tx,
    string pcId, string gioBD, string gioKT, int gioiHan, int soDaDK)
        {
            // SLOT_Id CHAR(10) => nên sinh kiểu SL00000001...
            string slotId = NextId(con, tx, "SLOTKHAM", "SLOT_ID", "SL");

            using (var cmd = new OracleCommand(@"
        INSERT INTO SLOTKHAM (SLOT_Id, PC_Id, SLOT_GioBD, SLOT_GioKT, SLOT_GioiHan, SLOT_SoDaDK)
        VALUES (:SLOT_Id, :PC_Id, :SLOT_GioBD, :SLOT_GioKT, :SLOT_GioiHan, :SLOT_SoDaDK)", con))
            {
                cmd.Transaction = tx;

                cmd.Parameters.Add(":SLOT_Id", slotId);
                cmd.Parameters.Add(":PC_Id", pcId);
                cmd.Parameters.Add(":SLOT_GioBD", gioBD);
                cmd.Parameters.Add(":SLOT_GioKT", gioKT);
                cmd.Parameters.Add(":SLOT_GioiHan", gioiHan);
                cmd.Parameters.Add(":SLOT_SoDaDK", soDaDK);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private static async Task InsertSlotsByCaAsync(
            OracleConnection con, OracleTransaction tx,
            string pcId, string caTruc, int gioiHanMoiSlot)
        {
            if (string.Equals(caTruc, "Sáng", StringComparison.OrdinalIgnoreCase))
            {
                await InsertSlotAsync(con, tx, pcId, "06:30", "07:30", gioiHanMoiSlot, 0);
                await InsertSlotAsync(con, tx, pcId, "07:30", "08:30", gioiHanMoiSlot, 0);
                await InsertSlotAsync(con, tx, pcId, "08:30", "09:30", gioiHanMoiSlot, 0);
                await InsertSlotAsync(con, tx, pcId, "09:30", "10:30", gioiHanMoiSlot, 0);
                await InsertSlotAsync(con, tx, pcId, "10:30", "11:30", gioiHanMoiSlot, 0);
                return;
            }

            // Chiều
            await InsertSlotAsync(con, tx, pcId, "13:00", "14:00", gioiHanMoiSlot, 0);
            await InsertSlotAsync(con, tx, pcId, "14:00", "15:00", gioiHanMoiSlot, 0);
            await InsertSlotAsync(con, tx, pcId, "15:00", "16:00", gioiHanMoiSlot, 0);
        }

    }
}
