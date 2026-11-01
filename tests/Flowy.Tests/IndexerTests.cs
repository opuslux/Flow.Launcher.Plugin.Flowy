using System.Collections.Generic;
using System.IO;
using System.Linq;
using Flow.Launcher.Plugin.Flowy;

namespace Flowy.Tests
{
    // Jeder Test bekommt einen eigenen temporären Ordnerbaum:
    //
    // root\
    //   a.lnk, b.exe, c.txt
    //   sub1\
    //     d.lnk
    //     node_modules\e.lnk
    //     Logs\f.lnk, g.log
    //     sub2\h.lnk
    public class IndexerTests : IDisposable
    {
        private readonly string _root;

        public IndexerTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "FlowyTests_" + Guid.NewGuid().ToString("N"));
            foreach (var file in new[] { "a.lnk", "b.exe", "c.txt", @"sub1\d.lnk", @"sub1\node_modules\e.lnk", @"sub1\Logs\f.lnk", @"sub1\Logs\g.log", @"sub1\sub2\h.lnk" })
            {
                string full = Path.Combine(_root, file);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, "");
            }
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch { }
        }

        private CatalogDirectory Rule(int depth, string fileTypes = "*.lnk", bool folders = false, string path = null)
        {
            return new CatalogDirectory { Path = path ?? _root, Depth = depth, FileTypesDisplay = fileTypes, IncludeDirectories = folders };
        }

        private static ExclusionRule Exclude(string path, string suffixes = "", bool recursive = true)
        {
            return new ExclusionRule { Path = path, SuffixesDisplay = suffixes, Recursive = recursive };
        }

        private HashSet<string> Scan(IEnumerable<CatalogDirectory> rules, IEnumerable<ExclusionRule> exclusions = null)
        {
            var indexer = new Indexer();
            indexer.BuildIndex(rules.ToList(), exclusions?.ToList() ?? new List<ExclusionRule>());
            return indexer.Catalog.Select(i => Path.GetRelativePath(_root, i.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private HashSet<string> Scan(CatalogDirectory rule, params ExclusionRule[] exclusions) => Scan(new[] { rule }, exclusions);

        // --- Tiefe und Dateitypen ---

        [Fact]
        public void Depth0_ScansOnlyRootFolder()
        {
            Assert.Equal(new[] { "a.lnk" }, Scan(Rule(depth: 0)));
        }

        [Fact]
        public void Depth1_IncludesOneSubLevel()
        {
            Assert.Equal(new[] { "a.lnk", @"sub1\d.lnk" }, Scan(Rule(depth: 1)).OrderBy(p => p));
        }

        [Fact]
        public void DeepScan_FindsAllMatchingFiles()
        {
            var result = Scan(Rule(depth: 10));
            Assert.Equal(new[] { "a.lnk", @"sub1\d.lnk", @"sub1\Logs\f.lnk", @"sub1\node_modules\e.lnk", @"sub1\sub2\h.lnk" }, result.OrderBy(p => p));
        }

        [Fact]
        public void MultipleFileTypes_AreCombined()
        {
            Assert.Equal(new[] { "a.lnk", "b.exe" }, Scan(Rule(depth: 0, fileTypes: "*.lnk; *.exe")).OrderBy(p => p));
        }

        [Fact]
        public void StarDotStar_IncludesAllFiles()
        {
            Assert.Equal(new[] { "a.lnk", "b.exe", "c.txt" }, Scan(Rule(depth: 0, fileTypes: "*.*")).OrderBy(p => p));
        }

        [Fact]
        public void EmptyFileTypes_WithFolders_IndexesOnlyFolders()
        {
            var result = Scan(Rule(depth: 1, fileTypes: "", folders: true));
            Assert.Equal(new[] { "sub1", @"sub1\Logs", @"sub1\node_modules", @"sub1\sub2" }, result.OrderBy(p => p));
        }

        [Fact]
        public void Folders_AreListedUpToDepthPlusOne()
        {
            var result = Scan(Rule(depth: 0, fileTypes: "", folders: true));
            Assert.Equal(new[] { "sub1" }, result);
        }

        [Fact]
        public void Counts_AreWrittenToRule()
        {
            var rule = Rule(depth: 1, folders: true);
            Scan(rule);
            Assert.Equal(2, rule.FileCount);
            Assert.Equal(4, rule.FolderCount);
        }

        [Fact]
        public void RuleIndex_IsOneBasedRuleNumber()
        {
            var indexer = new Indexer();
            var missing = Rule(depth: 0, path: Path.Combine(_root, "missing"));
            indexer.BuildIndex(new List<CatalogDirectory> { missing, Rule(depth: 0) }, new List<ExclusionRule>());
            Assert.All(indexer.Catalog, i => Assert.Equal(2, i.RuleIndex));
            Assert.Equal(1, missing.IndexDisplay);
        }

        [Fact]
        public void MissingDirectory_IsSkippedWithZeroCounts()
        {
            var rule = Rule(depth: 5, path: Path.Combine(_root, "does-not-exist"));
            rule.FileCount = 99;
            Assert.Empty(Scan(rule));
            Assert.Equal(0, rule.FileCount);
        }

        [Fact]
        public void EnvironmentVariables_AreExpanded()
        {
            Environment.SetEnvironmentVariable("FLOWY_TEST_ROOT", _root);
            Assert.Equal(new[] { "a.lnk" }, Scan(Rule(depth: 0, path: "%FLOWY_TEST_ROOT%")));
        }

        [Fact]
        public void Rebuild_ReplacesCatalogInsteadOfMutatingIt()
        {
            var indexer = new Indexer();
            indexer.BuildIndex(new List<CatalogDirectory> { Rule(depth: 0) }, null);
            var before = indexer.Catalog;

            indexer.BuildIndex(new List<CatalogDirectory> { Rule(depth: 10) }, null);

            Assert.NotSame(before, indexer.Catalog);
            Assert.Single(before);
        }

        // --- Ausschlüsse ---

        [Fact]
        public void GlobalSuffixExclusion_AppliesEverywhere()
        {
            var result = Scan(Rule(depth: 10, fileTypes: "*.*"), Exclude("", "*.log; *.txt"));
            Assert.DoesNotContain("c.txt", result);
            Assert.DoesNotContain(@"sub1\Logs\g.log", result);
            Assert.Contains("b.exe", result);
        }

        [Fact]
        public void FolderExclusion_RemovesWholeSubtreeAndFolderResult()
        {
            var result = Scan(Rule(depth: 10, folders: true), Exclude(Path.Combine(_root, "sub1")));
            Assert.Equal(new[] { "a.lnk" }, result);
        }

        [Fact]
        public void FolderExclusion_IgnoresTrailingBackslash()
        {
            var result = Scan(Rule(depth: 10), Exclude(Path.Combine(_root, "sub1") + "\\"));
            Assert.Equal(new[] { "a.lnk" }, result);
        }

        [Fact]
        public void SuffixExclusion_NonRecursive_OnlyAffectsThatFolder()
        {
            var result = Scan(Rule(depth: 10), Exclude(Path.Combine(_root, "sub1"), "*.lnk", recursive: false));
            Assert.DoesNotContain(@"sub1\d.lnk", result);
            Assert.Contains(@"sub1\sub2\h.lnk", result);
        }

        [Fact]
        public void SuffixExclusion_Recursive_AffectsSubfolders()
        {
            var result = Scan(Rule(depth: 10), Exclude(Path.Combine(_root, "sub1"), "*.lnk", recursive: true));
            Assert.Equal(new[] { "a.lnk" }, result);
        }

        [Fact]
        public void Glob_LeadingStar_MatchesFolderAnywhere()
        {
            var result = Scan(Rule(depth: 10), Exclude(@"*\node_modules"));
            Assert.DoesNotContain(@"sub1\node_modules\e.lnk", result);
            Assert.Equal(4, result.Count);
        }

        [Fact]
        public void Glob_StarInMiddle_MatchesAcrossLevels()
        {
            var result = Scan(Rule(depth: 10), Exclude(_root + @"\*\Logs"));
            Assert.DoesNotContain(@"sub1\Logs\f.lnk", result);
            Assert.Contains(@"sub1\d.lnk", result);
        }

        [Fact]
        public void Glob_QuestionMark_MatchesSingleCharacter()
        {
            var result = Scan(Rule(depth: 10, folders: true), Exclude(_root + @"\sub?"));
            Assert.Equal(new[] { "a.lnk" }, result);
        }

        [Fact]
        public void Glob_IsCaseInsensitive()
        {
            var result = Scan(Rule(depth: 10), Exclude(@"*\NODE_MODULES"));
            Assert.DoesNotContain(@"sub1\node_modules\e.lnk", result);
        }

        [Fact]
        public void Glob_WithSuffix_OnlyExcludesThoseFiles()
        {
            var result = Scan(Rule(depth: 10, fileTypes: "*.*"), Exclude(@"*\Logs", "*.log"));
            Assert.DoesNotContain(@"sub1\Logs\g.log", result);
            Assert.Contains(@"sub1\Logs\f.lnk", result);
        }
    }
}
