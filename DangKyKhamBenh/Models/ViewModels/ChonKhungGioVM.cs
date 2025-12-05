using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models.ViewModels
{
    public class ChonKhungGioVM
    {
        public string DV_MaDichVu { get; set; }
        public string K_MaKhoa { get; set; }
        public string TenKhoa { get; set; }

        public DateTime NgayDangChon { get; set; }
        public DateTime Ngay { get; set; }  // Thêm thuộc tính Ngay

        public List<DateTime> NgayCoLich { get; set; } = new List<DateTime>();

        public List<BacSiKhungGioCardVM> Cards { get; set; } = new List<BacSiKhungGioCardVM>();
        public List<CaKhamBlockVM> Blocks { get; set; } = new List<CaKhamBlockVM>();
    }


    public class BacSiKhungGioCardVM
    {
        public string PC_Id { get; set; }
        public string BS_MaBacSi { get; set; }
        public string HoTen { get; set; }

        public string PK_MaPK { get; set; }
        public string TenPhong { get; set; }
        public string ViTri { get; set; }

        public string CaTruc { get; set; } // "Sáng" / "Chiều"
        public List<SlotChonVM> Slots { get; set; } = new List<SlotChonVM>();
    }

    public class SlotChonVM
    {
        public string SlotId { get; set; }
        public string GioBD { get; set; } // "06:30"
        public string GioKT { get; set; } // "07:30"
        public int GioiHan { get; set; }
        public int SoDaDK { get; set; }
        public bool ConCho => SoDaDK < GioiHan;
    }
    public class CaKhamBlockVM
    {
        public string PC_Id { get; set; }
        public string CaTruc { get; set; }
        public string TenPhong { get; set; }
        public string ViTri { get; set; }
        public string BS_MaBacSi { get; set; }
        public string TenBacSi { get; set; }
        public List<SlotChonVM> Slots { get; set; } = new List<SlotChonVM>();
    }
}