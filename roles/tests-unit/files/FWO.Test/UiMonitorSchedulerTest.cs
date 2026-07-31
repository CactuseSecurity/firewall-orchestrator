using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Test.Mocks;
using FWO.Ui.Pages.Monitoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiMonitorSchedulerTest
    {
        private static MethodInfo GetPrivateMethod(string name, params Type[] parameterTypes)
        {
            return typeof(MonitorScheduler).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance, null, parameterTypes, null)
                ?? throw new MissingMethodException(typeof(MonitorScheduler).FullName, name);
        }

        private static T GetPrivateField<T>(MonitorScheduler component, string fieldName)
        {
            FieldInfo? field = typeof(MonitorScheduler).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(MonitorScheduler).FullName, fieldName);
            }
            return (T)field.GetValue(component)!;
        }

        private static void SetPrivateProperty<T>(MonitorScheduler component, string propertyName, T value)
        {
            PropertyInfo? property = typeof(MonitorScheduler).GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingMemberException(typeof(MonitorScheduler).FullName, propertyName);
            }
            property.SetValue(component, value);
        }

        private static TestSetup RenderComponent(TestMiddlewareClient client)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(new SimulatedApiConnection());
            context.Services.AddSingleton<MiddlewareClient>(client);
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            IRenderedComponent<CascadingAuthenticationState> component = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<MonitorScheduler>());
            IRenderedComponent<MonitorScheduler> scheduler = component.FindComponent<MonitorScheduler>();
            return new TestSetup(context, scheduler, scheduler.Instance);
        }

        [Test]
        public void OnInitializedAsync_LoadsJobsAndSortsThemByName()
        {
            SchedulerSequenceHandler handler = new();
            handler.NextJobs = CreateJobs("Zulu", "Alpha");
            handler.RefreshedJobs = CreateJobs("Zulu", "Alpha");
            TestMiddlewareClient client = new();
            client.UseHandler(handler);

            using TestSetup setup = RenderComponent(client);

            List<SchedulerJobInfo> jobs = GetPrivateField<List<SchedulerJobInfo>>(setup.Component, "jobs");
            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<bool>(setup.Component, "initComplete"), Is.True);
                Assert.That(jobs, Has.Count.EqualTo(2));
                Assert.That(jobs[0].JobName, Is.EqualTo("Alpha"));
                Assert.That(jobs[1].JobName, Is.EqualTo("Zulu"));
                Assert.That(handler.GetJobsCalls, Is.GreaterThanOrEqualTo(1));
            });
        }

        [Test]
        public void Formatters_ReturnExpectedMarkupForCommonBranches()
        {
            SchedulerSequenceHandler handler = new();
            handler.NextJobs = CreateJobs("Alpha");
            TestMiddlewareClient client = new();
            client.UseHandler(handler);

            using TestSetup setup = RenderComponent(client);

            object[] blankArgs = new object[1];
            blankArgs[0] = string.Empty;
            object[] filledArgs = new object[1];
            filledArgs[0] = "every 10 minutes";
            object?[] noRunArgs = new object?[2];
            noRunArgs[0] = (object?)null;
            noRunArgs[1] = false;
            object[] startNowArgs = new object[2];
            startNowArgs[0] = DateTimeOffset.Now.AddSeconds(5);
            startNowArgs[1] = true;
            object[] successArgs = new object[1];
            successArgs[0] = new SchedulerJobInfo { LastExecutionStatus = SchedulerJobExecutionStatus.Success };
            object[] failedArgs = new object[1];
            failedArgs[0] = new SchedulerJobInfo { LastExecutionStatus = SchedulerJobExecutionStatus.Failed };
            object[] emptyArgs = new object[1];
            emptyArgs[0] = new SchedulerJobInfo { LastExecutionStatus = SchedulerJobExecutionStatus.None };

            MarkupString blankInterval = (MarkupString)GetPrivateMethod("FormatInterval", typeof(string)).Invoke(setup.Component, blankArgs)!;
            MarkupString filledInterval = (MarkupString)GetPrivateMethod("FormatInterval", typeof(string)).Invoke(setup.Component, filledArgs)!;
            MarkupString noRunTime = (MarkupString)GetPrivateMethod("FormatRunTime", typeof(DateTimeOffset?), typeof(bool)).Invoke(setup.Component, noRunArgs)!;
            MarkupString startingNow = (MarkupString)GetPrivateMethod("FormatRunTime", typeof(DateTimeOffset?), typeof(bool)).Invoke(setup.Component, startNowArgs)!;
            MarkupString successStatus = (MarkupString)GetPrivateMethod("FormatExecutionStatus", typeof(SchedulerJobInfo)).Invoke(setup.Component, successArgs)!;
            MarkupString failedStatus = (MarkupString)GetPrivateMethod("FormatExecutionStatus", typeof(SchedulerJobInfo)).Invoke(setup.Component, failedArgs)!;
            MarkupString emptyStatus = (MarkupString)GetPrivateMethod("FormatExecutionStatus", typeof(SchedulerJobInfo)).Invoke(setup.Component, emptyArgs)!;

            Assert.Multiple(() =>
            {
                Assert.That(blankInterval.ToString(), Does.Contain("text-muted"));
                Assert.That(filledInterval.ToString(), Does.Contain("scheduler_interval_description"));
                Assert.That(noRunTime.ToString(), Does.Contain("text-muted"));
                Assert.That(startingNow.ToString(), Is.EqualTo("scheduler_now"));
                Assert.That(successStatus.ToString(), Does.Contain("text-success"));
                Assert.That(failedStatus.ToString(), Does.Contain("text-danger"));
                Assert.That(emptyStatus.ToString(), Does.Contain("text-muted"));
            });
        }

        [Test]
        public async Task TriggerJob_RunsTheJobAndRefreshesTheList()
        {
            SchedulerSequenceHandler handler = new();
            handler.NextJobs = CreateJobs("Initial");
            handler.RefreshedJobs = CreateJobs("Refreshed");
            TestMiddlewareClient client = new();
            client.UseHandler(handler);

            using TestSetup setup = RenderComponent(client);

            object[] triggerArgs = new object[1];
            triggerArgs[0] = "Initial";
            await setup.Rendered.InvokeAsync(() => (Task)GetPrivateMethod("TriggerJob", typeof(string)).Invoke(setup.Component, triggerArgs)!);

            List<SchedulerJobInfo> jobs = GetPrivateField<List<SchedulerJobInfo>>(setup.Component, "jobs");
            Assert.Multiple(() =>
            {
                Assert.That(handler.RunCalls, Is.EqualTo(1));
                Assert.That(handler.RunJobNames, Has.Count.EqualTo(1));
                Assert.That(handler.RunJobNames[0], Is.EqualTo("Initial"));
                Assert.That(handler.GetJobsCalls, Is.GreaterThanOrEqualTo(2));
                Assert.That(GetPrivateField<string?>(setup.Component, "runningJob"), Is.Null);
                Assert.That(jobs, Has.Count.EqualTo(1));
                Assert.That(jobs[0].JobName, Is.EqualTo("Refreshed"));
            });
        }

        [Test]
        public async Task LoadJobs_WhenMiddlewareFails_ShowsUiMessage()
        {
            SchedulerSequenceHandler handler = new();
            handler.NextJobs = CreateJobs("Alpha");
            TestMiddlewareClient client = new();
            client.UseHandler(handler);

            using TestSetup setup = RenderComponent(client);
            setup.Component.Dispose();
            List<(string Title, string Message, bool ErrorFlag)> messages = new();
            SetPrivateProperty(setup.Component, "DisplayMessageInUi", new Action<Exception?, string, string, bool>((exception, title, message, errorFlag) =>
            {
                messages.Add((title, message, errorFlag));
            }));

            client.UseHandler(new SchedulerFailureHandler());
            object[] noArgs = Array.Empty<object>();
            await (Task)GetPrivateMethod("LoadJobs").Invoke(setup.Component, noArgs)!;

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("scheduler_fetch_jobs"));
                Assert.That(messages[0].ErrorFlag, Is.True);
                Assert.That(GetPrivateField<List<SchedulerJobInfo>>(setup.Component, "jobs"), Has.Count.EqualTo(1));
            });
        }

        private static List<SchedulerJobInfo> CreateJobs(params string[] jobNames)
        {
            List<SchedulerJobInfo> jobs = new();
            for (int index = 0; index < jobNames.Length; index++)
            {
                string jobName = jobNames[index];
                jobs.Add(new SchedulerJobInfo
                {
                    JobName = jobName,
                    Group = "group",
                    IntervalDescription = index == 0 ? "" : "every hour",
                    LastFireTimeUtc = index == 0 ? null : new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero),
                    NextFireTimeUtc = index == 0 ? DateTimeOffset.Now.AddSeconds(5) : new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.Zero),
                    LastExecutionStatus = index == 0 ? SchedulerJobExecutionStatus.Success : SchedulerJobExecutionStatus.Failed,
                    LastExecutionError = index == 0 ? string.Empty : "boom"
                });
            }
            return jobs;
        }

        private sealed record TestSetup(BunitContext Context, IRenderedComponent<MonitorScheduler> Rendered, MonitorScheduler Component) : IDisposable
        {
            public void Dispose()
            {
                Context.Dispose();
            }
        }
    }

    internal sealed class SchedulerFailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(SchedulerResponseHelper.JsonResponse(HttpStatusCode.InternalServerError, "{\"error\":\"failed\"}"));
        }
    }

    internal sealed class SchedulerSequenceHandler : HttpMessageHandler
    {
        public List<SchedulerJobInfo> NextJobs { get; set; } = new();
        public List<SchedulerJobInfo> RefreshedJobs { get; set; } = new();
        public int GetJobsCalls { get; private set; }
        public int RunCalls { get; private set; }
        public List<string> RunJobNames { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri != null && request.RequestUri.AbsolutePath.EndsWith("/Scheduler", StringComparison.OrdinalIgnoreCase))
            {
                GetJobsCalls++;
                List<SchedulerJobInfo> jobs = GetJobsCalls > 1 ? RefreshedJobs : NextJobs;
                return SchedulerResponseHelper.JsonResponse(HttpStatusCode.OK, JsonSerializer.Serialize(jobs));
            }

            if (request.RequestUri != null && request.RequestUri.AbsolutePath.EndsWith("/Scheduler/Run", StringComparison.OrdinalIgnoreCase))
            {
                string body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
                RunCalls++;
                if (!string.IsNullOrWhiteSpace(body))
                {
                    using JsonDocument document = JsonDocument.Parse(body);
                    if (document.RootElement.TryGetProperty("JobName", out JsonElement jobName)
                        || document.RootElement.TryGetProperty("jobName", out jobName))
                    {
                        RunJobNames.Add(jobName.GetString() ?? string.Empty);
                    }
                }
                return SchedulerResponseHelper.JsonResponse(HttpStatusCode.OK, "true");
            }

            return SchedulerResponseHelper.JsonResponse(HttpStatusCode.NotFound, "{}");
        }
    }

    internal static class SchedulerResponseHelper
    {
        public static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
                ReasonPhrase = statusCode == HttpStatusCode.OK ? "OK" : "Internal Server Error"
            };
        }
    }
}
