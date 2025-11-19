using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models.ViewModels
{
    public class NgayKhamItemVM
    {
        public DateTime Date { get; set; }
        public bool IsSelectable { get; set; }   // ngày màu xanh
        public bool IsToday { get; set; }        // hôm nay
    }

    public class ChonNgayKhamVM
    {
        public string DV_MaDichVu { get; set; }

        public int Year { get; set; }
        public int Month { get; set; }

        public List<NgayKhamItemVM> Days { get; set; } = new List<NgayKhamItemVM>();
    }
}