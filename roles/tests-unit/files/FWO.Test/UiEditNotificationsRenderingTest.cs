using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Enums;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Shared;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Renders the notification editor and drives it through its buttons: the edit popup for the different
    /// recipient kinds, saving, deleting and resetting the last sent date, including the error paths.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class UiEditNotificationsRenderingTest
    {
        private static readonly List<int> kInitialIds = [1];
        private static readonly List<int> kIdsAfterAdd = [1, 100];
        private static readonly List<int> kIdsAfterDelete = [];

        [Test]
        public async Task EditPopup_ForRecertification_ShowsDeadlineRowsResponsibleEditorsAndPriority()
        {
            await using EditorContext context = new(NotificationClient.Recertification, RecertNotification());
            IRenderedComponent<EditNotifications> editor = context.Render();

            Click(editor, Icons.Edit);

            string markup = editor.Markup;
            Assert.Multiple(() =>
            {
                Assert.That(markup, Does.Contain(context.UserConfig.GetText("notif_before_deadline")));
                Assert.That(markup, Does.Contain(context.UserConfig.GetText("notif_after_deadline")));
                Assert.That(markup, Does.Contain(context.UserConfig.GetText("email_priority")));
                Assert.That(markup, Does.Contain(context.UserConfig.GetText(EmailPriority.High.TextKey())));
                Assert.That(markup, Does.Contain("notif_to_resp"));
                Assert.That(markup, Does.Contain("notif_bcc_resp"));
            });
        }

        [Test]
        public async Task AddPopup_ForInterfaceRequest_OffersDeadlineChoiceWithoutBeforeDeadlineRow()
        {
            await using EditorContext context = new(NotificationClient.InterfaceRequest);
            IRenderedComponent<EditNotifications> editor = context.Render();

            Click(editor, Icons.Add);

            Assert.Multiple(() =>
            {
                Assert.That(editor.Markup, Does.Contain(context.UserConfig.GetText("deadline")));
                Assert.That(editor.Markup, Does.Not.Contain(context.UserConfig.GetText("notif_before_deadline")));
                Assert.That(editor.Markup, Does.Contain("notif_to_resp"));
            });
        }

        [Test]
        public async Task EditPopup_WithDeadlineFilter_ShowsLockedDeadlineAsText()
        {
            await using EditorContext context = new(NotificationClient.RuleTimer, RuleTimerNotification());
            IRenderedComponent<EditNotifications> editor = context.Render(deadlineFilter: NotificationDeadline.RuleExpiry);

            Click(editor, Icons.Edit);

            Assert.That(editor.Markup, Does.Contain(context.UserConfig.GetText(nameof(NotificationDeadline.RuleExpiry))));
        }

        [Test]
        public async Task EditPopup_ForWorkflowAction_ShowsWorkflowRecipientEditors()
        {
            await using EditorContext context = new(NotificationClient.WfAction, WorkflowNotification());
            IRenderedComponent<EditNotifications> editor = context.Render();

            Click(editor, Icons.Edit);

            Assert.That(editor.FindComponents<WorkflowEmailRecipientEditor>(), Has.Count.EqualTo(3));
        }

        [Test]
        public async Task Save_ForNewDirectAddressNotification_AddsItAndReportsNewId()
        {
            await using EditorContext context = new(NotificationClient.Report);
            List<int>? reportedIds = null;
            IRenderedComponent<EditNotifications> editor = context.Render(kInitialIds, ids => reportedIds = ids);

            Click(editor, Icons.Add);
            await SetActNotification(editor, notification =>
            {
                notification.EmailSubject = "Weekly report";
                notification.EmailAddressTo = "to@example.test";
                notification.EmailAddressCc = "cc@example.test";
                notification.EmailPriority = EmailPriority.Low;
            });
            ClickSave(editor);

            Assert.Multiple(() =>
            {
                Assert.That(context.ApiConnection.AddCount, Is.EqualTo(1));
                Assert.That(context.ApiConnection.LastEmailPriority, Is.EqualTo(nameof(EmailPriority.Low)));
                Assert.That(reportedIds, Is.EqualTo(kIdsAfterAdd));
                Assert.That(editor.FindComponents<OtherEmailAddressesEditor>(), Is.Empty);
                Assert.That(context.Messages, Is.Empty);
            });
        }

        [Test]
        public async Task Save_ForExistingNotification_UpdatesItAndClosesPopup()
        {
            await using EditorContext context = new(NotificationClient.Report, ReportNotification());
            IRenderedComponent<EditNotifications> editor = context.Render();

            Click(editor, Icons.Edit);
            ClickSave(editor);

            Assert.Multiple(() =>
            {
                Assert.That(context.ApiConnection.UpdateCount, Is.EqualTo(1));
                Assert.That(editor.Markup, Does.Not.Contain(context.UserConfig.GetText("email_subject")));
                Assert.That(context.Messages, Is.Empty);
            });
        }

        [Test]
        public async Task Save_ReportsErrorWhenMutationFails()
        {
            await using EditorContext context = new(NotificationClient.Report, ReportNotification());
            context.ApiConnection.ThrowOnMutation = true;
            IRenderedComponent<EditNotifications> editor = context.Render();

            Click(editor, Icons.Edit);
            ClickSave(editor);

            Assert.Multiple(() =>
            {
                Assert.That(context.Messages, Has.Count.EqualTo(1));
                Assert.That(context.Messages[0].Exception, Is.Not.Null);
            });
        }

        [Test]
        public async Task Save_RejectsRepeatAfterDeadlineWithoutRepetitionCount()
        {
            await using EditorContext context = new(NotificationClient.RuleTimer, RuleTimerNotification());
            IRenderedComponent<EditNotifications> editor = context.Render();

            Click(editor, Icons.Edit);
            await SetActNotification(editor, notification => notification.RepetitionsAfterDeadline = null);
            ClickSave(editor);

            Assert.Multiple(() =>
            {
                Assert.That(context.ApiConnection.UpdateCount, Is.EqualTo(0));
                Assert.That(context.Messages.Select(message => message.Message), Has.Some.EqualTo(context.UserConfig.GetText("E4013")));
            });
        }

        [Test]
        public async Task Cancel_ClosesPopupWithoutSaving()
        {
            await using EditorContext context = new(NotificationClient.Report, ReportNotification());
            IRenderedComponent<EditNotifications> editor = context.Render();

            Click(editor, Icons.Edit);
            Click(editor.FindComponents<PopUp>()[0], Icons.Cancel);

            Assert.Multiple(() =>
            {
                Assert.That(context.ApiConnection.UpdateCount, Is.EqualTo(0));
                Assert.That(editor.Markup, Does.Not.Contain(context.UserConfig.GetText("email_subject")));
            });
        }

        [Test]
        public async Task Delete_RemovesNotificationAndReportsRemainingIds()
        {
            await using EditorContext context = new(NotificationClient.Report, ReportNotification());
            List<int>? reportedIds = null;
            IRenderedComponent<EditNotifications> editor = context.Render(kInitialIds, ids => reportedIds = ids);

            Click(editor, Icons.Delete);
            Click(editor.FindComponent<ConfirmDelete>(), Icons.Delete);

            Assert.Multiple(() =>
            {
                Assert.That(context.ApiConnection.DeleteCount, Is.EqualTo(1));
                Assert.That(reportedIds, Is.EqualTo(kIdsAfterDelete));
                Assert.That(editor.FindAll("button.btn-warning"), Is.Empty);
            });
        }

        [Test]
        public async Task Delete_RestoresIdsAndReportsErrorWhenMutationFails()
        {
            await using EditorContext context = new(NotificationClient.Report, ReportNotification());
            context.ApiConnection.ThrowOnMutation = true;
            List<List<int>> reportedIds = [];
            IRenderedComponent<EditNotifications> editor = context.Render(kInitialIds, ids => reportedIds.Add(ids));

            Click(editor, Icons.Delete);
            Click(editor.FindComponent<ConfirmDelete>(), Icons.Delete);

            Assert.Multiple(() =>
            {
                Assert.That(reportedIds.Last(), Is.EqualTo(kInitialIds));
                Assert.That(context.Messages, Has.Count.EqualTo(1));
                Assert.That(context.Messages[0].Title, Is.EqualTo(context.UserConfig.GetText("delete_notification")));
            });
        }

        [Test]
        public async Task Delete_ReportsRollbackErrorWhenRestoringIdsFails()
        {
            await using EditorContext context = new(NotificationClient.Report, ReportNotification());
            context.ApiConnection.ThrowOnMutation = true;
            int callbackCount = 0;
            IRenderedComponent<EditNotifications> editor = context.Render(kInitialIds, _ =>
            {
                if (++callbackCount > 1)
                {
                    throw new InvalidOperationException("rollback failed");
                }
            });

            Click(editor, Icons.Delete);
            Click(editor.FindComponent<ConfirmDelete>(), Icons.Delete);

            Assert.Multiple(() =>
            {
                Assert.That(context.Messages, Has.Count.EqualTo(1));
                Assert.That(context.Messages[0].Exception?.Message, Is.EqualTo("rollback failed"));
            });
        }

        [Test]
        public async Task ResetLastSent_ClearsDateWhenNotificationWasUpdated()
        {
            await using EditorContext context = new(NotificationClient.Report, ReportNotification(lastSent: true));
            IRenderedComponent<EditNotifications> editor = context.Render();

            Click(editor, Icons.Cleanup);
            Click(editor.FindComponent<Confirm>(), Icons.Confirm);

            Assert.Multiple(() =>
            {
                Assert.That(context.ApiConnection.ResetCount, Is.EqualTo(1));
                Assert.That(FindButtons(editor, Icons.Cleanup), Is.Empty);
            });
        }

        [Test]
        public async Task ResetLastSent_KeepsDateWhenNoNotificationWasUpdated()
        {
            await using EditorContext context = new(NotificationClient.Report, ReportNotification(lastSent: true));
            context.ApiConnection.ResetAffectedRows = 0;
            IRenderedComponent<EditNotifications> editor = context.Render();

            Click(editor, Icons.Cleanup);
            Click(editor.FindComponent<Confirm>(), Icons.Confirm);

            Assert.That(FindButtons(editor, Icons.Cleanup), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ResetLastSent_ReportsErrorWhenUpdateFails()
        {
            await using EditorContext context = new(NotificationClient.Report, ReportNotification(lastSent: true));
            context.ApiConnection.ThrowOnMutation = true;
            IRenderedComponent<EditNotifications> editor = context.Render();

            Click(editor, Icons.Cleanup);
            Click(editor.FindComponent<Confirm>(), Icons.Confirm);

            Assert.Multiple(() =>
            {
                Assert.That(context.Messages, Has.Count.EqualTo(1));
                Assert.That(context.Messages[0].Title, Is.EqualTo(context.UserConfig.GetText("reset_notification")));
            });
        }

        [Test]
        public async Task Table_ShowsConfiguredResponsiblesWithRequesterAndOtherAddresses()
        {
            FwoNotification notification = RecertNotification();
            notification.RecipientTo = EmailRecipientOption.ConfiguredResponsibles;
            notification.EmailAddressTo = "{\"none\":false,\"owner_responsible_type_ids\":[1],\"requester\":true,\"other_addresses\":true,\"other_address_list\":[\"x@example.test\"]}";
            await using EditorContext context = new(NotificationClient.InterfaceRequest, notification);

            IRenderedComponent<EditNotifications> editor = context.Render();

            Assert.Multiple(() =>
            {
                Assert.That(editor.Markup, Does.Contain("Main"));
                Assert.That(editor.Markup, Does.Contain(context.UserConfig.GetText(nameof(EmailRecipientOption.Requester))));
                Assert.That(editor.Markup, Does.Contain(context.UserConfig.GetText(nameof(EmailRecipientOption.OtherAddresses))));
            });
        }

        [Test]
        public async Task Table_ShowsAddressesOfOtherAddressSelectionJson()
        {
            FwoNotification notification = ReportNotification();
            notification.EmailAddressTo = "{\"none\":false,\"other_addresses\":true,\"other_address_list\":[\"json@example.test\"]}";
            await using EditorContext context = new(NotificationClient.Report, notification);

            IRenderedComponent<EditNotifications> editor = context.Render();

            Assert.That(editor.Markup, Does.Contain("json@example.test"));
        }

        [Test]
        public async Task Table_ShowsOtherAddressesLabelForSelectionJsonWithoutAddresses()
        {
            FwoNotification notification = ReportNotification();
            notification.EmailAddressTo = "{\"none\":false,\"other_addresses\":true,\"other_address_list\":[]}";
            await using EditorContext context = new(NotificationClient.Report, notification);

            IRenderedComponent<EditNotifications> editor = context.Render();

            Assert.Multiple(() =>
            {
                Assert.That(editor.Markup, Does.Contain($"Report: {context.UserConfig.GetText(nameof(EmailRecipientOption.OtherAddresses))}"));
                Assert.That(editor.Markup, Does.Not.Contain("other_address_list"));
            });
        }

        [TestCase(EmailRecipientOption.OwnerGroupOnly)]
        [TestCase(EmailRecipientOption.FallbackToMainResponsibleIfOwnerGroupEmpty)]
        public async Task EditPopup_MigratesLegacyRecipientToConfiguredResponsibles(EmailRecipientOption legacyRecipient)
        {
            FwoNotification notification = RecertNotification();
            notification.RecipientTo = legacyRecipient;
            notification.EmailAddressTo = "";
            await using EditorContext context = new(NotificationClient.Recertification, notification);
            IRenderedComponent<EditNotifications> editor = context.Render();

            Click(editor, Icons.Edit);

            FwoNotification actNotification = GetActNotification(editor);
            Assert.That(actNotification.RecipientTo, Is.EqualTo(EmailRecipientOption.ConfiguredResponsibles));
        }

        [Test]
        public async Task Initialization_ReportsErrorWhenNotificationsCannotBeLoaded()
        {
            await using EditorContext context = new(NotificationClient.Report);
            context.ApiConnection.ThrowOnLoad = true;

            context.Render();

            Assert.That(context.Messages.Select(message => message.Title), Has.Some.EqualTo(context.UserConfig.GetText("fetch_data")));
        }

        [Test]
        public async Task EditPopup_OpenedDropdownsListOptionsAndSetIntervals()
        {
            FwoNotification notification = RecertNotification();
            notification.IntervalBeforeDeadline = null;
            notification.RepeatIntervalAfterDeadline = null;
            await using EditorContext context = new(NotificationClient.Recertification, notification);
            IRenderedComponent<EditNotifications> editor = context.Render();

            Click(editor, Icons.Edit);
            foreach (IElement dropdownInput in editor.FindAll("input[id^='dropdown-input-']"))
            {
                dropdownInput.Focus();
            }
            string daysText = context.UserConfig.GetText(nameof(SchedulerInterval.Days));
            editor.FindAll("button.dropdown-item").First(button => button.TextContent.Trim() == daysText).Click();
            editor.FindAll("button.dropdown-item").Last(button => button.TextContent.Trim() == daysText).Click();

            FwoNotification actNotification = GetActNotification(editor);
            Assert.Multiple(() =>
            {
                Assert.That(actNotification.IntervalBeforeDeadline, Is.EqualTo(SchedulerInterval.Days));
                Assert.That(actNotification.RepeatIntervalAfterDeadline, Is.EqualTo(SchedulerInterval.Days));
                Assert.That(editor.Markup, Does.Contain(context.UserConfig.GetText(EmailPriority.Low.TextKey())));
                Assert.That(editor.Markup, Does.Contain(context.UserConfig.GetText(NotificationLoggingMode.LogOnly)));
            });
        }

        [Test]
        public async Task AddPopup_OpenedDeadlineDropdownListsDeadlineOptions()
        {
            await using EditorContext context = new(NotificationClient.InterfaceRequest);
            IRenderedComponent<EditNotifications> editor = context.Render();

            Click(editor, Icons.Add);
            foreach (IElement dropdownInput in editor.FindAll("input[id^='dropdown-input-']"))
            {
                dropdownInput.Focus();
            }

            Assert.That(editor.Markup, Does.Contain(context.UserConfig.GetText(nameof(NotificationDeadline.RequestDate))));
        }

        [Test]
        public async Task EditPopup_ForAuditor_ShowsDisabledSave()
        {
            await using EditorContext context = new(NotificationClient.Report, ReportNotification(), Roles.Auditor);
            IRenderedComponent<EditNotifications> editor = context.Render();

            Click(editor, Icons.Edit);

            Assert.Multiple(() =>
            {
                Assert.That(FindButtons(editor.FindComponents<PopUp>()[0], Icons.Save), Is.Empty);
                Assert.That(editor.FindComponents<PopUp>()[0].FindAll("button[disabled]"), Is.Not.Empty);
            });
        }

        [Test]
        public async Task Save_ForInterfaceRequest_KeepsRequesterSelectionAndAddsNotification()
        {
            await using EditorContext context = new(NotificationClient.InterfaceRequest);
            IRenderedComponent<EditNotifications> editor = context.Render();

            Click(editor, Icons.Add);
            await SetActNotification(editor, notification => notification.EmailSubject = "Interface requested");
            ClickSave(editor);

            Assert.Multiple(() =>
            {
                Assert.That(context.ApiConnection.AddCount, Is.EqualTo(1));
                Assert.That(context.Messages, Is.Empty);
            });
        }

        private static FwoNotification RecertNotification() => new()
        {
            Id = 1,
            Name = "Recert reminder",
            NotificationClient = NotificationClient.Recertification,
            RecipientTo = EmailRecipientOption.OtherAddresses,
            EmailAddressTo = "a@example.test",
            RecipientBcc = EmailRecipientOption.ConfiguredResponsibles,
            EmailAddressBcc = "{\"none\":false,\"owner_responsible_type_ids\":[2]}",
            EmailSubject = "Recertify",
            Deadline = NotificationDeadline.RecertDate,
            OffsetBeforeDeadline = 2,
            IntervalBeforeDeadline = SchedulerInterval.Weeks,
            RepeatOffsetAfterDeadline = 1,
            RepeatIntervalAfterDeadline = SchedulerInterval.Weeks,
            RepetitionsAfterDeadline = 2,
            EmailPriority = EmailPriority.High
        };

        private static FwoNotification RuleTimerNotification() => new()
        {
            Id = 1,
            Name = "Rule expiry",
            NotificationClient = NotificationClient.RuleTimer,
            RecipientTo = EmailRecipientOption.OtherAddresses,
            EmailAddressTo = "a@example.test",
            EmailSubject = "Expiry",
            Deadline = NotificationDeadline.RuleExpiry,
            RepeatOffsetAfterDeadline = 1,
            RepeatIntervalAfterDeadline = SchedulerInterval.Days,
            RepetitionsAfterDeadline = 2
        };

        private static FwoNotification WorkflowNotification() => new()
        {
            Id = 1,
            Name = "Workflow",
            NotificationClient = NotificationClient.WfAction,
            RecipientTo = EmailRecipientOption.Requester,
            EmailSubject = "Ticket"
        };

        private static FwoNotification ReportNotification(bool lastSent = false) => new()
        {
            Id = 1,
            Name = "Report",
            NotificationClient = NotificationClient.Report,
            RecipientTo = EmailRecipientOption.OtherAddresses,
            EmailAddressTo = "a@example.test",
            EmailSubject = "Report",
            LastSent = lastSent ? new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc) : null
        };

        private static List<IElement> FindButtons<TComponent>(IRenderedComponent<TComponent> component, string icon) where TComponent : IComponent
        {
            string iconClass = $"class=\"{icon}\"";
            return [.. component.FindAll("button").Where(button => button.InnerHtml.Contains(iconClass) && !button.HasAttribute("disabled"))];
        }

        private static void Click<TComponent>(IRenderedComponent<TComponent> component, string icon) where TComponent : IComponent
        {
            List<IElement> buttons = FindButtons(component, icon);
            Assert.That(buttons, Is.Not.Empty, $"No enabled button with icon {icon}.");
            buttons[0].Click();
        }

        private static void ClickSave(IRenderedComponent<EditNotifications> editor)
        {
            Click(editor.FindComponents<PopUp>()[0], Icons.Save);
        }

        private static FwoNotification GetActNotification(IRenderedComponent<EditNotifications> editor)
        {
            System.Reflection.FieldInfo field = typeof(EditNotifications).GetField("actNotification",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(EditNotifications).FullName, "actNotification");
            return (FwoNotification)field.GetValue(editor.Instance)!;
        }

        private static async Task SetActNotification(IRenderedComponent<EditNotifications> editor, Action<FwoNotification> change)
        {
            await editor.InvokeAsync(() => change(GetActNotification(editor)));
        }

        /// <summary>
        /// Render context of the editor with an admin user, a recording API and a message sink.
        /// </summary>
        private sealed class EditorContext : IAsyncDisposable
        {
            private readonly BunitContext context = new();
            private readonly NotificationClient client;

            public EditorContext(NotificationClient client, FwoNotification? notification = null, string role = Roles.Admin)
            {
                this.client = client;
                ApiConnection = new EditNotificationsApiConn { Notifications = notification == null ? new List<FwoNotification>() : new List<FwoNotification> { notification } };
                UserConfig = new SimulatedUserConfig();
                UserConfig.User.Roles = [role];
                UserConfig.SetExecutionMode(role);
                context.JSInterop.Mode = JSRuntimeMode.Loose;
                context.Services.AddLocalization();
                context.Services.AddAuthorizationCore();
                context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
                context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(role));
                context.Services.AddSingleton<DomEventService>();
                context.Services.AddSingleton<ApiConnection>(ApiConnection);
                context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
                context.Services.AddSingleton<UserConfig>(UserConfig);
                context.Services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
            }

            public EditNotificationsApiConn ApiConnection { get; }
            public SimulatedUserConfig UserConfig { get; }
            public List<(Exception? Exception, string Title, string Message)> Messages { get; } = [];

            public IRenderedComponent<EditNotifications> Render(List<int>? notificationIds = null, Action<List<int>>? idsChanged = null,
                NotificationDeadline? deadlineFilter = null)
            {
                IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                    .AddChildContent(builder =>
                    {
                        builder.OpenComponent<CascadingValue<Action<Exception?, string, string, bool>>>(0);
                        builder.AddAttribute(1, "Value", (Action<Exception?, string, string, bool>)((exception, title, message, _) => Messages.Add((exception, title, message))));
                        builder.AddAttribute(2, "IsFixed", true);
                        builder.AddAttribute(3, "ChildContent", (RenderFragment)(childBuilder =>
                        {
                            childBuilder.OpenComponent<EditNotifications>(0);
                            childBuilder.AddAttribute(1, nameof(EditNotifications.Client), client);
                            childBuilder.AddAttribute(2, nameof(EditNotifications.NotificationIds), notificationIds == null ? null : new List<int>(notificationIds));
                            childBuilder.AddAttribute(3, nameof(EditNotifications.NotificationIdsChanged),
                                EventCallback.Factory.Create<List<int>>(this, ids => idsChanged?.Invoke(ids)));
                            childBuilder.AddAttribute(4, nameof(EditNotifications.DeadlineFilter), deadlineFilter);
                            childBuilder.CloseComponent();
                        }));
                        builder.CloseComponent();
                    }));
                return wrapper.FindComponent<EditNotifications>();
            }

            public ValueTask DisposeAsync()
            {
                return context.DisposeAsync();
            }
        }

        /// <summary>
        /// Serves the notification queries and records the mutations; can fail loading or mutations.
        /// </summary>
        private sealed class EditNotificationsApiConn : SimulatedApiConnection
        {
            public List<FwoNotification> Notifications { get; init; } = [];
            public bool ThrowOnLoad { get; set; }
            public bool ThrowOnMutation { get; set; }
            public int ResetAffectedRows { get; set; } = 1;
            public int AddCount { get; private set; }
            public int UpdateCount { get; private set; }
            public int DeleteCount { get; private set; }
            public int ResetCount { get; private set; }
            public string? LastEmailPriority { get; private set; }

            private static readonly List<OwnerResponsibleType> kResponsibleTypes =
            [
                new() { Id = GlobalConst.kOwnerResponsibleTypeMain, Name = "Main", Active = true, SortOrder = 1 },
                new() { Id = GlobalConst.kOwnerResponsibleTypeSupporting, Name = "Supporting", Active = true, SortOrder = 2 }
            ];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == NotificationQueries.getNotifications)
                {
                    return ThrowOnLoad
                        ? throw new InvalidOperationException("load failed")
                        : Task.FromResult((QueryResponseType)(object)Notifications.Select(notification => new FwoNotification(notification)).ToList());
                }
                if (query == OwnerQueries.getOwnerResponsibleTypes)
                {
                    return Task.FromResult((QueryResponseType)(object)kResponsibleTypes.ToList());
                }
                if (ThrowOnMutation)
                {
                    throw new InvalidOperationException("mutation failed");
                }
                return Task.FromResult(HandleMutation<QueryResponseType>(query, variables));
            }

            private QueryResponseType HandleMutation<QueryResponseType>(string query, object? variables)
            {
                if (query == NotificationQueries.addNotification)
                {
                    AddCount++;
                    LastEmailPriority = variables?.GetType().GetProperty("emailPriority")?.GetValue(variables) as string;
                    return (QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId { NewId = 100 }] };
                }
                if (query == NotificationQueries.updateNotification)
                {
                    UpdateCount++;
                    return (QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId { UpdatedId = 1 }] };
                }
                if (query == NotificationQueries.deleteNotification)
                {
                    DeleteCount++;
                    return (QueryResponseType)new object();
                }
                if (query == NotificationQueries.updateNotificationsLastSent)
                {
                    ResetCount++;
                    return (QueryResponseType)(object)new ReturnId { AffectedRows = ResetAffectedRows };
                }
                throw new NotSupportedException($"Unexpected query: {query}");
            }
        }

        private sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
        {
            public LocalizedString this[string name] => new(name, name, resourceNotFound: true);

            public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: true);

            public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
        }
    }
}
