using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessLayer.DTOs;
using BusinessLayer.Interfaces;
using PresentationLayer.Models;

namespace PresentationLayer.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserService _userService;

        public AccountController(IUserService userService)
        {
            _userService = userService;
        }

        // ============================================================
        // LOGIN
        // ============================================================

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectByUserRole();
            }
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userService.AuthenticateAsync(model.Email, model.Password);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác. Vui lòng thử lại.");
                return View(model);
            }

            // Create authentication claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            if (user.Role == "Student" && await _userService.IsDefaultPasswordAsync(user.UserId))
            {
                TempData["Warning"] = "Bạn đang sử dụng mật khẩu mặc định. Vui lòng đổi mật khẩu để tiếp tục.";
                return RedirectToAction("Index", "Profile");
            }

            return RedirectByUserRole();
        }

        // ============================================================
        // REGISTER
        // ============================================================

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectByUserRole();
            }
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _userService.RegisterAsync(
                model.Username,
                model.Password,
                model.FullName,
                model.Email,
                "Student"
            );

            if (result == null)
            {
                ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc Email đã tồn tại trong hệ thống.");
                return View(model);
            }

            TempData["Success"] = "Đăng ký tài khoản thành công! Vui lòng đăng nhập.";
            return RedirectToAction("Login");
        }

        // ============================================================
        // LOGOUT
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // ============================================================
        // ACCESS DENIED
        // ============================================================

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // ============================================================
        // ADMIN — User Management with Server-side Search & Sorting
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Users(string? searchTerm, string? roleFilter, string? sortBy, string? sortOrder)
        {
            var allUsers = await _userService.GetAllUsersAsync();

            // --- FILTER OUT ADMIN ACCOUNTS ---
            var nonAdminUsers = allUsers.Where(u => !string.Equals(u.Role, "Admin", StringComparison.OrdinalIgnoreCase)).ToList();

            // --- Server-side FILTERING (LINQ) ---
            IEnumerable<UserDto> filteredUsers = nonAdminUsers;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                filteredUsers = filteredUsers.Where(u =>
                    (u.FullName ?? "").ToLower().Contains(term) ||
                    (u.Email ?? "").ToLower().Contains(term) ||
                    (u.Username ?? "").ToLower().Contains(term)
                );
            }

            if (!string.IsNullOrWhiteSpace(roleFilter))
            {
                filteredUsers = filteredUsers.Where(u => string.Equals(u.Role, roleFilter, StringComparison.OrdinalIgnoreCase));
            }

            // --- Server-side SORTING (LINQ) ---
            sortBy ??= "fullName";
            sortOrder ??= "asc";

            filteredUsers = sortBy.ToLower() switch
            {
                "email"    => string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase) ? filteredUsers.OrderByDescending(u => u.Email ?? "")    : filteredUsers.OrderBy(u => u.Email ?? ""),
                "role"     => string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase) ? filteredUsers.OrderByDescending(u => u.Role ?? "")     : filteredUsers.OrderBy(u => u.Role ?? ""),
                "username" => string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase) ? filteredUsers.OrderByDescending(u => u.Username ?? "") : filteredUsers.OrderBy(u => u.Username ?? ""),
                _          => string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase) ? filteredUsers.OrderByDescending(u => u.FullName ?? "") : filteredUsers.OrderBy(u => u.FullName ?? ""),
            };

            var viewModel = new UserSearchViewModel
            {
                SearchTerm    = searchTerm,
                RoleFilter    = roleFilter,
                Users         = filteredUsers,
                TotalCount    = nonAdminUsers.Count,
                FilteredCount = filteredUsers.Count()
            };

            ViewBag.SortBy    = sortBy;
            ViewBag.SortOrder = sortOrder;

            return View(viewModel);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (model.Role != "Student" && string.IsNullOrWhiteSpace(model.Password))
            {
                TempData["Error"] = "Mật khẩu là bắt buộc đối với Giáo viên hoặc Admin.";
                return RedirectToAction("Users");
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" | ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                return RedirectToAction("Users");
            }

            string password = model.Password ?? string.Empty;
            if (model.Role == "Student" && string.IsNullOrWhiteSpace(password))
            {
                password = "FptStudent@123";
            }

            var user = new UserDto
            {
                Username     = model.Username.Trim(),
                PasswordHash = password,
                FullName     = model.FullName.Trim(),
                Email        = model.Email.Trim().ToLower(),
                Role         = model.Role
            };

            var result = await _userService.CreateUserAsync(user);
            if (result == null)
            {
                TempData["Error"] = "Tên đăng nhập hoặc Email đã được sử dụng trong hệ thống.";
            }
            else
            {
                TempData["Success"] = $"Tạo tài khoản '{model.FullName}' thành công!";
            }

            return RedirectToAction("Users");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" | ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                return RedirectToAction("Users");
            }

            var user = new UserDto
            {
                UserId       = model.UserId,
                Username     = model.Username.Trim(),
                PasswordHash = model.Password ?? string.Empty,
                FullName     = model.FullName.Trim(),
                Email        = model.Email.Trim().ToLower(),
                Role         = model.Role
            };

            await _userService.UpdateUserAsync(user);
            TempData["Success"] = $"Cập nhật tài khoản '{model.FullName}' thành công!";
            return RedirectToAction("Users");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            if (userId <= 0)
            {
                TempData["Error"] = "ID tài khoản không hợp lệ.";
                return RedirectToAction("Users");
            }

            await _userService.DeleteUserAsync(userId);
            TempData["Success"] = "Xóa tài khoản thành công!";
            return RedirectToAction("Users");
        }

        // ============================================================
        // HELPER
        // ============================================================

        private IActionResult RedirectByUserRole()
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Users", "Account");
            }
            else if (User.IsInRole("Teacher"))
            {
                return RedirectToAction("Index", "Document");
            }
            else // Student
            {
                return RedirectToAction("Index", "Chat");
            }
        }
    }
}
