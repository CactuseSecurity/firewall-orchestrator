using FWO.Data.Workflow;
using System.Text.Json;

namespace FWO.Data.Modelling
{
    /// <summary>
    /// The extra configurations of a modelled connection as they were requested: saved with the request task when it is
    /// created from the connection, next to its connection id, so that what is implemented later follows the request that
    /// was approved, not the connection as it is changed afterwards.
    /// </summary>
    public static class ModellingRequestExtraConfigs
    {
        /// <summary>
        /// The additional info of a request task for a modelled connection: its id and its extra configurations, those of
        /// the interface it uses included. An empty list is saved as well, so a request task without saved extra
        /// configurations can be told from one whose connection had none.
        /// </summary>
        /// <param name="connection">Modelled connection the request task is created for.</param>
        public static Dictionary<string, string> AdditionalInfo(ModellingConnection connection)
        {
            ArgumentNullException.ThrowIfNull(connection);
            return new()
            {
                { AdditionalInfoKeys.ConnId, connection.Id.ToString() },
                { AdditionalInfoKeys.ExtraConfigs, Serialize(connection.ExtraConfigs) },
                { AdditionalInfoKeys.InterfaceExtraConfigs, Serialize(connection.ExtraConfigsFromInterface) }
            };
        }

        /// <summary>
        /// The extra configurations saved with a request task under the given key; null if the request task has none
        /// saved, e.g. as it was created before they were saved with it.
        /// </summary>
        /// <param name="reqTask">Request task.</param>
        /// <param name="key"><see cref="AdditionalInfoKeys.ExtraConfigs"/> or <see cref="AdditionalInfoKeys.InterfaceExtraConfigs"/>.</param>
        /// <exception cref="JsonException">The saved value is no list of extra configurations.</exception>
        public static List<ModellingExtraConfig>? Read(WfReqTaskBase reqTask, string key)
        {
            ArgumentNullException.ThrowIfNull(reqTask);
            string value = reqTask.GetAddInfoValue(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }
            return JsonSerializer.Deserialize<List<ModellingExtraConfig>>(value) ?? throw new JsonException($"The value of '{key}' is no list of extra configurations.");
        }

        private static string Serialize(IEnumerable<ModellingExtraConfig> extraConfigs)
        {
            List<ModellingExtraConfig> requested = [.. extraConfigs.Select(config =>
                new ModellingExtraConfig { ExtraConfigType = config.ExtraConfigType, ExtraConfigText = config.ExtraConfigText })];
            return JsonSerializer.Serialize(requested);
        }
    }
}
