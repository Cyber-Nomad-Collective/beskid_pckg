namespace pckg.Data;

public sealed class BoardPostCommentEntity
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public int? ParentCommentId { get; set; }
    public required string AuthorUserId { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? EditedAtUtc { get; set; }
    public int UpvoteCount { get; set; }
    public int DownvoteCount { get; set; }
    public bool IsDeleted { get; set; }
    
    public BoardPostEntity? Post { get; set; }
    public BoardPostCommentEntity? ParentComment { get; set; }
}
