using Vectrik.Models;

namespace Vectrik.Services;

/// <summary>
/// CRUD and role-assignment operations for operator roles.
/// </summary>
public interface IOperatorRoleService
{
    Task<List<OperatorRole>> GetAllAsync(bool activeOnly = true);
    Task<OperatorRole?> GetByIdAsync(int id);
    Task<OperatorRole> CreateAsync(OperatorRole role);
    Task<OperatorRole> UpdateAsync(OperatorRole role);
    Task DeleteAsync(int id);
    Task AssignRoleToUserAsync(int userId, int roleId, string assignedBy);
    Task RemoveRoleFromUserAsync(int userId, int roleId);
    Task<List<User>> GetUsersWithRoleAsync(int roleId);
    Task<bool> UserHasRoleAsync(int userId, int roleId);
}
