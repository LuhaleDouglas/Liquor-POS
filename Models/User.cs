namespace LiquorPOS.Api.Models;

public class User
{
    public int UserId { get; set; }

    public int BusinessId { get; set; }

    public int? BranchId { get; set; }

    public int RoleId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}