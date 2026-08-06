namespace DraftDatastore.Domain.Entities;

public sealed class LoginHistory
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public bool Succeeded { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public User User { get; set; } = null!;
}
