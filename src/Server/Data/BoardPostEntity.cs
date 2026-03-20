namespace Server.Data;

public sealed class BoardPostEntity
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public required string AuthorUserId { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public BoardPostType PostType { get; set; } = BoardPostType.Issue;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? EditedAtUtc { get; set; }
    public int UpvoteCount { get; set; }
    public int DownvoteCount { get; set; }
    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    public bool IsDeleted { get; set; }

    public BoardEntity? Board { get; set; }
}
