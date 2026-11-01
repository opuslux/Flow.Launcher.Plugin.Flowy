using Flow.Launcher.Plugin.Flowy;

namespace Flowy.Tests
{
    public class PathHelperTests
    {
        [Theory]
        [InlineData("c:")]
        [InlineData(@"c:\")]
        [InlineData(@"D:\d_apps")]
        [InlineData(@"\\server\share")]
        [InlineData(@"%UserProfile%\")]
        public void LooksLikePath_RecognizesPaths(string input)
        {
            Assert.True(PathHelper.LooksLikePath(input));
        }

        [Theory]
        [InlineData("notepad")]
        [InlineData("c")]
        [InlineData("1:")]
        [InlineData(@"\single")]
        [InlineData("flowy rescan")]
        public void LooksLikePath_RejectsSearchTerms(string input)
        {
            Assert.False(PathHelper.LooksLikePath(input));
        }

        [Theory]
        [InlineData("c:", @"c:\", "")]
        [InlineData(@"c:\", @"c:\", "")]
        [InlineData(@"c:\win", @"c:\", "win")]
        [InlineData(@"c:\Windows\", @"c:\Windows\", "")]
        [InlineData(@"c:\Windows\sys", @"c:\Windows", "sys")]
        [InlineData("c:/Windows/sys", @"c:\Windows", "sys")]
        [InlineData(@"  c:\win  ", @"c:\", "win")]
        public void TryParsePathInput_SplitsDirectoryAndFilter(string input, string expectedDir, string expectedFilter)
        {
            Assert.True(PathHelper.TryParsePathInput(input, out string dir, out string filter));
            Assert.Equal(expectedDir, dir);
            Assert.Equal(expectedFilter, filter);
        }

        [Fact]
        public void TryParsePathInput_ExpandsEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable("FLOWY_TEST_DIR", @"C:\FlowyTest");
            Assert.True(PathHelper.TryParsePathInput(@"%FLOWY_TEST_DIR%\ab", out string dir, out string filter));
            Assert.Equal(@"C:\FlowyTest", dir);
            Assert.Equal("ab", filter);
        }

        [Fact]
        public void TryParsePathInput_ReturnsFalseForSearchTerms()
        {
            Assert.False(PathHelper.TryParsePathInput("notepad", out _, out _));
        }

        [Theory]
        [InlineData(@"C:\Tools", "\"C:\\Tools\"")]
        [InlineData(@"C:\", "\"C:\\\\\"")]
        [InlineData(@"C:\My Apps\", "\"C:\\My Apps\\\\\"")]
        public void QuoteArg_EscapesTrailingBackslash(string input, string expected)
        {
            Assert.Equal(expected, PathHelper.QuoteArg(input));
        }
    }
}
