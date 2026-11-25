namespace FWO.Data.Ai
{
    /// <summary>
    /// AI providers the AI harness (FWO.Ai.Harness) can talk to. Stored by name in the global config
    /// (<see cref="AiSettings.kProviderKey"/>).
    /// </summary>
    public enum AiProvider
    {
        /// <summary>No AI provider is configured; the AI functions are not available.</summary>
        None,

        /// <summary>Ollama, usually a local model server; no API key.</summary>
        Ollama,

        /// <summary>OpenAI or an OpenAI compatible endpoint.</summary>
        OpenAi,

        /// <summary>Anthropic (Claude).</summary>
        Anthropic,

        /// <summary>Google Gemini (Gemini Developer API).</summary>
        Google
    }
}
