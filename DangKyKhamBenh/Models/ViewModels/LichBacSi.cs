using System;

namespace DangKyKhamBenh.Models.ViewModels
{
    public class LichBacSi
    {
        public string PC_Id { get; set; }          // Mã phân công
        public DateTime PC_Ngay { get; set; }      // Ngày trực
        public string CaTruc { get; set; }         // Ca trực (Sáng/Chiều/...)

        public string BS_MaBacSi { get; set; }     // Mã bác sĩ
        public string TenBacSi { get; set; }       // Họ tên bác sĩ
        public string ChuyenKhoa { get; set; }     // Chuyên khoa

        public string TenKhoa { get; set; }        // Khoa
        public string TenPhongKham { get; set; }   // Phòng khám

        public int TongSoSlot { get; set; }        // Tổng số lượt khám (sum SLOT_GioiHan)
        public int SoDaDangKy { get; set; }        // Số lượt đã đăng ký (sum SLOT_SoDaDK)
    }
}
