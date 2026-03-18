namespace pckg.Data;

public sealed class UserEmailEntity
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public required string Email { get; set; }
    public bool IsVerified { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime AddedAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
}
