namespace Server.Data;

public sealed class BoardCommentVoteEntity
{
    public int Id { get; set; }
    public int CommentId { get; set; }
    public required string UserId { get; set; }
    public int VoteValue { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    
    public BoardPostCommentEntity? Comment { get; set; }
}
