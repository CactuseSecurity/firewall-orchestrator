using System;
using System.IO;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class DataImportBaseTest
    {
        [Test]
        public void RunImportScriptPassesArguments()
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Ignore("Script execution test requires a Unix-like environment.");
            }

            string tempDir = Path.Combine(Path.GetTempPath(), $"fwo-import-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string outputFile = Path.Combine(tempDir, "args.txt");
                string scriptPath = Path.Combine(tempDir, "print_args.py");
                string scriptContent = "#!/usr/bin/env python3\n"
                    + "import sys\n"
                    + $"with open(r\"{outputFile}\", \"w\", encoding=\"utf-8\") as file:\n"
                    + "    file.write(\" \".join(sys.argv[1:]))\n";

                File.WriteAllText(scriptPath, scriptContent);
#pragma warning disable CA1416 // Validate platform compatibility
                File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#pragma warning restore CA1416 // Validate platform compatibility

                ApiConnection apiConnection = new SimulatedApiConnection();
                GlobalConfig globalConfig = new SimulatedGlobalConfig();
                TestDataImportBase importer = new(apiConnection, globalConfig);

                bool executed = importer.ExecuteScript(scriptPath, "-f foldername");

                Assert.That(executed, Is.True);
                Assert.That(File.ReadAllText(outputFile), Is.EqualTo("-f foldername"));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private sealed class TestDataImportBase : DataImportBase
        {
            public TestDataImportBase(ApiConnection apiConnection, GlobalConfig globalConfig)
                : base(apiConnection, globalConfig)
            {
            }

            public bool ExecuteScript(string scriptPath, string? args)
            {
                return RunImportScript(scriptPath, args);
            }
        }
    }
}
