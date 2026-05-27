using FWO.Data;

namespace FWO.Services
{
    /// <summary>
    /// Resolves workflow recipient DNs through the authoritative identity backend.
    /// </summary>
    public interface IWorkflowRecipientResolver
    {
        /// <summary>
        /// Resolves mixed user or group DNs to user DNs.
        /// </summary>
        /// <param name="dns">User or group distinguished names.</param>
        /// <returns>Resolved user distinguished names.</returns>
        Task<List<string>> ResolveUserDns(IEnumerable<string> dns);

        /// <summary>
        /// Resolves mixed user or group DNs to UI users with email addresses when available.
        /// </summary>
        /// <param name="dns">User or group distinguished names.</param>
        /// <returns>Resolved UI users.</returns>
        Task<List<UiUser>> ResolveUsers(IEnumerable<string> dns);
    }
}
