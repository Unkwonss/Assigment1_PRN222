using System.Collections.Generic;
using System.Threading.Tasks;
using BusinessLayer.DTOs;

namespace BusinessLayer.Interfaces
{
    public interface IUserService
    {
        Task<UserDto?> AuthenticateAsync(string email, string password);
        Task<UserDto?> RegisterAsync(string username, string password, string fullName, string email, string role = "Student");
        Task<UserDto?> GetUserByIdAsync(int userId);
        Task<UserDto?> GetUserByEmailAsync(string email);
        Task<IEnumerable<UserDto>> GetAllUsersAsync();
        Task<UserDto> CreateUserAsync(UserDto userDto);
        Task UpdateUserAsync(UserDto userDto);
        Task DeleteUserAsync(int userId);
        Task<UserDto?> CreateStudentAccountAsync(string fullName, string email);
        Task<int> ImportStudentsFromCsvAsync(System.IO.Stream fileStream);
        Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword);
        Task<bool> IsDefaultPasswordAsync(int userId);
    }
}
