using System.Security.Cryptography;

namespace FWO.Basics
{
    /// <summary>
    /// Defines and validates the server-side allowlist for customization import sources.
    /// </summary>
    public static class ImportPathPolicy
    {
        /// <summary>
        /// Directory below which app-data and subnet-data import sources may live.
        /// </summary>
        public const string kAllowedCustomizationRoot = "/usr/local/fworch";

        private static readonly string[] kAllowedExtensions = [".json", ".py"];

        /// <summary>
        /// Returns allowed import file stems below the default customization root.
        /// </summary>
        public static List<string> GetAllowedImportFileStems()
        {
            return GetAllowedImportFileStems(kAllowedCustomizationRoot);
        }

        /// <summary>
        /// Returns allowed import file stems below the given customization root.
        /// </summary>
        public static List<string> GetAllowedImportFileStems(string allowedRoot)
        {
            if (!Directory.Exists(allowedRoot))
            {
                return [];
            }

            return Directory.EnumerateFiles(allowedRoot, "*", SearchOption.AllDirectories)
                .Where(IsAllowedImportFileExtension)
                .Where(path => TryValidateExistingFile(path, allowedRoot, out _))
                .Select(RemoveAllowedExtension)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Removes the allowed extension from an existing selected import file.
        /// </summary>
        public static string RemoveAllowedExtension(string selectedFile)
        {
            string extension = Path.GetExtension(selectedFile);
            return kAllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
                ? selectedFile[..^extension.Length]
                : selectedFile;
        }

        /// <summary>
        /// Validates a stored extensionless import source and returns matching files.
        /// </summary>
        public static List<string> GetValidatedExistingImportFiles(string storedPath)
        {
            return GetValidatedExistingImportFiles(storedPath, kAllowedCustomizationRoot);
        }

        /// <summary>
        /// Validates a stored extensionless import source and returns matching files.
        /// </summary>
        public static List<string> GetValidatedExistingImportFiles(string storedPath, string allowedRoot)
        {
            string extension = Path.GetExtension(storedPath);
            string pathWithoutExtension = kAllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
                ? RemoveAllowedExtension(storedPath)
                : storedPath;

            List<string> existingFiles = kAllowedExtensions
                .Select(extension => pathWithoutExtension + extension)
                .Where(File.Exists)
                .ToList();

            if (existingFiles.Count == 0)
            {
                throw new FileNotFoundException($"Import source '{pathWithoutExtension}' must reference an existing .json or .py file.");
            }

            foreach (string existingFile in existingFiles)
            {
                if (!TryValidateExistingFile(existingFile, allowedRoot, out string errorMessage))
                {
                    throw new UnauthorizedAccessException(errorMessage);
                }
            }
            return existingFiles;
        }

        /// <summary>
        /// Validates the shape of a configured import source without touching the filesystem.
        /// Ensures the source resolves below the allowed customization root, uses no path
        /// traversal, and (when an extension is present) uses an allowed extension.
        /// File existence and security attributes (symlink, world-writable) are validated on the
        /// importer host at read/run time, since only that host owns the customization directory.
        /// </summary>
        public static void ValidateImportSourceShape(string importSource)
        {
            ValidateImportSourceShape(importSource, kAllowedCustomizationRoot);
        }

        /// <summary>
        /// Validates the shape of a configured import source without touching the filesystem.
        /// </summary>
        public static void ValidateImportSourceShape(string importSource, string allowedRoot)
        {
            if (string.IsNullOrWhiteSpace(importSource))
            {
                throw new ArgumentException("Import source must not be empty.");
            }

            string extension = Path.GetExtension(importSource);
            if (extension.Length > 0 && !kAllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Import source '{importSource}' must reference a .json or .py file.");
            }

            string fullRoot = Path.GetFullPath(allowedRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(importSource, fullRoot);
            if (!fullPath.StartsWith(fullRoot, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException($"Import source '{importSource}' is outside the allowed customization directory '{allowedRoot}'.");
            }
        }

        /// <summary>
        /// Validates a specific existing import file.
        /// </summary>
        public static void ValidateExistingImportFile(string filePath)
        {
            ValidateExistingImportFile(filePath, kAllowedCustomizationRoot);
        }

        /// <summary>
        /// Validates a specific existing import file.
        /// </summary>
        public static void ValidateExistingImportFile(string filePath, string allowedRoot)
        {
            if (!TryValidateExistingFile(filePath, allowedRoot, out string errorMessage))
            {
                throw new UnauthorizedAccessException(errorMessage);
            }
        }

        /// <summary>
        /// Calculates the SHA-256 hash for a file.
        /// </summary>
        public static string CalculateSha256(string filePath)
        {
            using FileStream stream = File.OpenRead(filePath);
            byte[] hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static bool TryValidateExistingFile(string filePath, string allowedRoot, out string errorMessage)
        {
            errorMessage = "";
            if (!File.Exists(filePath))
            {
                errorMessage = $"Import file '{filePath}' does not exist.";
                return false;
            }

            if (!IsAllowedImportFileExtension(filePath))
            {
                errorMessage = $"Import file '{filePath}' must end with .json or .py.";
                return false;
            }

            string fullRoot = Path.GetFullPath(allowedRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(filePath);
            if (!fullPath.StartsWith(fullRoot, StringComparison.Ordinal))
            {
                errorMessage = $"Import file '{filePath}' is outside the allowed customization directory '{allowedRoot}'.";
                return false;
            }

            if (ContainsSymlink(fullPath, fullRoot))
            {
                errorMessage = $"Import file '{filePath}' contains a symbolic link in its path.";
                return false;
            }

            string? writablePath = GetFirstWorldWritablePath(fullPath, fullRoot);
            if (writablePath != null)
            {
                errorMessage = $"Import file '{filePath}' uses world-writable path '{writablePath}'.";
                return false;
            }
            return true;
        }

        private static bool IsAllowedImportFileExtension(string filePath)
        {
            return kAllowedExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);
        }

        private static bool ContainsSymlink(string fullPath, string fullRoot)
        {
            foreach (string path in EnumeratePathComponents(fullPath, fullRoot))
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    return true;
                }
            }
            return false;
        }

        private static string? GetFirstWorldWritablePath(string fullPath, string fullRoot)
        {
            if (OperatingSystem.IsWindows())
            {
                return null;
            }

            foreach (string path in EnumeratePathComponents(fullPath, fullRoot))
            {
                if (IsWorldWritable(path))
                {
                    return path;
                }
            }
            return null;
        }

        private static bool IsWorldWritable(string path)
        {
#pragma warning disable CA1416 // Unix file modes are only read on Unix-like platforms.
            UnixFileMode mode = File.GetUnixFileMode(path);
#pragma warning restore CA1416
            return (mode & UnixFileMode.OtherWrite) == UnixFileMode.OtherWrite;
        }

        private static IEnumerable<string> EnumeratePathComponents(string fullPath, string fullRoot)
        {
            string currentPath = fullRoot.TrimEnd(Path.DirectorySeparatorChar);
            if (Directory.Exists(currentPath))
            {
                yield return currentPath;
            }

            string relativePath = Path.GetRelativePath(currentPath, fullPath);
            foreach (string component in relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                currentPath = Path.Combine(currentPath, component);
                yield return currentPath;
            }
        }
    }
}
