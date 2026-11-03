using System.Globalization;

namespace FWO.Test.AppServerIpChange
{
    /// <summary>
    /// Names and paths of the test data, set by roles/tests-integration/tasks/test-app-server-ip-change.yml.
    /// </summary>
    internal sealed class AppServerIpChangeSettings
    {
        /// <summary>Import file (without extension) delivering the app server with the old ip.</summary>
        public string FirstImportSource { get; private init; } = "";

        /// <summary>Import file (without extension) delivering the app server with the new ip.</summary>
        public string SecondImportSource { get; private init; } = "";

        /// <summary>Invalid import file (without extension) imported after each import file, see AppServerIpChangeTest.ImportAppData.</summary>
        public string GuardImportSource { get; private init; } = "";

        /// <summary>External app id of the test app.</summary>
        public string ExtAppId { get; private init; } = "";

        /// <summary>Import source of the test app and its app servers.</summary>
        public string AppImportSource { get; private init; } = "";

        /// <summary>Id of the test firewall management.</summary>
        public int ManagementId { get; private init; }

        /// <summary>Name of the app role, which is also the name of its group on the test firewall.</summary>
        public string AppRoleName { get; private init; } = "";

        /// <summary>Name the test reverse lookup returns for both ips.</summary>
        public string ServerName { get; private init; } = "";

        /// <summary>Ip of the app server in the first import.</summary>
        public string OldIp { get; private init; } = "";

        /// <summary>Ip of the app server in the second import.</summary>
        public string NewIp { get; private init; } = "";

        /// <summary>
        /// Reads the settings from the environment of the test run.
        /// </summary>
        /// <returns>The settings.</returns>
        /// <exception cref="InvalidOperationException">If a setting is missing.</exception>
        public static AppServerIpChangeSettings FromEnvironment()
        {
            return new AppServerIpChangeSettings
            {
                FirstImportSource = Read("FWO_TEST_FIRST_IMPORT_SOURCE"),
                SecondImportSource = Read("FWO_TEST_SECOND_IMPORT_SOURCE"),
                GuardImportSource = Read("FWO_TEST_GUARD_IMPORT_SOURCE"),
                ExtAppId = Read("FWO_TEST_EXT_APP_ID"),
                AppImportSource = Read("FWO_TEST_APP_IMPORT_SOURCE"),
                ManagementId = int.Parse(Read("FWO_TEST_MGM_ID"), CultureInfo.InvariantCulture),
                AppRoleName = Read("FWO_TEST_APP_ROLE"),
                ServerName = Read("FWO_TEST_SERVER_NAME"),
                OldIp = Read("FWO_TEST_OLD_IP"),
                NewIp = Read("FWO_TEST_NEW_IP")
            };
        }

        private static string Read(string variableName)
        {
            string? value = Environment.GetEnvironmentVariable(variableName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Environment variable {variableName} is not set.");
            }
            return value;
        }
    }
}
