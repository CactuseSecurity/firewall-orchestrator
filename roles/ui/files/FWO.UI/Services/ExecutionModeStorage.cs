using FWO.Api.Client;
using FWO.Logging;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace FWO.Ui.Services
{
    public class ExecutionModeStorage(ISessionStorage sessionStorage)
    {
        private const string ExecutionModeKey = "execution_mode";

        public async Task<string?> GetExecutionMode()
        {
            try
            {
                ProtectedBrowserStorageResult<string> result = await sessionStorage.GetAsync<string>(ExecutionModeKey);
                return result.Success && !string.IsNullOrWhiteSpace(result.Value) ? result.Value : null;
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Execution Mode", $"Failed to restore execution mode from session storage: {ex.Message}");
                await ClearExecutionMode();
                return null;
            }
        }

        public async Task SetExecutionMode(string executionMode)
        {
            string modeToStore = string.IsNullOrWhiteSpace(executionMode) ? ExecutionModeHelper.UserRolesSelection : executionMode;
            await sessionStorage.SetAsync(ExecutionModeKey, modeToStore);
        }

        public async Task ClearExecutionMode()
        {
            try
            {
                await sessionStorage.DeleteAsync(ExecutionModeKey);
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Execution Mode", $"Failed to clear execution mode from session storage: {ex.Message}");
            }
        }
    }
}
