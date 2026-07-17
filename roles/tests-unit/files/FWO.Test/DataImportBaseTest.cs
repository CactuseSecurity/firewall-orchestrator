using System;
using System.IO;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class DataImportBaseTest
    {
        [Test]
        public void RunImportScriptReturnsFalseWhenScriptMissing()
        {
            ApiConnection apiConnection = new SimulatedApiConnection();
            GlobalConfig globalConfig = new SimulatedGlobalConfig();
            TestDataImportBase importer = new(apiConnection, globalConfig);

            string scriptPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.py");

            bool executed = importer.ExecuteScript(scriptPath, null);

            Assert.That(executed, Is.False);
        }

        [Test]
        public void RunImportScriptPassesArguments()
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Ignore("Script execution test requires a Unix-like environment.");
            }

            string tempDir = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"fwo-import-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string outputFile = Path.Combine(tempDir, "args.txt");
                string scriptPath = Path.Combine(tempDir, "print_args.py");
                string scriptContent = "#!/usr/bin/env python3\n"
                    + "import sys\n"
                    + $"with open(r\"{outputFile}\", \"w\", encoding=\"utf-8\") as file:\n"
                    + "    file.write(\"\\n\".join(sys.argv[1:]))\n";

                File.WriteAllText(scriptPath, scriptContent);
#pragma warning disable CA1416 // Validate platform compatibility
                File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#pragma warning restore CA1416 // Validate platform compatibility

                ApiConnection apiConnection = new SimulatedApiConnection();
                GlobalConfig globalConfig = new SimulatedGlobalConfig();
                TestDataImportBase importer = new(apiConnection, globalConfig);

                bool executed = importer.ExecuteScript(
                    scriptPath,
                    "--criticalityRecertPeriodMapping \"1:360\" \"2:180\" \"3:90\" --compositeIdFields \"Applikation\" \"Teilapplikation\"",
                    validateImportFile: false
                );

                Assert.That(executed, Is.True);
                Assert.That(
                    File.ReadAllLines(outputFile),
                    Is.EqualTo(
                    [
                        "--criticalityRecertPeriodMapping",
                        "1:360",
                        "2:180",
                        "3:90",
                        "--compositeIdFields",
                        "Applikation",
                        "Teilapplikation"
                    ])
                );
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public void ReadFileTrimsContent()
        {
            ApiConnection apiConnection = new SimulatedApiConnection();
            GlobalConfig globalConfig = new SimulatedGlobalConfig();
            TestDataImportBase importer = new(apiConnection, globalConfig);

            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "  sample text \n");

                importer.ReadImportFile(tempFile, validateImportFile: false);

                Assert.That(importer.ImportFile, Is.EqualTo("sample text"));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Test]
        public void ParseCommandLineArgumentsPreservesQuotedValues()
        {
            List<string> arguments = TestDataImportBase.GetArguments(
                "--filterColumn \"Aktive Firewallregel\" --includeValues \"Ja\" --compositeIdFields \"Applikation\" \"Teilapplikation\""
            );

            Assert.That(
                arguments,
                Is.EqualTo(
                [
                    "--filterColumn",
                    "Aktive Firewallregel",
                    "--includeValues",
                    "Ja",
                    "--compositeIdFields",
                    "Applikation",
                    "Teilapplikation"
                ])
            );
        }

        [Test]
        public void ImportPathPolicyAcceptsAllowedExistingFileAndRemovesExtension()
        {
            string tempRoot = CreateNonWorldWritableTempDirectory();
            try
            {
                string nestedDir = Path.Combine(tempRoot, "nested");
                Directory.CreateDirectory(nestedDir);
                string importFile = Path.Combine(nestedDir, "owners.json");
                File.WriteAllText(importFile, "{}");
                SetOwnerOnlyModes(tempRoot, nestedDir, importFile);

                List<string> validFiles = ImportPathPolicy.GetValidatedExistingImportFiles(importFile, tempRoot);
                List<string> stems = ImportPathPolicy.GetAllowedImportFileStems(tempRoot);

                Assert.That(validFiles, Is.EqualTo(new[] { importFile }));
                Assert.That(stems, Is.EqualTo(new[] { Path.Combine(nestedDir, "owners") }));
                Assert.That(ImportPathPolicy.RemoveAllowedExtension(importFile), Is.EqualTo(Path.Combine(nestedDir, "owners")));
            }
            finally
            {
                Directory.Delete(tempRoot, true);
            }
        }

        [Test]
        public void ImportPathPolicyAllowsOnlyConfiguredCustomizationRoots()
        {
            string fwoHome = CreateNonWorldWritableTempDirectory();
            try
            {
                string customizingRoot = Path.Combine(fwoHome, "scripts", "customizing");
                string etcRoot = Path.Combine(fwoHome, "etc");
                string blockedRoot = Path.Combine(fwoHome, "importer");
                Directory.CreateDirectory(customizingRoot);
                Directory.CreateDirectory(etcRoot);
                Directory.CreateDirectory(blockedRoot);

                string customizingScript = Path.Combine(customizingRoot, "owners.py");
                string etcFile = Path.Combine(etcRoot, "owners.json");
                string blockedScript = Path.Combine(blockedRoot, "owners.py");
                File.WriteAllText(customizingScript, "#!/usr/bin/env python3\n");
                File.WriteAllText(etcFile, "{}");
                File.WriteAllText(blockedScript, "#!/usr/bin/env python3\n");

                List<string> allowedRoots = [customizingRoot, etcRoot];

                Assert.DoesNotThrow(() => ImportPathPolicy.ValidateExistingImportFile(customizingScript, allowedRoots));
                Assert.DoesNotThrow(() => ImportPathPolicy.ValidateExistingImportFile(etcFile, allowedRoots));
                Assert.Throws<UnauthorizedAccessException>(() =>
                    ImportPathPolicy.ValidateExistingImportFile(blockedScript, allowedRoots));
                Assert.Throws<UnauthorizedAccessException>(() =>
                    ImportPathPolicy.ValidateImportSourceShape(blockedScript, allowedRoots));
                Assert.That(
                    ImportPathPolicy.GetAllowedImportFileStems(allowedRoots),
                    Is.EquivalentTo(new[]
                    {
                        Path.Combine(customizingRoot, "owners"),
                        Path.Combine(etcRoot, "owners")
                    })
                );
            }
            finally
            {
                Directory.Delete(fwoHome, true);
            }
        }

        [Test]
        public void ImportPathPolicyRejectsFileOutsideAllowedRoot()
        {
            string tempRoot = CreateNonWorldWritableTempDirectory();
            string outsideFile = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid():N}.json");
            try
            {
                File.WriteAllText(outsideFile, "{}");

                Assert.Throws<UnauthorizedAccessException>(() =>
                    ImportPathPolicy.GetValidatedExistingImportFiles(outsideFile, tempRoot));
            }
            finally
            {
                Directory.Delete(tempRoot, true);
                File.Delete(outsideFile);
            }
        }

        [Test]
        public void ImportPathPolicyRejectsWorldWritablePath()
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Ignore("Unix file mode test requires a Unix-like environment.");
            }

            string tempRoot = CreateNonWorldWritableTempDirectory();
            try
            {
                string importFile = Path.Combine(tempRoot, "owners.json");
                File.WriteAllText(importFile, "{}");
#pragma warning disable CA1416 // Unix file modes are only set on Unix-like platforms.
                File.SetUnixFileMode(importFile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherWrite);
#pragma warning restore CA1416

                Assert.Throws<UnauthorizedAccessException>(() =>
                    ImportPathPolicy.GetValidatedExistingImportFiles(importFile, tempRoot));
            }
            finally
            {
                Directory.Delete(tempRoot, true);
            }
        }

        [Test]
        public void ValidateImportSourceShapeAcceptsSourceUnderRootWithoutFilesystemAccess()
        {
            // Root and files do not exist on disk: shape validation must not touch the filesystem.
            string root = Path.Combine(Path.GetTempPath(), $"fwo-missing-root-{Guid.NewGuid():N}");

            Assert.DoesNotThrow(() => ImportPathPolicy.ValidateImportSourceShape(Path.Combine(root, "owners"), root));
            Assert.DoesNotThrow(() => ImportPathPolicy.ValidateImportSourceShape(Path.Combine(root, "nested", "owners.json"), root));
            Assert.DoesNotThrow(() => ImportPathPolicy.ValidateImportSourceShape("owners", root));
        }

        [Test]
        public void ValidateImportSourceShapeRejectsTraversalOutsideRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), $"fwo-missing-root-{Guid.NewGuid():N}");

            Assert.Throws<UnauthorizedAccessException>(() =>
                ImportPathPolicy.ValidateImportSourceShape(Path.Combine(root, "..", "owners"), root));
        }

        [Test]
        public void ValidateImportSourceShapeRejectsDisallowedExtension()
        {
            string root = Path.Combine(Path.GetTempPath(), $"fwo-missing-root-{Guid.NewGuid():N}");

            Assert.Throws<ArgumentException>(() =>
                ImportPathPolicy.ValidateImportSourceShape(Path.Combine(root, "owners.sh"), root));
        }

        private sealed class TestDataImportBase : DataImportBase
        {
            public TestDataImportBase(ApiConnection apiConnection, GlobalConfig globalConfig)
                : base(apiConnection, globalConfig)
            {
            }

            public bool ExecuteScript(string scriptPath, string? args, bool validateImportFile = true)
            {
                return RunImportScript(scriptPath, args, validateImportFile);
            }

            public void ReadImportFile(string filepath, bool validateImportFile = true)
            {
                ReadFile(filepath, validateImportFile);
            }

            public static List<string> GetArguments(string args)
            {
                return ParseCommandLineArguments(args);
            }

            public string ImportFile => importFile;
        }

        private static string CreateNonWorldWritableTempDirectory()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"fwo-import-policy-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);
            if (!OperatingSystem.IsWindows())
            {
#pragma warning disable CA1416 // Unix file modes are only set on Unix-like platforms.
                File.SetUnixFileMode(tempRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#pragma warning restore CA1416
            }
            return tempRoot;
        }

        private static void SetOwnerOnlyModes(string root, string nestedDir, string filePath)
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

#pragma warning disable CA1416 // Unix file modes are only set on Unix-like platforms.
            File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.SetUnixFileMode(nestedDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
#pragma warning restore CA1416
        }
    }
}
