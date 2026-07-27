using FWO.Basics;
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
            try
            {
                string modeToStore = string.IsNullOrWhiteSpace(executionMode) ? GlobalConst.kUserRolesSelection : executionMode;

                await sessionStorage.SetAsync(ExecutionModeKey, modeToStore)
                    .WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception)
            {
                Log.WriteDebug("Execution Mode", "SessionStorage is currently unavailable.");
            }
        }

        public async Task ClearExecutionMode()
        {
            try
            {
                await sessionStorage.DeleteAsync(ExecutionModeKey)
                    .WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception)
            {
                Log.WriteDebug("Execution Mode", $"SessionStorage is currently unavailable.");
            }
        }
    }
}
