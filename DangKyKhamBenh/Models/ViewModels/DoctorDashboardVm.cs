using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models.ViewModels
{
    public class DoctorDashboardVm
    {
        public string BS_MaBacSi { get; set; }
        public string TenBacSi { get; set; }

        public IEnumerable<LichTrucItem> LichTruc7Ngay { get; set; }
        public ThongKeBlock ThongKe { get; set; }

        public bool IsTruongKhoa { get; set; }
        //
        public IEnumerable<LichHenItem> LichHenHomNay { get; set; }
        public IEnumerable<HangDoiItem> HangDoiKham { get; set; }
        public IEnumerable<KetQuaCLSItem> KetQuaCanLamSangMoi { get; set; }
        public int SoHenHomNay { get; set; }
        public int SoBenhNhanCho { get; set; }
        public int SoKetQuaMoi { get; set; }
        public KpiSeries KPI_30Ngay { get; set; } = new KpiSeries();
    }

    public class LichTrucItem
    {
        public string PC_Id { get; set; }      // <-- thêm
        public DateTime Ngay { get; set; }
        public string CaTruc { get; set; }
        public string PhongKham { get; set; }
        public string Khoa { get; set; }
        public string ViTri { get; set; }
    }


    public class ThongKeBlock
    {
        public int SoBenhNhanDaKham_30Ngay { get; set; }
        public decimal DoanhThuDuKien_30Ngay { get; set; }
        public int SoChiDinhDichVu_30Ngay { get; set; }

        //
        public int BinhQuanNgay { get; set; }
        public decimal DonGiaTrungBinh { get; set; }
        public int TiLeCLS { get; set; }
    }
    public class SlotKhamItem
    {
        public string SlotId { get; set; }
        public string GioBD { get; set; }    // "HH:mm"
        public string GioKT { get; set; }
        public int GioiHan { get; set; }
        public int SoDaDK { get; set; }
    }

    public class ScheduleDetailVm
    {
        public string PC_Id { get; set; }
        public DateTime Ngay { get; set; }
        public string CaTruc { get; set; }

        public string PhongKham { get; set; }
        public string Khoa { get; set; }
        public string ViTri { get; set; }

        public IList<SlotKhamItem> Slots { get; set; } = new List<SlotKhamItem>();
    }

}