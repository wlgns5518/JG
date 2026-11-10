using MarketingAIAgent.Domain;
using Xunit;

namespace MarketingAIAgent.Tests;
public class RulesTests
{
    public const string ValidComment = "운명원 홍보: 상대의 애매한 태도라는 고민이 눈에 들어오네요. 관계를 돌아보는 참고로 사주 상담도 차분하게 살펴보셔도 좋겠습니다.";
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abcdefghijk&feature=share")]
    [InlineData("https://youtu.be/abcdefghijk?t=10")]
    [InlineData("https://youtube.com/shorts/abcdefghijk")]
    [InlineData("abcdefghijk")]
    public void ExtractsSupportedUrls(string value) => Assert.Equal("abcdefghijk", Rules.ExtractVideoId(value));
    [Theory]
    [InlineData("https://evil.com/watch?v=abcdefghijk")]
    [InlineData("https://youtube.com.evil.com/watch?v=abcdefghijk")]
    [InlineData("file:///abcdefghijk")]
    [InlineData("https://youtube.com/watch?v=short")]
    public void RejectsUntrustedUrls(string value) => Assert.Throws<AgentException>(() => Rules.ExtractVideoId(value));
    [Fact] public void ValidatesComment() => Assert.Null(Rules.ValidateComment(ValidComment));
    [Theory][InlineData(39)][InlineData(121)] public void RejectsLength(int length) => Assert.NotNull(Rules.ValidateComment(new string('가', length)));
    [Theory][InlineData(" #추천")][InlineData(" https://test.com")][InlineData(" test.kr")]
    public void RejectsLinks(string suffix) => Assert.NotNull(Rules.ValidateComment(ValidComment + suffix));
    [Fact] public void RequiresDisclosure() => Assert.NotNull(Rules.ValidateComment(ValidComment.Replace("홍보", "안내")));
    [Fact] public void CountsGraphemes() => Assert.Equal(2, Rules.Count("가👨‍👩‍👧‍👦"));
    [Fact] public void MeasuresActualGrowth()
    {
        var now = DateTime.UtcNow;
        Assert.Equal(200, Rules.Growth(new() { Views = 1000, CapturedAt = now }, new() { Views = 1400, CapturedAt = now.AddHours(2) }));
        Assert.Null(Rules.Growth(new() { CapturedAt = now }, new() { CapturedAt = now }));
        Assert.Null(Rules.Growth(new() { Views = 100, CapturedAt = now }, new() { Views = 90, CapturedAt = now.AddHours(1) }));
    }
    [Fact] public void UsesSeoulDay() => Assert.Equal("2026-11-11", Rules.SeoulDate(new DateTime(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc)));
    [Fact] public void EnforcesTransitions()
    {
        Assert.True(Rules.CanTransition(PostStatus.Posting, PostStatus.VerificationUnknown));
        Assert.False(Rules.CanTransition(PostStatus.Posting, PostStatus.Failed));
        Assert.False(Rules.CanTransition(PostStatus.Success, PostStatus.OpeningBrowser));
        Assert.False(Rules.CanTransition(PostStatus.Typing, PostStatus.Success));
        Assert.True(Rules.CanTransition(PostStatus.OpeningBrowser, PostStatus.Failed));
    }
}
