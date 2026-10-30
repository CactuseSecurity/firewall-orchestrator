using FWO.Config.Api.Data;
using FWO.Data;
using System.Reflection;

namespace FWO.Test
{
    /// <summary>
    /// Builds the API result of the config upsert for simulated API connections.
    /// </summary>
    internal static class ConfigUpsertTestResult
    {
        /// <summary>
        /// Returns the result of a successful upsert, which reports every passed config item as written.
        /// </summary>
        /// <typeparam name="QueryResponseType">Response type requested by the caller.</typeparam>
        /// <param name="variables">Variables passed to the upsert, holding the config items.</param>
        public static QueryResponseType ForAllItems<QueryResponseType>(object? variables)
        {
            PropertyInfo? configItemsProperty = variables?.GetType().GetProperty("config_items");
            int itemCount = configItemsProperty?.GetValue(variables) is IEnumerable<ConfigItem> configItems ? configItems.Count() : 0;
            return (QueryResponseType)(object)new ReturnId { AffectedRows = itemCount };
        }
    }
}
