using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models.ViewModels
{
    public class PhieuTongHopVM
    {
        // dữ liệu lịch khám
        public string SlotId { get; set; }
        public string DV_MaDichVu { get; set; }
        public DateTime NgayKham { get; set; }

        public string TenDichVu { get; set; }
        public decimal TienKham { get; set; }
        public string GioBD { get; set; }
        public string GioKT { get; set; }
        public string TenPhong { get; set; }
        public string ViTri { get; set; }
        public string TenBacSi { get; set; }

        // lựa chọn BHYT
        public string BhytCase { get; set; }          // "1".."4"
        public bool BaoLanhVienPhi { get; set; }      // true/false

        // thông tin bệnh nhân (lấy theo user đăng nhập)
        public string HoTen { get; set; }
        public string SoDienThoai { get; set; }
        public string CCCD { get; set; }
        public string SoBHYT { get; set; }

        // text hiển thị
        public string BhytCaseText { get; set; }
        public string BaoLanhText => BaoLanhVienPhi ? "Có" : "Không";
    }
}