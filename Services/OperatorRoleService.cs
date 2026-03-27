using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;

namespace Vectrik.Services;

public class OperatorRoleService : IOperatorRoleService
{
    private readonly TenantDbContext _db;

    public OperatorRoleService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<OperatorRole>> GetAllAsync(bool activeOnly = true)
    {
        var query = _db.OperatorRoles.AsQueryable();
        if (activeOnly)
            query = query.Where(r => r.IsActive);
        return await query.OrderBy(r => r.DisplayOrder).ThenBy(r => r.Name).ToListAsync();
    }

    public async Task<OperatorRole?> GetByIdAsync(int id)
    {
        return await _db.OperatorRoles
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<OperatorRole> CreateAsync(OperatorRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        if (string.IsNullOrWhiteSpace(role.Name))
            throw new ArgumentException("Role name is required.");

        if (string.IsNullOrWhiteSpace(role.Slug))
            throw new ArgumentException("Role slug is required.");

        if (await _db.OperatorRoles.AnyAsync(r => r.Slug == role.Slug))
            throw new InvalidOperationException($"A role with slug '{role.Slug}' already exists.");

        _db.OperatorRoles.Add(role);
        await _db.SaveChangesAsync();
        return role;
    }

    public async Task<OperatorRole> UpdateAsync(OperatorRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        var existing = await _db.OperatorRoles.FindAsync(role.Id)
            ?? throw new InvalidOperationException($"OperatorRole {role.Id} not found.");

        if (await _db.OperatorRoles.AnyAsync(r => r.Slug == role.Slug && r.Id != role.Id))
            throw new InvalidOperationException($"A role with slug '{role.Slug}' already exists.");

        existing.Name = role.Name;
        existing.Slug = role.Slug;
        existing.Description = role.Description;
        existing.IsActive = role.IsActive;
        existing.DisplayOrder = role.DisplayOrder;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var role = await _db.OperatorRoles.FindAsync(id)
            ?? throw new InvalidOperationException($"OperatorRole {id} not found.");

        var hasAssignments = await _db.UserOperatorRoles.AnyAsync(ur => ur.OperatorRoleId == id);
        if (hasAssignments)
            throw new InvalidOperationException("Cannot delete a role that is assigned to users. Deactivate it instead.");

        _db.OperatorRoles.Remove(role);
        await _db.SaveChangesAsync();
    }

    public async Task AssignRoleToUserAsync(int userId, int roleId, string assignedBy)
    {
        if (string.IsNullOrWhiteSpace(assignedBy))
            throw new ArgumentException("AssignedBy is required.");

        var alreadyAssigned = await _db.UserOperatorRoles
            .AnyAsync(ur => ur.UserId == userId && ur.OperatorRoleId == roleId);
        if (alreadyAssigned)
            return; // idempotent

        _db.UserOperatorRoles.Add(new UserOperatorRole
        {
            UserId = userId,
            OperatorRoleId = roleId,
            AssignedBy = assignedBy
        });
        await _db.SaveChangesAsync();
    }

    public async Task RemoveRoleFromUserAsync(int userId, int roleId)
    {
        var assignment = await _db.UserOperatorRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.OperatorRoleId == roleId);

        if (assignment is not null)
        {
            _db.UserOperatorRoles.Remove(assignment);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<List<User>> GetUsersWithRoleAsync(int roleId)
    {
        return await _db.UserOperatorRoles
            .Where(ur => ur.OperatorRoleId == roleId)
            .Select(ur => ur.User)
            .ToListAsync();
    }

    public async Task<bool> UserHasRoleAsync(int userId, int roleId)
    {
        return await _db.UserOperatorRoles
            .AnyAsync(ur => ur.UserId == userId && ur.OperatorRoleId == roleId);
    }
}
