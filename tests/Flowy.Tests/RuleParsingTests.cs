using Flow.Launcher.Plugin.Flowy;

namespace Flowy.Tests
{
    public class RuleParsingTests
    {
        [Fact]
        public void FileTypesDisplay_SplitsOnSemicolonAndComma()
        {
            var rule = new CatalogDirectory { FileTypesDisplay = "*.exe; *.lnk, ,*.bat;" };
            Assert.Equal(new[] { "*.exe", "*.lnk", "*.bat" }, rule.FileTypes);
        }

        [Fact]
        public void FileTypesDisplay_KeepsRawTextWhileTyping()
        {
            var rule = new CatalogDirectory { FileTypesDisplay = "*.exe; " };
            Assert.Equal("*.exe; ", rule.FileTypesDisplay);
        }

        [Fact]
        public void FileTypes_DefaultIsLnk()
        {
            Assert.Equal(new[] { "*.lnk" }, new CatalogDirectory().FileTypes);
        }

        [Theory]
        [InlineData("3", 3)]
        [InlineData("abc", 0)]
        [InlineData("-1", 0)]
        public void DepthDisplay_AcceptsOnlyNonNegativeNumbers(string input, int expected)
        {
            var rule = new CatalogDirectory { DepthDisplay = input };
            Assert.Equal(expected, rule.Depth);
        }

        [Fact]
        public void SuffixesDisplay_SplitsOnSemicolonAndComma()
        {
            var rule = new ExclusionRule { SuffixesDisplay = "*.tmp;*.log , " };
            Assert.Equal(new[] { "*.tmp", "*.log" }, rule.Suffixes);
        }

        [Fact]
        public void ExclusionRule_DefaultsToRecursiveWithoutSuffixes()
        {
            var rule = new ExclusionRule();
            Assert.True(rule.Recursive);
            Assert.Empty(rule.Suffixes);
        }
    }
}
