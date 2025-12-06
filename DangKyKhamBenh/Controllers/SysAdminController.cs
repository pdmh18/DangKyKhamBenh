using DangKyKhamBenh.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace DangKyKhamBenh.Controllers
{
    public class SysAdminController : Controller
    {
        private SysAdmin sysAdminService = new SysAdmin();

        public ActionResult Index()
        {
            DataTable users = sysAdminService.GetAllUsers();
            ViewBag.Users = users;

            return View();
        }

        public ActionResult UserRoles(string username)
        {

            DataTable userRoles = sysAdminService.GetUserRoles(username);
            ViewBag.UserRoles = userRoles;

            return View();
        }
        public ActionResult CheckUserRole(string username, string role)
        {
            bool hasRole = sysAdminService.CheckUserRole(username, role);
            ViewBag.HasRole = hasRole;
            ViewBag.Username = username;
            ViewBag.Role = role;

            return View();
        }
    }
}