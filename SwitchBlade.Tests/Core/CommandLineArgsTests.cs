using System;
using SwitchBlade.Core;
using Xunit;

namespace SwitchBlade.Tests.Core
{
    public class CommandLineArgsTests
    {
        [Fact]
        public void Quote_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => CommandLineArgs.Quote(null!));
        }

        [Fact]
        public void Quote_EmptyString_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, CommandLineArgs.Quote(""));
        }

        [Theory]
        [InlineData("/debug")]
        [InlineData("--minimized")]
        [InlineData("abc123")]
        public void Quote_PlainArgument_ReturnsUnchanged(string argument)
        {
            Assert.Equal(argument, CommandLineArgs.Quote(argument));
        }

        [Fact]
        public void Quote_ContainsSpace_WrapsInQuotes()
        {
            Assert.Equal("\"a b\"", CommandLineArgs.Quote("a b"));
        }

        [Fact]
        public void Quote_EmbeddedQuote_EscapesIt()
        {
            // a"b -> "a\"b" : the embedded quote is escaped so it parses back to a literal quote.
            Assert.Equal("\"a\\\"b\"", CommandLineArgs.Quote("a\"b"));
        }

        [Fact]
        public void Quote_BackslashBeforeEmbeddedQuote_PreservesLiteralBackslash()
        {
            // a\b"c -> "a\b\"c" : the literal backslash before b is kept, the quote is escaped.
            Assert.Equal("\"a\\b\\\"c\"", CommandLineArgs.Quote("a\\b\"c"));
        }

        [Fact]
        public void Quote_TrailingBackslashWithSpace_DoublesTrailingBackslash()
        {
            // C:\my dir\ -> "C:\my dir\\" : trailing backslashes are doubled so they don't escape the closing quote.
            Assert.Equal("\"C:\\my dir\\\\\"", CommandLineArgs.Quote("C:\\my dir\\"));
        }

        [Fact]
        public void BuildArguments_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => CommandLineArgs.BuildArguments(null!));
        }

        [Fact]
        public void BuildArguments_Empty_ReturnsEmptyString()
        {
            Assert.Equal(string.Empty, CommandLineArgs.BuildArguments([]));
        }

        [Fact]
        public void BuildArguments_MixedArgs_QuotesOnlyWhenNeeded()
        {
            var result = CommandLineArgs.BuildArguments(["/debug", "path with space"]);

            Assert.Equal("/debug \"path with space\"", result);
        }
    }
}
