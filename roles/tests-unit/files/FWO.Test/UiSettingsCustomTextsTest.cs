using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Config.File;
using FWO.Data;
using FWO.Test.Mocks;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;
using System.Linq;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    internal class UiSettingsCustomTextsTest
    {
        private static readonly Language[] kUiLanguages = new Language[2]
        {
            new() { Name = "English", CultureInfo = "en-US" },
            new() { Name = "German", CultureInfo = "de-DE" }
        };

        private static readonly FieldInfo SelectedLanguageField = typeof(SettingsCustomTexts).GetField("selectedLanguage", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(typeof(SettingsCustomTexts).FullName, "selectedLanguage");
        private static readonly FieldInfo DictsLoadedField = typeof(SettingsCustomTexts).GetField("dictsLoaded", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(typeof(SettingsCustomTexts).FullName, "dictsLoaded");
        private static readonly FieldInfo ActDictField = typeof(SettingsCustomTexts).GetField("actDict", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(typeof(SettingsCustomTexts).FullName, "actDict");
        private static readonly FieldInfo ActCustomDictField = typeof(SettingsCustomTexts).GetField("actCustomDict", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(typeof(SettingsCustomTexts).FullName, "actCustomDict");
        private static readonly FieldInfo ResultsField = typeof(SettingsCustomTexts).GetField("results", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(typeof(SettingsCustomTexts).FullName, "results");
        private static readonly FieldInfo SearchStringField = typeof(SettingsCustomTexts).GetField("searchString", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(typeof(SettingsCustomTexts).FullName, "searchString");
        private static readonly FieldInfo IgnoreHelpTextsField = typeof(SettingsCustomTexts).GetField("ignoreHelpTexts", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(typeof(SettingsCustomTexts).FullName, "ignoreHelpTexts");
        private static readonly FieldInfo CaseSensitiveField = typeof(SettingsCustomTexts).GetField("caseSensitive", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(typeof(SettingsCustomTexts).FullName, "caseSensitive");

        [SetUp]
        public void SetUp()
        {
            SimulatedUserConfig.DummyTranslate["customize_texts"] = "Customize texts";
            SimulatedUserConfig.DummyTranslate["U5321"] = "Customize the UI texts";
            SimulatedUserConfig.DummyTranslate["language"] = "Language";
            SimulatedUserConfig.DummyTranslate["select"] = "Select";
            SimulatedUserConfig.DummyTranslate["search"] = "Search";
            SimulatedUserConfig.DummyTranslate["case_sensitive"] = "Case sensitive";
            SimulatedUserConfig.DummyTranslate["ignore_helptexts"] = "Ignore help texts";
            SimulatedUserConfig.DummyTranslate["key"] = "Key";
            SimulatedUserConfig.DummyTranslate["text"] = "Text";
            SimulatedUserConfig.DummyTranslate["custom_text"] = "Custom text";
            SimulatedUserConfig.DummyTranslate["delete"] = "Delete";
            SimulatedUserConfig.DummyTranslate["save"] = "Save";
            SimulatedUserConfig.DummyTranslate["change_default"] = "Change default";
            SimulatedUserConfig.DummyTranslate["U5301"] = "Custom texts saved";
            SimulatedUserConfig.DummyTranslate["H5702"] = "Select language";
            SimulatedUserConfig.DummyTranslate["H5703"] = "Search texts";
            SimulatedUserConfig.DummyTranslate["H5704"] = "Results";
        }

        [Test]
        public void OnInitialized_UsesFirstLanguage()
        {
            SettingsCustomTexts component = CreateComponent(CreateApiConnection(), out _, out _);

            InvokePrivateVoid(component, "OnInitialized");

            Assert.That(GetMember<Language>(component, "selectedLanguage").Name, Is.EqualTo("English"));
        }

        [Test]
        public async Task LoadDicts_LoadsTextsAndCustomTexts()
        {
            RecordingCustomTextsApiConnection apiConnection = CreateApiConnection();
            SettingsCustomTexts component = CreateComponent(apiConnection, out GlobalConfig globalConfig, out UserConfig userConfig);
            SetMember(component, "selectedLanguage", globalConfig.UiLanguages[1]);

            await InvokePrivateTask(component, "LoadDicts", globalConfig.UiLanguages[1]);

            Assert.Multiple(() =>
            {
                Assert.That(DictsLoadedField.GetValue(component), Is.EqualTo(true));
                Assert.That(GetMember<Dictionary<string, string>>(component, "actDict"), Has.Count.EqualTo(3));
                Assert.That(GetMember<Dictionary<string, string>>(component, "actCustomDict"), Has.Count.EqualTo(2));
                Assert.That(apiConnection.Queries.Count(query => query == ConfigQueries.getTextsPerLanguage), Is.EqualTo(1));
                Assert.That(apiConnection.Queries.Count(query => query == ConfigQueries.getCustomTextsPerLanguage), Is.EqualTo(2));
            });
        }

        [Test]
        public void Search_FiltersHelpTextsAndMatchesCustomTexts()
        {
            SettingsCustomTexts component = CreateComponent(CreateApiConnection(), out _, out _);
            SetMember(component, "actDict", new Dictionary<string, string>
            {
                { "A1000", "Alpha one" },
                { "H2000", "Hidden help" },
                { "B1000", "Beta value" }
            });
            SetMember(component, "actCustomDict", new Dictionary<string, string>
            {
                { "B1000", "Beta custom" },
                { "C1000", "Alpha custom" }
            });
            SetMember(component, "searchString", "Alpha");
            SetMember(component, "ignoreHelpTexts", true);
            SetMember(component, "caseSensitive", false);

            InvokePrivateVoid(component, "Search");

            List<SettingsCustomTexts.TextEntry> results = GetMember<List<SettingsCustomTexts.TextEntry>>(component, "results");
            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component, "displayResults"), Is.True);
                Assert.That(results, Has.Count.EqualTo(2));
                Assert.That(results.Any(entry => entry.Key == "A1000"), Is.True);
                Assert.That(results.Any(entry => entry.Key == "C1000"), Is.True);
                Assert.That(results.Any(entry => entry.Key == "H2000"), Is.False);
            });
        }

        [Test]
        public void Search_IsCaseSensitiveWhenRequested()
        {
            SettingsCustomTexts component = CreateComponent(CreateApiConnection(), out _, out _);
            SetMember(component, "actDict", new Dictionary<string, string>
            {
                { "A1000", "Alpha one" }
            });
            SetMember(component, "actCustomDict", new Dictionary<string, string>());
            SetMember(component, "searchString", "alpha");
            SetMember(component, "ignoreHelpTexts", false);
            SetMember(component, "caseSensitive", true);

            InvokePrivateVoid(component, "Search");

            Assert.That(GetMember<List<SettingsCustomTexts.TextEntry>>(component, "results"), Is.Empty);
        }

        [Test]
        public void Search_DoesNotDuplicateMatchingKeysFromCustomDict()
        {
            SettingsCustomTexts component = CreateComponent(CreateApiConnection(), out _, out _);
            SetMember(component, "actDict", new Dictionary<string, string>
            {
                { "B1000", "Beta value" }
            });
            SetMember(component, "actCustomDict", new Dictionary<string, string>
            {
                { "B1000", "Beta custom" }
            });
            SetMember(component, "searchString", "Beta");
            SetMember(component, "ignoreHelpTexts", false);
            SetMember(component, "caseSensitive", false);

            InvokePrivateVoid(component, "Search");

            List<SettingsCustomTexts.TextEntry> results = GetMember<List<SettingsCustomTexts.TextEntry>>(component, "results");
            Assert.Multiple(() =>
            {
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0].Key, Is.EqualTo("B1000"));
                Assert.That(results[0].CustomText, Is.EqualTo("Beta custom"));
            });
        }

        [Test]
        public void Search_IncludesHelpTextsWhenNotIgnored()
        {
            SettingsCustomTexts component = CreateComponent(CreateApiConnection(), out _, out _);
            SetMember(component, "actDict", new Dictionary<string, string>
            {
                { "H2000", "Hidden help" }
            });
            SetMember(component, "actCustomDict", new Dictionary<string, string>());
            SetMember(component, "searchString", "Hidden");
            SetMember(component, "ignoreHelpTexts", false);
            SetMember(component, "caseSensitive", false);

            InvokePrivateVoid(component, "Search");

            List<SettingsCustomTexts.TextEntry> results = GetMember<List<SettingsCustomTexts.TextEntry>>(component, "results");
            Assert.Multiple(() =>
            {
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0].Key, Is.EqualTo("H2000"));
            });
        }

        [Test]
        public async Task Save_DeletesAndUpsertsCustomTexts()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = new();
            RecordingCustomTextsApiConnection apiConnection = CreateApiConnection();
            SettingsCustomTexts component = CreateComponent(apiConnection, out GlobalConfig globalConfig, out UserConfig userConfig, messages);
            SetMember(component, "selectedLanguage", globalConfig.UiLanguages[0]);
            SetMember(component, "results", new List<SettingsCustomTexts.TextEntry>
            {
                new() { Key = "A1000", CustomText = "", Delete = true },
                new() { Key = "B1000", CustomText = "Beta custom updated", Delete = false },
                new() { Key = "C1000", CustomText = "", Delete = false }
            });

            int initialQueryCount = apiConnection.Queries.Count;
            await InvokePrivateTask(component, "Save");

            List<string> queries = apiConnection.Queries.Skip(initialQueryCount).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Change default"));
                Assert.That(messages[0].Message, Is.EqualTo("Custom texts saved"));
                Assert.That(queries.Count(query => query == ConfigQueries.deleteCustomText), Is.EqualTo(1));
                Assert.That(queries.Count(query => query == ConfigQueries.upsertCustomText), Is.EqualTo(1));
                Assert.That(queries.Count(query => query == ConfigQueries.getCustomTextsPerLanguage), Is.EqualTo(1));
                Assert.That(queries.Count(query => query == ConfigQueries.getTextsPerLanguage), Is.EqualTo(1));
                Assert.That(GetMember<List<SettingsCustomTexts.TextEntry>>(component, "results")[0].Delete, Is.False);
            });
        }

        [Test]
        public async Task Save_ShowsErrorWhenUpsertFails()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = new();
            RecordingCustomTextsApiConnection apiConnection = CreateApiConnection();
            apiConnection.ThrowOnUpsert = true;
            SettingsCustomTexts component = CreateComponent(apiConnection, out GlobalConfig globalConfig, out _, messages);
            SetMember(component, "selectedLanguage", globalConfig.UiLanguages[0]);
            SetMember(component, "results", new List<SettingsCustomTexts.TextEntry>
            {
                new() { Key = "B1000", CustomText = "Beta custom updated", Delete = false }
            });

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Save"));
                Assert.That(messages[0].IsError, Is.True);
            });
        }

        [Test]
        public async Task LoadDicts_ShowsNoDataWhenTextLoadFails()
        {
            RecordingCustomTextsApiConnection apiConnection = CreateApiConnection();
            apiConnection.ThrowOnTextsQuery = true;
            SettingsCustomTexts component = CreateComponent(apiConnection, out GlobalConfig globalConfig, out _);
            SetMember(component, "selectedLanguage", globalConfig.UiLanguages[0]);

            await InvokePrivateTask(component, "LoadDicts", globalConfig.UiLanguages[0]);

            Assert.Multiple(() =>
            {
                Assert.That(DictsLoadedField.GetValue(component), Is.EqualTo(false));
                Assert.That(GetMember<Dictionary<string, string>>(component, "actDict"), Is.Empty);
                Assert.That(apiConnection.Queries.Count(query => query == ConfigQueries.getTextsPerLanguage), Is.EqualTo(1));
            });
        }

        private static SettingsCustomTexts CreateComponent(RecordingCustomTextsApiConnection apiConnection, out GlobalConfig globalConfig, out UserConfig userConfig, List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null)
        {
            globalConfig = new SimulatedGlobalConfig();
            globalConfig.UiLanguages = kUiLanguages;
            userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection, "English");
            SettingsCustomTexts component = new();
            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
            {
                messages?.Add((exception, title, message, isError));
            }));
            return component;
        }

        private static RecordingCustomTextsApiConnection CreateApiConnection()
        {
            RecordingCustomTextsApiConnection apiConnection = new();
            apiConnection.TextsByLanguage["English"] = new List<UiText>
            {
                new() { Id = "A1000", Txt = "Alpha one", Language = "English" },
                new() { Id = "B1000", Txt = "Beta value", Language = "English" },
                new() { Id = "H2000", Txt = "Hidden help", Language = "English" }
            };
            apiConnection.TextsByLanguage["German"] = new List<UiText>
            {
                new() { Id = "A1000", Txt = "Alpha eins", Language = "German" },
                new() { Id = "B1000", Txt = "Beta wert", Language = "German" },
                new() { Id = "H2000", Txt = "Versteckte Hilfe", Language = "German" }
            };
            apiConnection.CustomTextsByLanguage["English"] = new Dictionary<string, string>
            {
                { "B1000", "Beta custom" },
                { "C1000", "Alpha custom" }
            };
            apiConnection.CustomTextsByLanguage["German"] = new Dictionary<string, string>
            {
                { "A1000", "Alpha angepasst" },
                { "C1000", "Alpha benutzerdefiniert" }
            };
            return apiConnection;
        }

        private static void SetMember<T>(object instance, string memberName, T value)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(instance, value);
                return;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            throw new MissingMemberException(type.FullName, memberName);
        }

        private static T GetMember<T>(object instance, string memberName)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return (T)property.GetValue(instance)!;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return (T)field.GetValue(instance)!;
            }

            throw new MissingMemberException(type.FullName, memberName);
        }

        private static async Task InvokePrivateTask(object instance, string methodName, params object?[] args)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            Task task = (Task)(method.Invoke(instance, args) ?? throw new InvalidOperationException($"{methodName} returned null task."));
            await task;
        }

        private static void InvokePrivateVoid(object instance, string methodName, params object?[] args)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            method.Invoke(instance, args);
        }

        private sealed class RecordingCustomTextsApiConnection : SimulatedApiConnection
        {
            public List<string> Queries { get; } = new();
            public Dictionary<string, List<UiText>> TextsByLanguage { get; } = new();
            public Dictionary<string, Dictionary<string, string>> CustomTextsByLanguage { get; } = new();
            public bool ThrowOnTextsQuery { get; set; }
            public bool ThrowOnUpsert { get; set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);

                if (query == ConfigQueries.getConfigItemsByUser && typeof(QueryResponseType) == typeof(ConfigItem[]))
                {
                    return Task.FromResult((QueryResponseType)(object)Array.Empty<ConfigItem>());
                }

                if (query == ConfigQueries.getTextsPerLanguage && typeof(QueryResponseType) == typeof(List<UiText>))
                {
                    if (ThrowOnTextsQuery)
                    {
                        throw new InvalidOperationException("text load failed");
                    }
                    string language = variables?.GetType().GetProperty("language")?.GetValue(variables)?.ToString() ?? "";
                    return Task.FromResult((QueryResponseType)(object)(TextsByLanguage.TryGetValue(language, out List<UiText>? texts) ? texts : new List<UiText>()));
                }

                if (query == ConfigQueries.getCustomTextsPerLanguage && typeof(QueryResponseType) == typeof(List<UiText>))
                {
                    string language = variables?.GetType().GetProperty("language")?.GetValue(variables)?.ToString() ?? "";
                    if (!CustomTextsByLanguage.TryGetValue(language, out Dictionary<string, string>? customTexts))
                    {
                        return Task.FromResult((QueryResponseType)(object)new List<UiText>());
                    }

                    List<UiText> result = customTexts.Select(entry => new UiText { Id = entry.Key, Txt = entry.Value, Language = language }).ToList();
                    return Task.FromResult((QueryResponseType)(object)result);
                }

                if (query == ConfigQueries.deleteCustomText && typeof(QueryResponseType) == typeof(object))
                {
                    string language = variables?.GetType().GetProperty("lang")?.GetValue(variables)?.ToString() ?? "";
                    string id = variables?.GetType().GetProperty("id")?.GetValue(variables)?.ToString() ?? "";
                    if (CustomTextsByLanguage.TryGetValue(language, out Dictionary<string, string>? customTexts))
                    {
                        customTexts.Remove(id);
                    }
                    return Task.FromResult((QueryResponseType)(object)new object());
                }

                if (query == ConfigQueries.upsertCustomText && typeof(QueryResponseType) == typeof(object))
                {
                    if (ThrowOnUpsert)
                    {
                        throw new InvalidOperationException("upsert failed");
                    }
                    string language = variables?.GetType().GetProperty("lang")?.GetValue(variables)?.ToString() ?? "";
                    string id = variables?.GetType().GetProperty("id")?.GetValue(variables)?.ToString() ?? "";
                    string text = variables?.GetType().GetProperty("text")?.GetValue(variables)?.ToString() ?? "";
                    if (!CustomTextsByLanguage.TryGetValue(language, out Dictionary<string, string>? customTexts))
                    {
                        customTexts = new Dictionary<string, string>();
                        CustomTextsByLanguage[language] = customTexts;
                    }
                    customTexts[id] = text;
                    return Task.FromResult((QueryResponseType)(object)new object());
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }
    }
}
