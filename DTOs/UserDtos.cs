namespace LiquorPOS.Api.DTOs;

/* ============================================================
   USER LIST RESPONSE
============================================================ */

public class UserListResponse
{
    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public int RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public int BusinessId { get; set; }

    public int? BranchId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}


/* ============================================================
   UPDATE USER REQUEST
============================================================ */

public class UpdateUserRequest
{
    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public int RoleId { get; set; }

    public int? BranchId { get; set; }

    public bool IsActive { get; set; }
}


/* ============================================================
   CHANGE PASSWORD REQUEST
============================================================ */

public class ChangeUserPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}