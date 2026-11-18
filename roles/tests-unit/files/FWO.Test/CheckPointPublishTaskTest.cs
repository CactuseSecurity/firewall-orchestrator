using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Text.Json.Nodes;
using static FWO.Test.FirewallWriteTestSupport;

namespace FWO.Test
{
    /// <summary>
    /// Tests that a Check Point publish only counts as successful once its task has succeeded: the answer of the publish
    /// call only starts the task. A failed task, an unreadable task or one still running at the end of the response
    /// timeout fails the write, so that the session is discarded instead of being left behind unpublished.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class CheckPointPublishTaskTest
    {
        private const string kCheckPointLogin = "{\"sid\":\"sid-1\"}";
        private const string kTaskId = "task-1";
        private const string kPublishStarted = "{\"task-id\":\"task-1\"}";
        private const string kTaskInProgress = "{\"tasks\":[{\"task-id\":\"task-1\",\"status\":\"in progress\",\"progress-percentage\":50}]}";
        private const string kTaskSucceeded = "{\"tasks\":[{\"task-id\":\"task-1\",\"status\":\"succeeded\",\"progress-percentage\":100}]}";
        private const string kTaskFailed = "{\"tasks\":[{\"task-id\":\"task-1\",\"status\":\"failed\",\"task-details\":[{\"fault-message\":\"Session needs approval\"}]}]}";
        private const int kShortTimeoutSeconds = 3;

        private static readonly List<string> SucceededCalls = ["login", "add-host", "publish", "show-task", "show-task", "logout"];
        private static readonly List<string> FailedCalls = ["login", "add-host", "publish", "show-task", "discard", "logout"];

        /// <summary>Answers the publish with a task and every read of the task with the next of the given answers; the last one repeats.</summary>
        private static FirewallSimulation Simulation(string publishAnswer, params RestResponse[] taskAnswers)
        {
            int taskReads = 0;
            return new FirewallSimulation
            {
                Handler = request => request.Resource switch
                {
                    "login" => Ok(kCheckPointLogin),
                    "publish" => Ok(publishAnswer),
                    "show-task" => taskAnswers[Math.Min(taskReads++, taskAnswers.Length - 1)],
                    _ => Ok("{}")
                }
            };
        }

        private static async Task<ModifyFirewallResult> Write(FirewallSimulation simulation)
        {
            FirewallWriteContext context = Context(FirewallVendors.kCheckPointR8x);
            FirewallWriter writer = new(new FixedFirewallWriteDataSource(context), new SimulatedFirewallWriteClientFactory(simulation));
            return await writer.ModifyFirewall(Task(WfTaskType.object_create, Network("h1", "10.0.0.1")));
        }

        private static RenderedFirewallRequest PublishRequest()
        {
            return new RenderedFirewallRequest
            {
                Step = new FirewallWriteStep(FirewallWriteOperation.Publish) { TargetName = "mgmt" },
                Method = "POST",
                Path = "publish",
                Body = "{}"
            };
        }

        private static List<string> Resources(FirewallSimulation simulation) => [.. simulation.Requests.Select(request => request.Resource)];

        [Test]
        public async Task PublishTaskThatSucceeds_CountsAsPublished()
        {
            FirewallSimulation simulation = Simulation(kPublishStarted, Ok(kTaskInProgress), Ok(kTaskSucceeded));

            ModifyFirewallResult result = await Write(simulation);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(Resources(simulation), Is.EqualTo(SucceededCalls));
                Assert.That(JsonNode.Parse(simulation.Requests[3].Body!)!["task-id"]!.GetValue<string>(), Is.EqualTo(kTaskId));
                Assert.That(simulation.Requests[3].Headers["X-chkp-sid"], Is.EqualTo("sid-1"));
                Assert.That(result.Steps[^1].Message, Does.Contain("succeeded"));
            }
        }

        [Test]
        public async Task PublishTaskThatFails_FailsTheWriteAndDiscardsTheSession()
        {
            FirewallSimulation simulation = Simulation(kPublishStarted, Ok(kTaskFailed));

            ModifyFirewallResult result = await Write(simulation);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.Rejected));
                Assert.That(result.Error?.Operation, Is.EqualTo(FirewallWriteOperation.Publish));
                Assert.That(result.Error?.NativeMessage, Does.Contain("Session needs approval"));
                Assert.That(Resources(simulation), Is.EqualTo(FailedCalls));
            }
        }

        [Test]
        public async Task PublishTaskThatCannotBeRead_FailsTheWrite()
        {
            FirewallSimulation simulation = Simulation(kPublishStarted,
                Response(HttpStatusCode.NotFound, "{\"code\":\"generic_err_object_not_found\",\"message\":\"Task not found\"}"));

            ModifyFirewallResult result = await Write(simulation);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.NotFound));
                Assert.That(result.Error?.NativeMessage, Does.Contain("Task not found"));
                Assert.That(Resources(simulation), Is.EqualTo(FailedCalls));
            }
        }

        [Test]
        public async Task PublishTaskStillRunningAtTheTimeout_FailsAsUnreachable()
        {
            FirewallSimulation simulation = Simulation(kPublishStarted, Ok(kTaskInProgress));
            SimulatedCheckPointWriteClient client = new(Management(FirewallVendors.kCheckPointR8x), simulation, kShortTimeoutSeconds);
            await client.Login();

            FirewallStepResult result = await client.Execute(PublishRequest());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.Unreachable));
                Assert.That(result.Error?.IsRetryable, Is.True);
                Assert.That(result.Message, Does.Contain($"did not finish within {kShortTimeoutSeconds} seconds"));
                Assert.That(Resources(simulation).Count(resource => resource == "show-task"), Is.EqualTo(kShortTimeoutSeconds));
            }
        }

        [Test]
        public async Task PublishAnswerWithoutTaskId_IsKeptAsItIs()
        {
            FirewallSimulation simulation = Simulation("{}", Ok(kTaskFailed));
            SimulatedCheckPointWriteClient client = new(Management(FirewallVendors.kCheckPointR8x), simulation);
            await client.Login();

            FirewallStepResult result = await client.Execute(PublishRequest());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(Resources(simulation), Does.Not.Contain("show-task"));
            }
        }

        [Test]
        public async Task OtherOperations_DoNotReadATask()
        {
            FirewallSimulation simulation = new() { Handler = request => Ok(request.Resource == "login" ? kCheckPointLogin : kPublishStarted) };
            SimulatedCheckPointWriteClient client = new(Management(FirewallVendors.kCheckPointR8x), simulation);
            await client.Login();

            FirewallStepResult result = await client.Execute(new RenderedFirewallRequest
            {
                Step = new FirewallWriteStep(FirewallWriteOperation.HostCreate) { TargetName = "h1" },
                Method = "POST",
                Path = "add-host",
                Body = "{\"name\":\"h1\",\"ip-address\":\"10.0.0.1\"}"
            });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(Resources(simulation), Does.Not.Contain("show-task"));
            }
        }
    }
}
