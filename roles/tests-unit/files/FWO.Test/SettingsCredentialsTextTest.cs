using NUnit.Framework;
using System.Text.RegularExpressions;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal partial class SettingsCredentialsTextTest
    {
        [GeneratedRegex(@"(?:GetText|PureLine)\(""(?<key>[^""]+)""\)")]
        private static partial Regex TextKeyRegex();

        [Test]
        public void SettingsCredentials_AllTextKeysAreLocalizedInGermanAndEnglish()
        {
            string page = File.ReadAllText(LocateRepositoryFile(
                "roles", "ui", "files", "FWO.UI", "Pages", "Settings", "SettingsCredentials.razor"));
            string texts = File.ReadAllText(LocateRepositoryFile(
                "roles", "database", "files", "sql", "idempotent", "fworch-texts.sql"));

            List<string> keys = [.. TextKeyRegex().Matches(page).Select(match => match.Groups["key"].Value).Distinct()];

            Assert.That(keys, Does.Contain("save_credential").And.Contain("E5131"));
            using (Assert.EnterMultipleScope())
            {
                foreach (string key in keys)
                {
                    Assert.That(texts, Does.Match($@"\('{Regex.Escape(key)}',\s*'German'"), key);
                    Assert.That(texts, Does.Match($@"\('{Regex.Escape(key)}',\s*'English'"), key);
                }
            }
        }

        private static string LocateRepositoryFile(params string[] parts)
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory != null)
            {
                FileInfo candidate = new(Path.Combine(directory.FullName, Path.Combine(parts)));
                if (candidate.Exists)
                {
                    return candidate.FullName;
                }
                directory = directory.Parent;
            }
            Assert.Ignore("The repository sources are not reachable in this environment.");
            return string.Empty;
        }
    }
}
