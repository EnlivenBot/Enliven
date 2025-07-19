using Common;
using FluentAssertions;
using Xunit;

namespace Enliven.Tests;

public class SafeSubstringTests {
    [Fact]
    public void ShouldReturnSameStringWhenLengthIsLessThanInputLength() {
        var input = new string('a', 10);
        var result = input.SafeSubstring(100);
        result.Should().Be(input);
    }

    [Fact]
    public void ShouldReturnSubstringWhenLengthIsGreaterThanInputLength() {
        var input = new string('a', 10);
        var result = input.SafeSubstring(5);
        result.Should().Be("aaaaa");
    }

    [Fact]
    public void ShouldReturnSubstringWithPostContentWhenLengthIsGreaterThanInputLength() {
        var input = new string('a', 10);
        var result = input.SafeSubstring(5, "...");
        result.Should().Be("aa...");
    }

    [Fact]
    public void ShouldReturnSubstringWithPostContentWhenLengthIsGreaterThanInputLengthAndPostContentIsLonger() {
        var input = new string('a', 6);
        var result = input.SafeSubstring(5, "...");
        result.Should().Be("aa...");
    }
}