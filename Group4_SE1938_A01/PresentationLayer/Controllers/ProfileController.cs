using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessLayer.Interfaces;

namespace PresentationLayer.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IUserService _userService;

        public ProfileController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.IsDefaultPassword = await _userService.IsDefaultPasswordAsync(userId);
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ các trường mật khẩu.";
                return RedirectToAction("Index");
            }

            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "Mật khẩu mới và xác nhận mật khẩu không khớp.";
                return RedirectToAction("Index");
            }

            if (newPassword.Length < 6)
            {
                TempData["Error"] = "Mật khẩu mới phải dài ít nhất 6 ký tự.";
                return RedirectToAction("Index");
            }

            var userId = GetCurrentUserId();
            try
            {
                var success = await _userService.ChangePasswordAsync(userId, oldPassword, newPassword);
                if (success)
                {
                    TempData["Success"] = "Đổi mật khẩu thành công!";
                }
                else
                {
                    TempData["Error"] = "Mật khẩu cũ không chính xác.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi đổi mật khẩu: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        private int GetCurrentUserId()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0";
            int.TryParse(userIdString, out int userId);
            return userId;
        }
    }
}
