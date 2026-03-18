namespace pckg.Data;

public sealed class BoardPostVoteEntity
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public required string UserId { get; set; }
    public int VoteValue { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    
    public BoardPostEntity? Post { get; set; }
}
