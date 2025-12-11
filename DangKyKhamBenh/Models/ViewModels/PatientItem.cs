using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models.ViewModels
{
    public class PatientItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string SoBaoHiemYT { get; set; }
        public string NhomMau { get; set; }
        public string TieuSuBenhAn { get; set; }
    }
}