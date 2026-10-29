using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class RepositoryContractFileLocatorTest
{
    /// <summary>
    /// Verifies all supported layouts, their precedence, and required-file failure behavior.
    /// </summary>
    [Test]
    public void LocateFrom_UsesRepositoryThenPackagedThenLegacyLayout()
    {
        string fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "FWO.RepositoryContractFileLocatorTest",
            Guid.NewGuid().ToString("N"));
        DirectoryInfo startDirectory = Directory.CreateDirectory(
            Path.Combine(fixtureRoot, "csharp", "FWO.Test", "bin", "Debug", "net10.0"));
        string repositoryFile = Path.Combine(fixtureRoot, "roles", "component", "fixture.json");
        string packagedFile = Path.Combine(fixtureRoot, "csharp", "repository-contracts", "fixture.json");
        string legacyFile = Path.Combine(fixtureRoot, "csharp", "fixture.json");

        try
        {
            WriteFixture(repositoryFile, "repository");
            WriteFixture(packagedFile, "packaged");
            WriteFixture(legacyFile, "legacy");
            Assert.That(Locate(startDirectory), Is.EqualTo(repositoryFile));

            File.Delete(repositoryFile);
            Assert.That(Locate(startDirectory), Is.EqualTo(packagedFile));

            File.Delete(packagedFile);
            Assert.That(Locate(startDirectory), Is.EqualTo(legacyFile));

            File.Delete(legacyFile);
            Assert.That(() => Locate(startDirectory), Throws.TypeOf<FileNotFoundException>());
        }
        finally
        {
            Directory.Delete(fixtureRoot, recursive: true);
        }
    }

    /// <summary>
    /// Locates the test fixture using the production search order.
    /// </summary>
    private static string Locate(DirectoryInfo startDirectory) =>
        RepositoryContractFileLocator.LocateFrom(
            [startDirectory],
            "roles", "component", "fixture.json");

    /// <summary>
    /// Creates one fixture and any required parent directories.
    /// </summary>
    private static void WriteFixture(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
