using Playgo.Domain.Common;

namespace Playgo.Domain.Entities;

public enum ReviewVoteType
{
    Like = 1,
    Dislike = 2,
}

public class ReviewVote : BaseEntity
{
    public Guid ReviewId { get; set; }
    public Review Review { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public ReviewVoteType VoteType { get; set; }
}
