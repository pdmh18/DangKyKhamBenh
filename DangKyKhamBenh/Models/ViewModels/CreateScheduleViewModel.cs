using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models.ViewModels
{
    public class CreateScheduleViewModel
    {
        public string BS_MaBacSi { get; set; }
        public string PK_MaPK { get; set; }
        public DateTime PC_Ngay { get; set; }
        public string PC_CaTruc { get; set; }
        public List<SlotItem> Slots { get; set; } // Danh sách các khung giờ
        public List<PhongKhamItem> PhongKhamList { get; set; }
        public List<BacSiItem> BacSiList { get; set; }
        public CreateScheduleViewModel()
        {
            Slots = new List<SlotItem>(); // Khởi tạo danh sách khung giờ
        }
    }

    public class SlotItem
    {
        public string GioBD { get; set; }  // Giờ bắt đầu
        public string GioKT { get; set; }  // Giờ kết thúc
        public int GioiHan { get; set; }  // Giới hạn số lượt
    }
}