using RecordingApp.Infrastructure.Irr;
using Xunit;

namespace RecordingApp.Tests;

public class TextSimilarityTests
{
    [Fact]
    public void WordLevelSimilarity_IdenticalText_ReturnsOne()
    {
        var score = TextSimilarity.WordLevelSimilarity("A dog running in a field", "A dog running in a field");
        Assert.Equal(1.0, score);
    }

    [Fact]
    public void WordLevelSimilarity_IgnoresCaseAndExtraWhitespace()
    {
        var score = TextSimilarity.WordLevelSimilarity("A DOG running", "a dog   running");
        Assert.Equal(1.0, score);
    }

    [Fact]
    public void WordLevelSimilarity_CompletelyDifferentText_ReturnsZero()
    {
        var score = TextSimilarity.WordLevelSimilarity("cat", "dog");
        Assert.Equal(0.0, score);
    }

    [Fact]
    public void WordLevelSimilarity_PartialOverlap_IsBetweenZeroAndOne()
    {
        var score = TextSimilarity.WordLevelSimilarity("A dog running in a field", "A dog running outside");
        Assert.InRange(score, 0.01, 0.99);
    }

    [Fact]
    public void WordLevelSimilarity_BothEmpty_ReturnsOne()
    {
        Assert.Equal(1.0, TextSimilarity.WordLevelSimilarity("", "   "));
    }
}
