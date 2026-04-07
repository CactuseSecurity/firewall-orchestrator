using System.IO;
using System.Threading.Tasks;
using FWO.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    public class ChangeLogHelperTest
    {
        [Test]
        public async Task ManualManagementEventWritesAuditLogWithReadableData()
        {
            string output = await CaptureConsoleAsync(async () =>
            {
                await ChangeLogHelper.LogManagerChange(
                    action: "manual_management_update",
                    managementId: 42,
                    managementName: "Mgmt-A",
                    userId: 7,
                    origin: "ui_settings");
            });

            Assert.That(output, Does.Contain("Audit - Management Updated Manually"));
            Assert.That(output, Does.Contain("Management ID: 42"));
            Assert.That(output, Does.Contain("Management Name: Mgmt-A"));
            Assert.That(output, Does.Contain("User ID: 7"));
            Assert.That(output, Does.Contain("Origin: ui_settings"));
        }

        [Test]
        public async Task AutodiscoveryPromptEventWritesInfoLog()
        {
            string output = await CaptureConsoleAsync(async () =>
            {
                await ChangeLogHelper.LogGatewayChange(
                    action: "autodiscovery_prompt_gateway_delete",
                    deviceId: 123,
                    managementId: 9,
                    origin: "autodiscovery");
            });

            Assert.That(output, Does.Contain("Info - Autodiscovery Prompt Created For Gateway Deletion"));
            Assert.That(output, Does.Contain("Gateway ID: 123"));
            Assert.That(output, Does.Contain("Management ID: 9"));
            Assert.That(output, Does.Contain("Origin: autodiscovery"));
        }

        [Test]
        public async Task DismissedPromptEventWritesAuditLog()
        {
            string output = await CaptureConsoleAsync(async () =>
            {
                await ChangeLogHelper.LogGatewayChange(
                    action: "prompt_dismissed_gateway_create",
                    deviceId: 8,
                    deviceName: "gw-01",
                    managementId: 3,
                    origin: "ui_autodiscovery");
            });

            Assert.That(output, Does.Contain("Audit - Gateway Creation Prompt Dismissed"));
            Assert.That(output, Does.Contain("Gateway ID: 8"));
            Assert.That(output, Does.Contain("Gateway Name: gw-01"));
            Assert.That(output, Does.Contain("Management ID: 3"));
        }

        [Test]
        public async Task MiddlewareMatrixImportWritesInfoLog()
        {
            string output = await CaptureConsoleAsync(async () =>
            {
                await ChangeLogHelper.LogMatrixChange(
                    action: "middleware_matrix_import_create",
                    matrixId: 5,
                    matrixName: "Payments",
                    origin: "import_zone_matrix_data");
            });

            Assert.That(output, Does.Contain("Info - Matrix Created During Middleware Import"));
            Assert.That(output, Does.Contain("Matrix ID: 5"));
            Assert.That(output, Does.Contain("Matrix Name: Payments"));
            Assert.That(output, Does.Contain("Origin: import_zone_matrix_data"));
        }

        [Test]
        public async Task UnmappedActionFamilyWritesWarning()
        {
            string output = await CaptureConsoleAsync(async () =>
            {
                await ChangeLogHelper.LogMatrixChange(
                    action: "unexpected_matrix_event",
                    matrixId: 1);
            });

            Assert.That(output, Does.Contain("Warning - unexpected matrix event"));
            Assert.That(output, Does.Contain("Unmapped change-log action family."));
        }

        private static async Task<string> CaptureConsoleAsync(Func<Task> action)
        {
            using StringWriter logOutput = new();
            TextWriter originalConsoleOut = Console.Out;
            Console.SetOut(logOutput);

            try
            {
                await action();
                await Console.Out.FlushAsync();
                return logOutput.ToString();
            }
            finally
            {
                Console.SetOut(originalConsoleOut);
            }
        }
    }
}
