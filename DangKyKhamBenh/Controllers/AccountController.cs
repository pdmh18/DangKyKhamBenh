using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls.WebParts;

namespace DangKyKhamBenh.Controllers
{
    public class AccountController : Controller
    {
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




        public ActionResult Register()
        {
            return View();
        }

        public ActionResult Logout()
        {
            return View();
        }
    }
}