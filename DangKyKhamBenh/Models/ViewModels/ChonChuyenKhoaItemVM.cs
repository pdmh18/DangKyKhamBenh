using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models.ViewModels
{
    public class ChonChuyenKhoaItemVM
    {
        public string DV_MaDichVu { get; set; }      
        public string TenChuyenKhoa { get; set; }    
        public string MoTa { get; set; }   // xử lí trên giao diện
        public decimal GiaTien { get; set; }       
    }

    public class ChonChuyenKhoaVM
    {
        public string TuKhoa { get; set; }     
        public List<ChonChuyenKhoaItemVM> Items { get; set; }

        public ChonChuyenKhoaVM()
        {
            Items = new List<ChonChuyenKhoaItemVM>();
        }
    }
}