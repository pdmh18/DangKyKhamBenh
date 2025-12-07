using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models.ViewModels
{
    public class SearchResultItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Info { get; set; }
        public string SoBaoHiemYT { get; set; }
        public string NhomMau { get; set; }
        public string TieuSuBenhAn { get; set; }

        public string PDV_MaPhieu { get; set; }
        public DateTime PDV_NgayChiDinh { get; set; }
        public string BN_MaBenhNhan { get; set; }


        public string NdIdNguoiDung { get; set; }
        public string NdHoTen { get; set; }
        public string NdSoDienThoai { get; set; }
        public string NdEmail { get; set; }
        public string NdCccd { get; set; }
        public DateTime? NdNgaySinh { get; set; }
        public string NdGioiTinh { get; set; }
        public string NdQuocGia { get; set; }
        public string NdDanToc { get; set; }
        public string NdNgheNghiep { get; set; }
        public string NdTinhThanh { get; set; }
        public string NdQuanHuyen { get; set; }
        public string NdPhuongXa { get; set; }
        public string NdDiaChiThuongChu { get; set; }
    }

}