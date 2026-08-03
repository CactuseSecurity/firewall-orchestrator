using FWO.Basics;
using FWO.Logging;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Security.Cryptography;

namespace FWO.Ui.Services
{
    public class ExecutionModeStorage(ISessionStorage sessionStorage)
    {
        private const string ExecutionModeKey = "execution_mode";

        public async Task<string?> GetExecutionMode()
        {
            try
            {
                ProtectedBrowserStorageResult<string> result = await sessionStorage.GetAsync<string>(ExecutionModeKey)
                    .WaitAsync(TimeSpan.FromSeconds(5));

                return result.Success && !string.IsNullOrWhiteSpace(result.Value) ? result.Value : null;
            }
            catch (CryptographicException ex)
            {
                Log.WriteWarning("Execution Mode", $"Unreadable protected session execution mode detected, trying to clear stored data: {ex.Message}");

                await ClearExecutionMode();

                return null;
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Execution Mode", $"Failed to read execution mode from session storage: {ex.Message}. {GlobalConst.BrowserResourceSaving}.");

                return null;
            }
        }

        public async Task SetExecutionMode(string executionMode)
        {
            try
            {
                string modeToStore = string.IsNullOrWhiteSpace(executionMode) ? GlobalConst.kUserRolesSelection : executionMode;

                await sessionStorage.SetAsync(ExecutionModeKey, modeToStore)
                    .WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Execution Mode", $"Failed to write execution mode to session storage: {ex.Message}. {GlobalConst.BrowserResourceSaving}.");
            }
        }

        public async Task ClearExecutionMode()
        {
            try
            {
                await sessionStorage.DeleteAsync(ExecutionModeKey)
                    .WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Execution Mode", $"Failed to clear stored execution mode: {ex.Message}. {GlobalConst.BrowserResourceSaving}.");
            }
        }
    }
}
