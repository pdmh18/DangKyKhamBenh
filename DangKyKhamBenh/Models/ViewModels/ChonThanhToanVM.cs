using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models.ViewModels
{
    public enum PaymentMethod
    {
        PayAtHospital = 0,     // MẶC ĐỊNH
        HospitalCard = 1,      // Thẻ khám bệnh BV...
        MoMo = 2,
        MobileBanking = 3,
        DomesticCard = 4,
        InternationalCard = 5,
        QR = 6
    }

    public class ChonThanhToanVM
    {
        public string SlotId { get; set; }
        public string DV_MaDichVu { get; set; }
        public DateTime NgayKham { get; set; }

        public string TenDichVu { get; set; }
        public decimal TienKham { get; set; }

        public PaymentMethod SelectedMethod { get; set; } = PaymentMethod.PayAtHospital;
    }
}