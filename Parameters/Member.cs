namespace ParallelPixivUtil2.Parameters;
public record Member(long MemberId, string MemberToken, string MemberName, ICollection<MemberPage> Pages);
