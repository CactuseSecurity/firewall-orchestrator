using System.Text.RegularExpressions;
using FWO.Basics;
using FWO.Logging;
using FWO.Config.Api.Data;
using FWO.Api.Client;
using FWO.Data;
using FWO.Api.Client.Queries;
using System.Reflection;
using System.Text.Json.Serialization;

namespace FWO.Config.Api
{
    /// <summary>
    /// Collection of all config data for the current user
    /// </summary>
    public class UserConfig : Config
    {
        public GlobalConfig? GlobalConfig => globalConfig;
        private readonly GlobalConfig? globalConfig;

        /// <summary>
        /// Indicates whether the administrator is allowed to perform a full rollback
        /// (deletion of all import data) of a management. Reads the authoritative value
        /// from the global config; defaults to false when no global config is available.
        /// </summary>
        public bool FullRollbackAllowed => globalConfig?.AllowFullRollback ?? false;

        public Dictionary<string, string> Translate { get; set; } = [];
        public Dictionary<string, string> Overwrite { get; set; } = [];

        public UiUser User { private set; get; }
        public string ExecutionMode { get; private set; } = GlobalConst.kUserRolesSelection;

        /// <summary>
        /// Creates a text-only user configuration for unauthenticated UI/bootstrap display.
        /// This does not load direct configuration properties for operational code.
        /// </summary>
        public static UserConfig ForTextOnly(GlobalConfig globalConfig, bool registerOnChangeHandler = true)
        {
            return new UserConfig(globalConfig, registerOnChangeHandler);
        }

        /// <summary>
        /// Creates an initialized configuration for middleware and scheduled jobs that need global settings.
        /// </summary>
        public static UserConfig ForGlobalSettings(GlobalConfig globalConfig, ApiConnection apiConnection, string language = GlobalConst.kEnglish, bool owningApiConnection = false)
        {
            return new UserConfig(globalConfig, apiConnection, new UiUser { DbId = 0, Language = language }, owningApiConnection);
        }

        public static async Task<UserConfig> ConstructAsync(GlobalConfig globalConfig, ApiConnection apiConnection, int userId, bool owningApiConnection = false)
        {
            UiUser[] users = await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDbId, new { userId = userId });
            UiUser? user = users.FirstOrDefault();
            if (user == null)
            {
                Log.WriteError("Load user config", $"User with id {userId} could not be found in database.");
                throw new KeyNotFoundException();
            }
            return new UserConfig(globalConfig, apiConnection, user, owningApiConnection);
        }

        public UserConfig(GlobalConfig globalConfig, ApiConnection apiConnection, UiUser user, bool owningApiConnection = false) : base(apiConnection, user.DbId, withSubscription: false, owningApiConnection)
        {
            User = user;
            Translate = GetLanguageDictionary(globalConfig.LangDict, user.Language!);
            Overwrite = Task.Run(async () => await GetCustomDict(user.Language!)).Result;
            this.globalConfig = globalConfig;
            OnGlobalConfigChange(globalConfig, globalConfig.RawConfigItems);
            globalConfig.OnChange += OnGlobalConfigChange;
        }

        // Warning: only for Texts, ConfigItems contain Default content, correct ConfigItems are only in this.globalConfig
        private UserConfig(GlobalConfig globalConfig, bool registerOnChangeHandler = true) : base()
        {
            User = new UiUser();
            Translate = GetLanguageDictionary(globalConfig.LangDict, globalConfig.DefaultLanguage);
            this.globalConfig = globalConfig;

            if (registerOnChangeHandler)
            {
                globalConfig.OnChange += OnGlobalConfigChange;
            }
        }

        private static Dictionary<string, string> GetLanguageDictionary(Dictionary<string, Dictionary<string, string>> dictionaries, string language)
        {
            if (dictionaries.TryGetValue(language, out Dictionary<string, string>? dictionary))
            {
                return dictionary;
            }

            Log.WriteWarning("Language", $"Language dictionary for '{language}' was not found.");
            return [];
        }

        public UserConfig() : base()
        {
            User = new UiUser();
        }

        private void OnGlobalConfigChange(Config config, ConfigItem[] changedItems)
        {
            if (IsDisposed) return;
            HashSet<string> userConfigKeys = GetUserConfigKeys();
            HashSet<string> personalOverrideKeys = RawConfigItems.Select(configItem => configItem.Key).ToHashSet();
            ConfigItem[] relevantChangedItems = changedItems.Where(configItem =>
                !userConfigKeys.Contains(configItem.Key) || !personalOverrideKeys.Contains(configItem.Key)).ToArray();

            Update(relevantChangedItems);
            InvokeOnChange(this, changedItems);
        }

        /// <summary>
        /// Gets all config keys that can be overridden per user.
        /// </summary>
        private HashSet<string> GetUserConfigKeys()
        {
            return GetType().GetProperties()
                .Where(prop => prop.GetCustomAttribute<UserConfigDataAttribute>() != null)
                .Select(prop => prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
                .Where(name => name != null)
                .Cast<string>()
                .ToHashSet();
        }

        public async Task SetUserInformation(string userDn, ApiConnection apiConnection)
        {
            ThrowIfDisposed();
            if (globalConfig != null)
            {
                OnGlobalConfigChange(globalConfig, globalConfig.RawConfigItems);
            }
            Log.WriteDebug("Get User Data", $"Get user data from user with DN: \"{userDn}\"");
            UiUser[]? users = await apiConnection.SendQueryAsync<UiUser[]>(AuthQueries.getUserByDn, new { dn = userDn });
            if (users.Length > 0)
            {
                User = users[0];
            }
            await InitWithUserId(apiConnection, User.DbId, true);

            if (User.Language == null)
            {
                User.Language = DefaultLanguage;
            }
            await ChangeLanguage(User.Language, apiConnection);
        }

        public async Task ChangeLanguage(string languageName, ApiConnection apiConnection)
        {
            ThrowIfDisposed();
            if (globalConfig != null)
            {
                await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.updateUserLanguage, new { id = User.DbId, language = languageName });
                Translate = GetLanguageDictionary(globalConfig.LangDict, languageName);
                Overwrite = await GetCustomDict(languageName);
                User.Language = languageName;
                InvokeOnChange(this, []);
            }
        }

        public string GetUserLanguage()
        {
            ThrowIfDisposed();
            return User.Language ?? "";
        }

        public void SetLanguage(string languageName)
        {
            ThrowIfDisposed();
            string defaultLanguage = globalConfig != null ? globalConfig.DefaultLanguage : GlobalConst.kEnglish;

            User = new UiUser()
            {
                Language = languageName != null && languageName != "" ? languageName : defaultLanguage
            };
            if (globalConfig != null && globalConfig.LangDict.TryGetValue(User.Language, out Dictionary<string, string>? langDict))
            {
                Translate = langDict;
                Overwrite = GetLanguageDictionary(globalConfig.OverDict, User.Language);
            }
        }

        public void SetExecutionMode(string executionMode)
        {
            ExecutionMode = string.IsNullOrWhiteSpace(executionMode) ? GlobalConst.kUserRolesSelection : executionMode;
            InvokeOnChange(this, []);
        }

        public bool CanUseAnyRole(params string[] targetRoles)
        {
            return CanUseAnyRole((IEnumerable<string>)targetRoles);
        }

        public bool CanUseAnyRole(IEnumerable<string> targetRoles)
        {
            return ExecutionModeHelper.HasAnyRoleInExecutionMode(User.Roles, ExecutionMode, targetRoles);
        }

        public ReportVisibility GetReportVisibility()
        {
            return new ReportVisibility(
                RuleRelated: CanUseAnyRole(ReportVisibilityRoleSets.RuleRelated),
                ModellingRelated: CanUseAnyRole(ReportVisibilityRoleSets.ModellingRelated),
                ComplianceRelated: CanUseAnyRole(ReportVisibilityRoleSets.ComplianceRelated),
                OwnerRelated: CanUseAnyRole(ReportVisibilityRoleSets.OwnerRelated),
                WorkflowRelated: CanUseAnyRole(ReportVisibilityRoleSets.WorkflowRelated));
        }

        /// <summary>
        /// Central visibility decision for report types: combines the global on/off switch
        /// (<see cref="ConfigData.AvailableReportTypes"/>) with any per-role "Visible"/"Not Visible" override
        /// configured in <see cref="ConfigData.ReportTypeVisibilityByRole"/>. An explicit per-role override
        /// always wins - it can reinstate a report type that was disabled globally, or hide one that wasn't.
        /// Only when the role's setting is "Inherited" does the global switch act as the fallback, alongside
        /// the standard role-category visibility rules.
        /// </summary>
        public bool CanUseReportType(ReportType reportType, bool modellingOwnerAllowed = true)
        {
            if (reportType == ReportType.Undefined)
            {
                return true;
            }

            List<string> applicableRoles = GetApplicableRoles();
            if (applicableRoles.Count == 0)
            {
                return false;
            }

            Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>> overrides = ParseReportTypeVisibilityByRole();
            bool globallyAvailable = ParseAvailableReportTypes().Contains(reportType);
            return applicableRoles.Any(role => IsReportTypeVisibleForRole(reportType, role, overrides, modellingOwnerAllowed, globallyAvailable));
        }

        /// <summary>
        /// Returns the subset of the user's roles for which the given report type is explicitly set
        /// to "Not Visible". Used to keep the data-access layer (role selection for report execution)
        /// aligned with the UI-facing visibility rules in <see cref="CanUseReportType"/>.
        /// </summary>
        public List<string> GetExplicitlyDeniedRoles(ReportType reportType)
        {
            if (reportType == ReportType.Undefined)
            {
                return [];
            }

            Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>> overrides = ParseReportTypeVisibilityByRole();
            return [.. User.Roles
                .Where(role => ReportTypeRoleVisibilityConfig.GetOption(overrides, role, reportType) == ReportTypeVisibilityOption.NotVisible)
                .Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        private List<string> GetApplicableRoles()
        {
            return [.. User.Roles.Where(role => ExecutionModeHelper.IsRoleAvailableInExecutionMode(User.Roles, ExecutionMode, role))];
        }

        private string? cachedReportTypeVisibilityByRoleRaw;
        private Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>> cachedReportTypeVisibilityByRole = [];

        /// <summary>
        /// Parses <see cref="ConfigData.ReportTypeVisibilityByRole"/>, memoizing the result against the
        /// raw config string so repeated calls (e.g. once per report type per render) don't each re-run
        /// JSON deserialization when the underlying config value hasn't changed.
        /// </summary>
        private Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>> ParseReportTypeVisibilityByRole()
        {
            if (cachedReportTypeVisibilityByRoleRaw != ReportTypeVisibilityByRole)
            {
                cachedReportTypeVisibilityByRoleRaw = ReportTypeVisibilityByRole;
                cachedReportTypeVisibilityByRole = ReportTypeRoleVisibilityConfig.Parse(ReportTypeVisibilityByRole);
            }
            return cachedReportTypeVisibilityByRole;
        }

        private static bool IsReportTypeVisibleForRole(ReportType reportType, string role,
            Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>> overrides, bool modellingOwnerAllowed,
            bool globallyAvailable)
        {
            ReportTypeVisibilityOption option = ReportTypeRoleVisibilityConfig.GetOption(overrides, role, reportType);
            return option switch
            {
                // An explicit "Visible" override wins over both the global switch and the coarse-grained
                // role-category visibility rules; it must not bypass the per-instance modelling-owner scoping check.
                ReportTypeVisibilityOption.Visible =>
                    modellingOwnerAllowed || !reportType.IsModellingReport() || reportType.IsOwnerReport(),
                ReportTypeVisibilityOption.NotVisible => false,
                _ => globallyAvailable && reportType.IsVisibleTemplateType(ReportVisibilityRoleSets.ForRole(role), modellingOwnerAllowed)
            };
        }

        private string? cachedAvailableReportTypesRaw;
        private HashSet<ReportType> cachedAvailableReportTypes = [];

        /// <summary>
        /// Parses <see cref="ConfigData.AvailableReportTypes"/>, memoizing the result against the raw config
        /// string the same way <see cref="ParseReportTypeVisibilityByRole"/> does. Malformed config data is
        /// treated as "nothing globally available" rather than throwing, since this now runs on every
        /// <see cref="CanUseReportType"/> call.
        /// </summary>
        private HashSet<ReportType> ParseAvailableReportTypes()
        {
            if (cachedAvailableReportTypesRaw != AvailableReportTypes)
            {
                cachedAvailableReportTypesRaw = AvailableReportTypes;
                cachedAvailableReportTypes = [];
                if (!string.IsNullOrWhiteSpace(AvailableReportTypes))
                {
                    try
                    {
                        List<ReportType>? parsed = System.Text.Json.JsonSerializer.Deserialize<List<ReportType>>(AvailableReportTypes);
                        if (parsed != null)
                        {
                            cachedAvailableReportTypes = [.. parsed];
                        }
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        // Keep the empty set computed above.
                    }
                }
            }
            return cachedAvailableReportTypes;
        }

        public override string GetText(string key)
        {
            ThrowIfDisposed();
            if (Overwrite != null && Overwrite.TryGetValue(key, out string? overwriteValue))
            {
                return Convert(overwriteValue);
            }
            if (Translate != null && Translate.TryGetValue(key, out string? translateValue))
            {
                return Convert(translateValue);
            }
            return GetFallbackText(key);
        }

        private string GetFallbackText(string key)
        {
            if (globalConfig == null)
            {
                return GlobalConst.kUndefinedText;
            }

            string defaultLanguage = string.IsNullOrEmpty(globalConfig.DefaultLanguage)
                ? GlobalConst.kEnglish
                : globalConfig.DefaultLanguage;
            if (TryGetText(globalConfig.LangDict, defaultLanguage, key, out string defaultText))
            {
                return Convert(defaultText);
            }

            return defaultLanguage != GlobalConst.kEnglish
                && TryGetText(globalConfig.LangDict, GlobalConst.kEnglish, key, out string englishText)
                ? Convert(englishText)
                : GlobalConst.kUndefinedText;
        }

        private static bool TryGetText(Dictionary<string, Dictionary<string, string>> dictionaries,
            string language, string key, out string text)
        {
            text = string.Empty;
            if (!dictionaries.TryGetValue(language, out Dictionary<string, string>? dictionary)
                || !dictionary.TryGetValue(key, out string? foundText)
                || foundText == null)
            {
                return false;
            }

            text = foundText;
            return true;
        }

        public string PureLine(string text)
        {
            ThrowIfDisposed();
            return PureLineStat(GetText(text));
        }

        public static string PureLineStat(string text)
        {
            var regex = new Regex(@"\s", RegexOptions.None, TimeSpan.FromSeconds(1));
            string output = RemoveLinks(regex.Replace(text.Trim(), " "));
            output = ReplaceListElems(output);
            bool cont = true;
            while (cont)
            {
                string outputOrig = output;
                output = Regex.Replace(outputOrig, @"  ", " ");
                if (output.Length == outputOrig.Length)
                {
                    cont = false;
                }
            }
            return output;
        }

        public string GetApiText(string key)
        {
            ThrowIfDisposed();
            string text = key;
            string pattern = @"[Aa]\d{4}";
            Match m = Regex.Match(key, pattern);
            if (m.Success)
            {
                string msg = GetText(m.Value);
                if (msg != GlobalConst.kUndefinedText)
                {
                    text = msg;
                }
            }
            return text;
        }

        public async Task<Dictionary<string, string>> GetCustomDict(string languageName)
        {
            ThrowIfDisposed();
            Dictionary<string, string> dict = [];
            if (apiConnection == null)
            {
                Log.WriteError("ApiConnection is null", "The ApiConnection is not initialized.");
                return dict;
            }
            try
            {
                List<UiText> uiTexts = await apiConnection.SendQueryAsync<List<UiText>>(ConfigQueries.getCustomTextsPerLanguage, new { language = languageName });
                if (uiTexts != null)
                {
                    foreach (UiText text in uiTexts)
                    {
                        dict.Add(text.Id, text.Txt);
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError("Read custom dictionary", $"Could not read custom dict.", exception);
            }
            return dict;
        }

        private static string RemoveLinks(string txtString)
        {
            string startLink = "<a href=\"/";
            int begin, end;
            int index = 0;
            bool cont = true;

            while (cont)
            {
                begin = txtString.IndexOf(startLink, index);
                if (begin >= 0)
                {
                    end = txtString.IndexOf('>', begin + startLink.Length);
                    if (end > 0)
                    {
                        txtString = txtString.Remove(begin, end - begin + 1);
                    }
                    else
                    {
                        cont = false;
                    }
                }
                else
                {
                    cont = false;
                }
            }
            txtString = Regex.Replace(txtString, "</a>", "");
            return txtString;
        }

        private static string ReplaceListElems(string txtString)
        {
            txtString = Regex.Replace(txtString, "<ol>", "");
            txtString = Regex.Replace(txtString, "</ol>", "");
            txtString = Regex.Replace(txtString, "<ul>", "");
            txtString = Regex.Replace(txtString, "</ul>", "");
            txtString = Regex.Replace(txtString, "<li>", "\r\n");
            txtString = Regex.Replace(txtString, "</li>", "");
            txtString = Regex.Replace(txtString, "<br>", "\r\n");
            return txtString;
        }

        private string Convert(string rawText)
        {
            string plainText = System.Web.HttpUtility.HtmlDecode(rawText);

            // Heuristic to add language parameter to internal links
            if (User != null && User.Language != null)
            {
                string startLink = "<a href=\"/";
                string insertString = $"/?lang={User.Language}";

                int begin, end;
                int index = 0;
                bool cont = true;

                while (cont)
                {
                    begin = plainText.IndexOf(startLink, index);
                    if (begin >= 0)
                    {
                        end = plainText.IndexOf('"', begin + startLink.Length);
                        if (end > 0)
                        {
                            plainText = plainText.Insert(end, insertString);
                            index = end + insertString.Length;
                        }
                        else
                        {
                            cont = false;
                        }
                    }
                    else
                    {
                        cont = false;
                    }
                }
            }
            return plainText;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && globalConfig != null)
            {
                globalConfig.OnChange -= OnGlobalConfigChange;
            }

            base.Dispose(disposing); // Call base class dispose
        }
    }
}
