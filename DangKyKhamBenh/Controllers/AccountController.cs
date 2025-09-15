using DangKyKhamBenh.ViewModel;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls.WebParts;
using DangKyKhamBenh.Models;

namespace DangKyKhamBenh.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public ActionResult TestConnection()
        {
            var cs = System.Configuration.ConfigurationManager.ConnectionStrings["OracleDbContext"]?.ConnectionString;
            using (var conn = new Oracle.ManagedDataAccess.Client.OracleConnection(cs))
            {
                conn.Open();
                using (var cmd = new Oracle.ManagedDataAccess.Client.OracleCommand(
                    "SELECT USER, SYS_CONTEXT('USERENV','DB_NAME') FROM dual", conn))
                using (var r = cmd.ExecuteReader())
                {
                    r.Read();
                    return Content($"USER={r.GetString(0)}, DB={r.GetString(1)}");
                }
            }
        }


        private static string BuildId(string prefix, decimal nextVal)
        => prefix + nextVal.ToString("00000000");

        private static string MapRole(string staffType)
            => string.Equals(staffType, "BacSi", StringComparison.OrdinalIgnoreCase) ? "DOCTOR" : "USER";

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult Register(DangKyKhamBenh.ViewModel.Register model)
        {
            if (!ModelState.IsValid) return View(model);

            var cs = System.Configuration.ConfigurationManager
                        .ConnectionStrings["OracleDbContext"]?.ConnectionString;
            System.Diagnostics.Debug.WriteLine(">> USING CS: " + cs);

            try
            {
                using (var conn = new Oracle.ManagedDataAccess.Client.OracleConnection(cs))
                {
                    conn.Open();

                    using (var tx = conn.BeginTransaction())
                    {
                        try
                        {
                            using (var checkCmd = new Oracle.ManagedDataAccess.Client.OracleCommand(@"
                        SELECT COUNT(*) FROM TAIKHOAN 
                        WHERE TRIM(UPPER(TK_UserName)) = TRIM(UPPER(:u))", conn))
                            {
                                checkCmd.Transaction = tx;
                                checkCmd.BindByName = true;
                                checkCmd.Parameters.Add("u", model.Username?.Trim());
                                if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                                {
                                    ModelState.AddModelError("", "Username đã tồn tại.");
                                    tx.Rollback();
                                    return View(model);
                                }
                            }

                            string ndId;
                            using (var seq = new Oracle.ManagedDataAccess.Client.OracleCommand(
                                "SELECT SEQ_NGUOIDUNG.NEXTVAL FROM DUAL", conn))
                            {
                                seq.Transaction = tx;
                                ndId = BuildId("ND", Convert.ToDecimal(seq.ExecuteScalar()));
                            }

                            using (var cmd = new Oracle.ManagedDataAccess.Client.OracleCommand(@"
                        INSERT INTO NGUOIDUNG
                         (ND_IdNguoiDung, ND_HoTen, ND_SoDienThoai, ND_Email, ND_NgaySinh, ND_DiaChiThuongChu)
                         VALUES (:id, :hoten, :sdt, :email, :dob, :addr)", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("id", ndId);
                                cmd.Parameters.Add("hoten", (object)model.Username ?? DBNull.Value);
                                cmd.Parameters.Add("sdt", (object)model.PhoneNumber ?? DBNull.Value);
                                cmd.Parameters.Add("email", (object)model.Email ?? DBNull.Value);
                                cmd.Parameters.Add("dob", (object)model.DateOfBirth ?? DBNull.Value);
                                cmd.Parameters.Add("addr", (object)model.Address ?? DBNull.Value);
                                cmd.ExecuteNonQuery();
                            }

                            string bnId = null, bsId = null;
                            bool isBacSi = string.Equals(model.StaffType, "BacSi", StringComparison.OrdinalIgnoreCase);

                            if (isBacSi)
                            {
                                using (var seq = new Oracle.ManagedDataAccess.Client.OracleCommand(
                                    "SELECT SEQ_BACSI.NEXTVAL FROM DUAL", conn))
                                {
                                    seq.Transaction = tx;
                                    bsId = BuildId("BS", Convert.ToDecimal(seq.ExecuteScalar()));
                                }

                                using (var cmd = new Oracle.ManagedDataAccess.Client.OracleCommand(@"
                            INSERT INTO BACSI (BS_MaBacSi, BS_ChuyenKhoa, BS_ChucDanh, BS_NamKinhNghiem, ND_IdNguoiDung)
                            VALUES (:id, :ck, :cd, :nam, :nd)", conn))
                                {
                                    cmd.Transaction = tx;
                                    cmd.BindByName = true;
                                    cmd.Parameters.Add("id", bsId);
                                    cmd.Parameters.Add("ck", "Tổng quát");
                                    cmd.Parameters.Add("cd", "BS");
                                    cmd.Parameters.Add("nam", 0);
                                    cmd.Parameters.Add("nd", ndId);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            else
                            {
                                using (var seq = new Oracle.ManagedDataAccess.Client.OracleCommand(
                                    "SELECT SEQ_BENHNHAN.NEXTVAL FROM DUAL", conn))
                                {
                                    seq.Transaction = tx;
                                    bnId = BuildId("BN", Convert.ToDecimal(seq.ExecuteScalar()));
                                }

                                using (var cmd = new Oracle.ManagedDataAccess.Client.OracleCommand(@"
                            INSERT INTO BENHNHAN (BN_MaBenhNhan, BN_SoBaoHiemYT, BN_NhomMau, BN_TieuSuBenhAn, ND_IdNguoiDung)
                            VALUES (:id, :bh, :nhom, :tieuSu, :nd)", conn))
                                {
                                    cmd.Transaction = tx;
                                    cmd.BindByName = true;
                                    cmd.Parameters.Add("id", bnId);
                                    cmd.Parameters.Add("bh", DBNull.Value);
                                    cmd.Parameters.Add("nhom", DBNull.Value);
                                    cmd.Parameters.Add("tieuSu", DBNull.Value);
                                    cmd.Parameters.Add("nd", ndId);
                                    cmd.ExecuteNonQuery();
                                }
                            }


                            string tkId;
                            using (var seq = new Oracle.ManagedDataAccess.Client.OracleCommand(
                                "SELECT SEQ_TAIKHOAN.NEXTVAL FROM DUAL", conn))
                            {
                                seq.Transaction = tx;
                                tkId = BuildId("TK", Convert.ToDecimal(seq.ExecuteScalar()));
                            }

                            using (var cmd = new Oracle.ManagedDataAccess.Client.OracleCommand(@"
                        INSERT INTO TAIKHOAN
                         (TK_MaTK, TK_UserName, TK_PassWord, TK_Role, TK_TrangThai, TK_StaffType,
                          BN_MaBenhNhan, BS_MaBacSi, ND_IdNguoiDung)
                         VALUES
                         (:tk, :u, :p, :r, :tt, :st, :bn, :bs, :nd)", conn))
                            {
                                cmd.Transaction = tx;
                                cmd.BindByName = true;
                                cmd.Parameters.Add("tk", tkId);
                                cmd.Parameters.Add("u", model.Username?.Trim());
                                cmd.Parameters.Add("p", model.Password?.Trim());  
                                cmd.Parameters.Add("r", MapRole(model.StaffType));  
                                cmd.Parameters.Add("tt", "Active");
                                cmd.Parameters.Add("st", isBacSi ? (object)@"Bác sĩ" : @"Bệnh nhân");
                                cmd.Parameters.Add("bn", (object)bnId ?? DBNull.Value);
                                cmd.Parameters.Add("bs", (object)bsId ?? DBNull.Value);
                                cmd.Parameters.Add("nd", ndId);
                                cmd.ExecuteNonQuery();
                            }

                            tx.Commit();
                            TempData["Success"] = "Đăng ký thành công, vui lòng đăng nhập.";
                            return RedirectToAction("Login");
                        }
                        catch (Exception ex)
                        {
                            tx.Rollback();
                            ModelState.AddModelError("", "Lỗi đăng ký: " + ex.Message);
                            return View(model);
                        }
                    }
                }
            }
            catch (Oracle.ManagedDataAccess.Client.OracleException ex)
            {
                System.Diagnostics.Debug.WriteLine($">> ORA-{ex.Number}: {ex.Message}");
                ModelState.AddModelError("", $"Kết nối Oracle lỗi ORA-{ex.Number}: {ex.Message}");
                return View(model);
            }
        }

        // GET: Account
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(string user, string password)
        {
            bool isValid = false;
      
            using (var conn = new OracleConnection(
                ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString))
            {
                conn.Open();
                string sql = @"SELECT COUNT(*) 
                               FROM TAIKHOAN 
                               WHERE TRIM(UPPER(TK_UserName)) = TRIM(UPPER(:pUser)) 
                                 AND TRIM(TK_PassWord) = TRIM(:pPassword)";


                using (var cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add(new OracleParameter("pUser", user?.Trim()));
                    cmd.Parameters.Add(new OracleParameter("pPassword", password?.Trim()));

                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    isValid = (count > 0);
                }
            }

            if (isValid)
            {
                Session["User"] = user;
                return RedirectToAction("Index", "Home");
            }
            else
            {
                ViewBag.Error = $"Invalid username or password. (User={user}, Pass={password})";
                return View();
            }
        }




        [HttpGet]
        [AllowAnonymous]
        public ActionResult Register()
        {
            return View(new Register()); 
        }

        //[HttpPost]
        //[AllowAnonymous]
        //[ValidateAntiForgeryToken]
        //public ActionResult Register(Register model)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return View(model);
        //    }
        //    TempData["Success"] = "Đăng ký thành công, vui lòng đăng nhập.";
        //    return RedirectToAction("Login");
        //}





        public ActionResult Logout()
        {
            return View();
        }

    }
}