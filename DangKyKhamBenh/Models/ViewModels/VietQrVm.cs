using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DangKyKhamBenh.Models.ViewModels
{
    public class VietQrVm
    {
        public string BankId { get; set; } = "mbbank";          // hoặc "970422"
        public string AccountNo { get; set; } = "977783867979";
        public string AccountName { get; set; } = "TO TRUONG TRUONG THANH";

        public string Template { get; set; } = "compact2";

        public long? Amount { get; set; }        // null = không bắt buộc
        public string AddInfo { get; set; }      // cho phép null/empty (C# 7.3 ok)

        public string QrUrl { get; set; } = "";
    }
}