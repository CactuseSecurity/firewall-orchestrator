using FWO.Basics;

namespace FWO.Ai.Harness.Hosting;

/// <summary>
/// Settings of the harness server, read from the configuration section <see cref="kSectionName"/>: environment
/// variables (e.g. Harness__McpEndpoint, set by the installer in the systemd unit) and the command line. A setting that
/// is not given has the default of its property. The AI provider, the model, the API key and the agent loop are not
/// part of them: they are edited on the AI settings page of the UI and read from the API by the harness itself, with its
/// technical user (see <see cref="Settings.AiSettingsRefresher"/>).
/// </summary>
public class HarnessSettings
{
    /// <summary>Configuration section of the settings.</summary>
    public const string kSectionName = "Harness";

    /// <summary>
    /// MCP endpoint of the MCP server on the same host, see ai_mcp_native_mcp_uri in the installer inventory.
    /// </summary>
    public const string kDefaultMcpEndpoint = "http://127.0.0.1:8890/mcp";

    /// <summary>Default number of runs a user may start within any minute.</summary>
    public const int kDefaultRunsPerMinute = 10;

    /// <summary>Default number of runs a user may run at the same time.</summary>
    public const int kDefaultMaxConcurrentRunsPerUser = 3;

    /// <summary>
    /// Default maximum request body size of the AG-UI endpoint (10 MiB). The harness is stateless, so a client sends the
    /// whole conversation with every run; the compaction keeps it within the context window of the model. The default
    /// leaves room for a conversation filling the compaction threshold of a model with a large context window
    /// (e.g. 1M tokens), so that the conversation is compacted before the limit refuses it.
    /// </summary>
    public const long kDefaultMaxRequestBodyBytes = 10L * 1024 * 1024;

    /// <summary>Largest maximum request body size that may be configured (64 MiB).</summary>
    public const long kMaxRequestBodyBytesLimit = 64L * 1024 * 1024;

    /// <summary>
    /// Default technical user of the harness, holding the role <see cref="Roles.AiHarness"/>; see ai_harness_user_name
    /// in the installer inventory.
    /// </summary>
    public const string kDefaultTechnicalUser = "ai-harness";

    /// <summary>
    /// Default file holding the password of the technical user; see ai_harness_password_file in the installer inventory.
    /// </summary>
    public const string kDefaultTechnicalUserPasswordFile = GlobalConst.kFwoBaseDir + "/etc/secrets/ai_harness_pwd";

    /// <summary>Default interval in seconds after which the AI settings are read again.</summary>
    public const int kDefaultSettingsRefreshSeconds = 30;

    /// <summary>Longest interval in seconds that may be configured for reading the AI settings again (one hour).</summary>
    public const int kMaxSettingsRefreshSeconds = 3600;

    private const string kMcpEndpointPurpose = "MCP endpoint";

    /// <summary>
    /// Endpoint of the FWO MCP server (FWO.Ai.Mcp). Must use https unless it is a loopback address, as the user's
    /// JWT is sent to it.
    /// </summary>
    public Uri McpEndpoint { get; set; } = new(kDefaultMcpEndpoint);

    /// <summary>Timeout for connecting to the MCP server; the MCP client's default if not set.</summary>
    public TimeSpan? McpConnectionTimeout { get; set; }

    /// <summary>Runs (AG-UI requests) a user may start within any minute; must be positive.</summary>
    public int RunsPerMinute { get; set; } = kDefaultRunsPerMinute;

    /// <summary>Runs a user may run at the same time; must be positive.</summary>
    public int MaxConcurrentRunsPerUser { get; set; } = kDefaultMaxConcurrentRunsPerUser;

    /// <summary>
    /// Maximum request body size of the AG-UI endpoint in bytes; larger requests get 413. It bounds the conversation a
    /// client sends with each run. Must be positive and at most <see cref="kMaxRequestBodyBytesLimit"/>.
    /// </summary>
    public long MaxRequestBodyBytes { get; set; } = kDefaultMaxRequestBodyBytes;

    /// <summary>
    /// Technical user the harness logs in to the middleware server with, to read the AI settings and the credential of
    /// the API key. It must hold the role <see cref="Roles.AiHarness"/> only, which may read nothing else.
    /// </summary>
    public string TechnicalUser { get; set; } = kDefaultTechnicalUser;

    /// <summary>File holding the password of the <see cref="TechnicalUser"/>; read at every login, never logged.</summary>
    public string TechnicalUserPasswordFile { get; set; } = kDefaultTechnicalUserPasswordFile;

    /// <summary>
    /// Interval in seconds after which the AI settings are read again, i.e. how long a change on the AI settings page
    /// takes to apply at most. Must be positive and at most <see cref="kMaxSettingsRefreshSeconds"/>.
    /// </summary>
    public int SettingsRefreshSeconds { get; set; } = kDefaultSettingsRefreshSeconds;

    /// <summary>
    /// Reads and checks the settings of the harness server.
    /// </summary>
    /// <param name="configuration">Configuration of the harness server</param>
    /// <exception cref="ArgumentException">A setting of the server is invalid.</exception>
    public static HarnessSettings FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        HarnessSettings settings = configuration.GetSection(kSectionName).Get<HarnessSettings>() ?? new HarnessSettings();
        settings.Validate();
        return settings;
    }

    /// <summary>
    /// Checks the settings of the server.
    /// </summary>
    /// <exception cref="ArgumentException">A setting of the server is invalid.</exception>
    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(McpEndpoint);
        EndpointGuard.RequireHttpsOrLoopback(McpEndpoint, kMcpEndpointPurpose);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(RunsPerMinute);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxConcurrentRunsPerUser);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxRequestBodyBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxRequestBodyBytes, kMaxRequestBodyBytesLimit);
        if (McpConnectionTimeout is TimeSpan connectionTimeout)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(connectionTimeout, TimeSpan.Zero);
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(TechnicalUser);
        ArgumentException.ThrowIfNullOrWhiteSpace(TechnicalUserPasswordFile);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(SettingsRefreshSeconds);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(SettingsRefreshSeconds, kMaxSettingsRefreshSeconds);
    }
}
