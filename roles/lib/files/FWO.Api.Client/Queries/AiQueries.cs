using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    /// <summary>
    /// Queries of the AI harness (FWO.Ai.Harness), sent with the JWT of its technical user (role ai-harness).
    /// </summary>
    public class AiQueries : Queries
    {
        /// <summary>Reads the global config items with the given keys, see FWO.Data.Ai.AiSettings.kKeys.</summary>
        public static readonly string getAiSettings;

        /// <summary>Reads the credential the AI settings reference (view v_ai_provider_credential), including its secret.</summary>
        public static readonly string getAiProviderCredential;

        static AiQueries()
        {
            try
            {
                getAiSettings = GetQueryText("ai/getAiSettings.graphql");
                getAiProviderCredential = GetQueryText("ai/getAiProviderCredential.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize AiQueries", "Api AiQueries could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
