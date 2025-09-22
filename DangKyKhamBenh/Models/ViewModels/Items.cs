using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models.ViewModels
{
    public class Items
    {
    }
    public class LichHenItem
    {
        public DateTime ThoiGian { get; set; }      // giả định 08:00 nếu không có giờ
        public string MaHoSo { get; set; }          // DK_MaPhieuKham
        public string MaBenhNhan { get; set; }
        public string TenBenhNhan { get; set; }
        public string SDT { get; set; }
        public string LyDo { get; set; }
        public string TrangThai { get; set; }
    }

    public class HangDoiItem
    {
        public int STT { get; set; }
        public string SoPhieu { get; set; }         // DK_MaPhieuKham
        public string MaBenhNhan { get; set; }
        public string TenBenhNhan { get; set; }
        public DateTime ThoiGianDangKy { get; set; }
        public string DoUuTien { get; set; }        // demo: tên khoa
    }

    public class KetQuaCLSItem
    {
        public string Id { get; set; }              // KQ_MaKQ
        public DateTime ThoiGian { get; set; }      // KQ_NgayTraKQ
        public string MaBenhNhan { get; set; }
        public string TenBenhNhan { get; set; }
        public string LoaiXetNghiem { get; set; }   // CLS_LoaiXetNghiem
        public string TomTat { get; set; }          // KQ_KetQuaChiTiet
    }

    public class KpiSeries
    {
        public List<string> Labels { get; set; } = new List<string>();
        public List<int> SoLuotDichVu { get; set; } = new List<int>();
        public List<decimal> DoanhThuNgay { get; set; } = new List<decimal>();
    }
}