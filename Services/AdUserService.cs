
namespace IfsahApp.Services;

// DTO returned by the "AD" service
public record AdUserDto(string AdUserName, string FullName, string Email, string Department);

public interface IAdUserService
{
    Task<AdUserDto?> GetUserByUserNameAsync(string userName, CancellationToken ct = default);
    Task<IReadOnlyList<AdUserDto>> GetUsersByDepartmentAsync(string department, CancellationToken ct = default);
}

/// <summary>
/// A simple in-memory "fake AD" service you can replace later with real AD/LDAP.
/// </summary>

public sealed class AdUserService : IAdUserService
{
    // Seed some sample users; includes the one you asked for (jdoe - IT).
    private static readonly List<AdUserDto> _users = new()
        {
            new("jdoe",     "John Doe",        "jdoe@example.com",     "IT"),
            new("asmith",   "Alice Smith",     "asmith@example.com",   "HR"),
            new("bking",    "Bob King",        "bking@example.com",    "Finance"),
            new("mali",     "Maha Al-Illy",    "mali@example.com",     "IT"),
            new("tnguyen",  "Thanh Nguyen",    "tnguyen@example.com",  "IT"),
            new("rjones",   "Rania Jones",     "rjones@example.com",   "HR")
        };

    public Task<AdUserDto?> GetUserByUserNameAsync(string userName, CancellationToken ct = default)
    {
        var user = _users.FirstOrDefault(u =>
            string.Equals(u.AdUserName, userName, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(user);
    }

    public Task<IReadOnlyList<AdUserDto>> GetUsersByDepartmentAsync(string department, CancellationToken ct = default)
    {
        var list = _users
            .Where(u => string.Equals(u.Department, department, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();

        return Task.FromResult((IReadOnlyList<AdUserDto>)list);
    }
}