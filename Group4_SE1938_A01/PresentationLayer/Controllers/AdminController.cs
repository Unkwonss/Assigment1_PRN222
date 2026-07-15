using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BusinessLayer.Interfaces;
using BusinessLayer.DTOs;
using PresentationLayer.Models;
using Microsoft.AspNetCore.SignalR;
using PresentationLayer.Hubs;

namespace PresentationLayer.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IUserService _userService;
        private readonly IHubContext<NewsHub> _hubContext;

        public AdminController(IUserService userService, IHubContext<NewsHub> hubContext)
        {
            _userService = userService;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> ManageStudents(string? searchTerm)
        {
            var allUsers = await _userService.GetAllUsersAsync();
            var students = allUsers.Where(u => u.Role == "Student");

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                students = students.Where(u =>
                    (u.FullName != null && u.FullName.ToLower().Contains(term)) ||
                    (u.Email != null && u.Email.ToLower().Contains(term)) ||
                    (u.Username != null && u.Username.ToLower().Contains(term))
                );
            }

            ViewBag.SearchTerm = searchTerm;
            return View(students);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStudent(string fullName, string email)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Vui lòng điền đầy đủ họ tên và email.";
                return RedirectToAction("ManageStudents");
            }

            if (!email.Contains("@"))
            {
                TempData["Error"] = "Email không hợp lệ.";
                return RedirectToAction("ManageStudents");
            }

            try
            {
                // Check email exists in all users
                var existing = await _userService.GetUserByEmailAsync(email);
                if (existing != null)
                {
                    TempData["Error"] = $"Email '{email}' đã tồn tại trong hệ thống với vai trò '{existing.Role}'.";
                    return RedirectToAction("ManageStudents");
                }

                var student = await _userService.CreateStudentAccountAsync(fullName, email);
                if (student != null)
                {
                    TempData["Success"] = $"Đã tạo tài khoản cho sinh viên '{fullName}'. Username: '{student.Username}'. Thông tin tài khoản đã được gửi qua email.";
                }
                else
                {
                    TempData["Error"] = "Tạo tài khoản thất bại.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi: {ex.Message}";
            }

            return RedirectToAction("ManageStudents");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportCsv(IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn một file CSV hợp lệ.";
                return RedirectToAction("ManageStudents");
            }

            string ext = Path.GetExtension(csvFile.FileName).ToLower();
            if (ext != ".csv" && ext != ".txt")
            {
                TempData["Error"] = "Chỉ chấp nhận file định dạng .csv hoặc .txt chứa dữ liệu CSV.";
                return RedirectToAction("ManageStudents");
            }

            try
            {
                using (var stream = csvFile.OpenReadStream())
                {
                    int successCount = await _userService.ImportStudentsFromCsvAsync(stream);
                    if (successCount > 0)
                    {
                        TempData["Success"] = $"Import thành công {successCount} sinh viên và gửi email tài khoản mặc định!";
                    }
                    else
                    {
                        TempData["Error"] = "Không import được sinh viên nào. Vui lòng kiểm tra lại cấu trúc file CSV (Dòng đầu là header, các dòng sau dạng: Họ tên,Email).";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi khi import file CSV: {ex.Message}";
            }

            return RedirectToAction("ManageStudents");
        }

        [HttpGet]
        public async Task<IActionResult> ManageTokens(string? searchTerm, string? roleFilter)
        {
            var allUsers = await _userService.GetAllUsersAsync();
            var targetUsers = allUsers.Where(u => u.Role == "Student" || u.Role == "Teacher");

            if (!string.IsNullOrWhiteSpace(roleFilter))
            {
                targetUsers = targetUsers.Where(u => u.Role.Equals(roleFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                targetUsers = targetUsers.Where(u =>
                    (u.FullName != null && u.FullName.ToLower().Contains(term)) ||
                    (u.Email != null && u.Email.ToLower().Contains(term)) ||
                    (u.Username != null && u.Username.ToLower().Contains(term))
                );
            }

            // Calculate weekly token usage for each active student (starting from Monday 00:00:00 UTC) via UserService
            var userIds = targetUsers.Select(u => u.UserId).ToList();
            var weeklyUsage = await _userService.GetWeeklyTokenUsageMapAsync(userIds);

            var studentTokenList = targetUsers.Select(s => new UserTokenUsageViewModel
            {
                UserId = s.UserId,
                FullName = s.FullName,
                Email = s.Email,
                Username = s.Username,
                Role = s.Role,
                WeeklyTokenLimit = s.WeeklyTokenLimit,
                WeeklyTokenUsed = weeklyUsage.ContainsKey(s.UserId) ? weeklyUsage[s.UserId] : 0
            }).ToList();

            ViewBag.SearchTerm = searchTerm;
            ViewBag.RoleFilter = roleFilter;
            return View(studentTokenList);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTokenLimit(int userId, int newLimit)
        {
            if (newLimit < 0)
            {
                TempData["Error"] = "Hạn mức token không thể nhỏ hơn 0.";
                return RedirectToAction("ManageTokens");
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user != null)
            {
                user.WeeklyTokenLimit = newLimit;
                await _userService.UpdateUserAsync(user);
                TempData["Success"] = $"Đã cập nhật hạn mức token của người dùng '{user.FullName}' thành {newLimit:N0} tokens.";
                await _hubContext.Clients.All.SendAsync("ReceiveSystemNotification", "Cập nhật hạn mức", $"Hạn mức token của '{user.FullName}' đã được cập nhật thành {newLimit:N0} tokens.", "info");
            }
            else
            {
                TempData["Error"] = "Không tìm thấy người dùng.";
            }

            return RedirectToAction("ManageTokens");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAllTokenLimits(int newLimit)
        {
            if (newLimit < 0)
            {
                TempData["Error"] = "Hạn mức token không thể nhỏ hơn 0.";
                return RedirectToAction("ManageTokens");
            }

            try
            {
                var allUsers = await _userService.GetAllUsersAsync();
                var targets = allUsers.Where(u => u.Role == "Student" || u.Role == "Teacher");

                foreach (var user in targets)
                {
                    user.WeeklyTokenLimit = newLimit;
                    await _userService.UpdateUserAsync(user);
                }

                TempData["Success"] = $"Đã cập nhật hạn mức token mặc định của tất cả sinh viên & giáo viên thành {newLimit:N0} tokens.";
                await _hubContext.Clients.All.SendAsync("ReceiveSystemNotification", "Hạn mức chung", $"Hạn mức mặc định của tất cả sinh viên & giáo viên đã được thay đổi thành {newLimit:N0} tokens.", "warning");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi khi cập nhật hàng loạt: {ex.Message}";
            }

            return RedirectToAction("ManageTokens");
        }
    }
}
