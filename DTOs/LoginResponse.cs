namespace LiquorPOS.Api.DTOs;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;

    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public int RoleId { get; set; }

    public int BusinessId { get; set; }

    public int? BranchId { get; set; }
}