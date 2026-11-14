namespace ServiceDesk.Api.Domain;

public class User
{
    public int Id { get; set; }
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public Role Role { get; set; } = Role.Customer;
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Agents and admins can be assigned to and work on tickets.</summary>
    public bool CanWorkTickets => Role is Role.Agent or Role.Admin;
}
