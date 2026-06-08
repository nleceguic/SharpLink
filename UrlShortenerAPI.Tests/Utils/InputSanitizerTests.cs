using FluentAssertions;
using UrlShortenerAPI.Helpers;

namespace UrlShortenerAPI.Tests.Utils
{
    public class InputSanitizerTests
    {
        // -----------------------------
        // SanitizeAlias
        // -----------------------------

        [Fact]
        public void SanitizeAlias_ShouldReturnEmpty_WhenInputIsNull()
        {
            var result = InputSanitizer.SanitizeAlias(null!);
            result.Should().BeEmpty();
        }

        [Fact]
        public void SanitizeAlias_ShouldReturnEmpty_WhenInputIsEmpty()
        {
            var result = InputSanitizer.SanitizeAlias("");
            result.Should().BeEmpty();
        }

        [Fact]
        public void SanitizeAlias_ShouldReturnUnchanged_WhenAllCharsAreValid()
        {
            var result = InputSanitizer.SanitizeAlias("abc-123_XYZ");
            result.Should().Be("abc-123_XYZ");
        }

        [Fact]
        public void SanitizeAlias_ShouldRemoveSpaces()
        {
            var result = InputSanitizer.SanitizeAlias("my alias");
            result.Should().Be("myalias");
        }

        [Fact]
        public void SanitizeAlias_ShouldRemoveSpecialChars()
        {
            var result = InputSanitizer.SanitizeAlias("hello@world!");
            result.Should().Be("helloworld");
        }

        [Fact]
        public void SanitizeAlias_ShouldKeepDashAndUnderscore()
        {
            var result = InputSanitizer.SanitizeAlias("my-alias_test");
            result.Should().Be("my-alias_test");
        }

        [Fact]
        public void SanitizeAlias_ShouldRemoveNonAsciiChars()
        {
            var result = InputSanitizer.SanitizeAlias("café");
            result.Should().Be("caf");
        }

        [Fact]
        public void SanitizeAlias_ShouldReturnEmpty_WhenAllCharsAreInvalid()
        {
            var result = InputSanitizer.SanitizeAlias("!@#$%^&*()");
            result.Should().BeEmpty();
        }

        // -----------------------------
        // SanitizeUrl
        // -----------------------------

        [Fact]
        public void SanitizeUrl_ShouldReturnEmpty_WhenInputIsNull()
        {
            var result = InputSanitizer.SanitizeUrl(null!);
            result.Should().BeEmpty();
        }

        [Fact]
        public void SanitizeUrl_ShouldReturnEmpty_WhenInputIsEmpty()
        {
            var result = InputSanitizer.SanitizeUrl("");
            result.Should().BeEmpty();
        }

        [Fact]
        public void SanitizeUrl_ShouldReturnUnchanged_WhenUrlIsClean()
        {
            var result = InputSanitizer.SanitizeUrl("https://example.com/path?q=1");
            result.Should().Be("https://example.com/path?q=1");
        }

        [Fact]
        public void SanitizeUrl_ShouldTrimLeadingAndTrailingWhitespace()
        {
            var result = InputSanitizer.SanitizeUrl("  https://example.com  ");
            result.Should().Be("https://example.com");
        }

        [Fact]
        public void SanitizeUrl_ShouldRemoveNonAsciiChars()
        {
            var result = InputSanitizer.SanitizeUrl("https://example.com/éà");
            result.Should().Be("https://example.com/");
        }

        [Fact]
        public void SanitizeUrl_ShouldRemoveEmojiChars()
        {
            var result = InputSanitizer.SanitizeUrl("https://example.com/\U0001F600");
            result.Should().Be("https://example.com/");
        }

        [Fact]
        public void SanitizeUrl_ShouldKeepPrintableAsciiChars()
        {
            var input = "https://example.com/path?key=value&other=123#fragment";
            var result = InputSanitizer.SanitizeUrl(input);
            result.Should().Be(input);
        }
    }
}
