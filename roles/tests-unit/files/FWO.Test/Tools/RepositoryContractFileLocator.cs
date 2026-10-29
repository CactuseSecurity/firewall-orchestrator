using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Locates repository-contract fixtures in source and installed-test layouts.
/// </summary>
internal static class RepositoryContractFileLocator
{
    /// <summary>
    /// Finds a required fixture by repository path, packaged contract name, or legacy flat name.
    /// </summary>
    internal static string Locate(params string[] repositoryRelativePath) =>
        LocateFrom(GetSearchDirectories(), repositoryRelativePath);

    /// <summary>
    /// Finds a required fixture from explicit start directories for deterministic locator tests.
    /// </summary>
    internal static string LocateFrom(
        IEnumerable<DirectoryInfo> directories,
        params string[] repositoryRelativePath)
    {
        ArgumentNullException.ThrowIfNull(directories);
        ArgumentNullException.ThrowIfNull(repositoryRelativePath);
        if (repositoryRelativePath.Length == 0)
        {
            throw new ArgumentException("A repository-relative fixture path is required.",
                nameof(repositoryRelativePath));
        }

        List<DirectoryInfo> startDirectories = directories.ToList();
        string? repositoryFile = FindWhileWalkingParents(startDirectories, repositoryRelativePath);
        if (repositoryFile != null)
        {
            return repositoryFile;
        }

        string fileName = repositoryRelativePath[^1];
        string? packagedFile = FindWhileWalkingParents(startDirectories, ["repository-contracts", fileName]);
        if (packagedFile != null)
        {
            return packagedFile;
        }

        string? legacyFile = FindWhileWalkingParents(startDirectories, [fileName]);
        return legacyFile ?? throw new FileNotFoundException(
            $"Required repository contract not found: {Path.Combine(repositoryRelativePath)}");
    }

    /// <summary>
    /// Returns distinct absolute directories used by local and installed test execution.
    /// </summary>
    private static List<DirectoryInfo> GetSearchDirectories()
    {
        return new List<string>
        {
            TestContext.CurrentContext.TestDirectory,
            Environment.CurrentDirectory
        }
        .Select(Path.GetFullPath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(path => new DirectoryInfo(path))
        .ToList();
    }

    /// <summary>
    /// Checks one relative path beneath each directory and all of its parents.
    /// </summary>
    private static string? FindWhileWalkingParents(
        IEnumerable<DirectoryInfo> startDirectories,
        IReadOnlyCollection<string> relativePath)
    {
        foreach (DirectoryInfo startDirectory in startDirectories)
        {
            for (DirectoryInfo? directory = startDirectory; directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine([directory.FullName, .. relativePath]);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        return null;
    }
}
