using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models.ViewModels
{
    public class PhieuKhamBenhVM
    {
        public string PdvId { get; set; }
        public string MaGiaoDich { get; set; } // tuỳ payment method

        public string TenDichVu { get; set; }
        public string TenPhong { get; set; }
        public string ViTri { get; set; }

        public int STT { get; set; }

        public string HoTen { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string GioiTinh { get; set; }
        public string TinhTP { get; set; }

        public DateTime NgayKham { get; set; }
        public string Buoi { get; set; }      // Sáng/Chiều...
        public string GioBD { get; set; }
        public string GioKT { get; set; }

        public decimal TienKham { get; set; }
        public string DoiTuong { get; set; }  // Thu phí / BHYT
        public string SoHoSo { get; set; }    // gợi ý: BN_MaBenhNhan
    }
}