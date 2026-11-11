using FWO.Basics.Exceptions;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Security.Authentication;
using static FWO.Test.FirewallWriteTestSupport;

namespace FWO.Test
{
    /// <summary>
    /// The normalized errors of the firewall write library: the same category and retry decision for every vendor, with the
    /// firewall's native message and code kept.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class FirewallWriteErrorTest
    {
        private const string kCheckPointLogin = "{\"sid\":\"sid-1\"}";
        private const string kFortiManagerOk = "{\"result\":[{\"status\":{\"code\":0}}],\"session\":\"fmg-session\"}";
        private const string kAzureToken = "{\"access_token\":\"token-1\"}";
        private const string kAzureLocation = "{\"location\":\"westeurope\"}";

        private static (FirewallWriter Writer, FirewallSimulation Simulation) Writer(FirewallWriteContext context)
        {
            FirewallSimulation simulation = new();
            return (new FirewallWriter(new FixedFirewallWriteDataSource(context), new SimulatedFirewallWriteClientFactory(simulation)), simulation);
        }

        private static async Task<ModifyFirewallResult> WriteHost(int deviceTypeId, Func<RecordedFirewallRequest, RestResponse> handler)
        {
            (FirewallWriter writer, FirewallSimulation simulation) = Writer(Context(deviceTypeId, configPath: "rg1"));
            simulation.Handler = handler;
            return await writer.ModifyFirewall(Task(WfTaskType.object_create, Network("h1", "10.0.0.1")));
        }

        private static RestResponse TimedOut() => new(new RestRequest())
        {
            ResponseStatus = ResponseStatus.TimedOut,
            ErrorMessage = "The request timed out."
        };

        private static RestResponse NoAnswer(Exception exception) => new(new RestRequest())
        {
            ResponseStatus = ResponseStatus.Error,
            ErrorException = exception,
            ErrorMessage = exception.Message
        };

        [TestCase(FirewallWriteErrorKind.Unreachable, true)]
        [TestCase(FirewallWriteErrorKind.RateLimited, true)]
        [TestCase(FirewallWriteErrorKind.Conflict, true)]
        [TestCase(FirewallWriteErrorKind.FirewallError, true)]
        [TestCase(FirewallWriteErrorKind.Unknown, true)]
        [TestCase(FirewallWriteErrorKind.Configuration, false)]
        [TestCase(FirewallWriteErrorKind.InvalidTaskData, false)]
        [TestCase(FirewallWriteErrorKind.Unsupported, false)]
        [TestCase(FirewallWriteErrorKind.Authentication, false)]
        [TestCase(FirewallWriteErrorKind.NotFound, false)]
        [TestCase(FirewallWriteErrorKind.AlreadyExists, false)]
        [TestCase(FirewallWriteErrorKind.Rejected, false)]
        public void RetryDecision_DependsOnlyOnTheKind(FirewallWriteErrorKind kind, bool retryable)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(FirewallWriteErrors.IsRetryable(kind), Is.EqualTo(retryable));
                Assert.That(new FirewallWriteError(kind, "native").IsRetryable, Is.EqualTo(retryable));
            }
        }

        [TestCase(HttpStatusCode.Unauthorized, FirewallWriteErrorKind.Authentication)]
        [TestCase(HttpStatusCode.Forbidden, FirewallWriteErrorKind.Authentication)]
        [TestCase(HttpStatusCode.NotFound, FirewallWriteErrorKind.NotFound)]
        [TestCase(HttpStatusCode.Conflict, FirewallWriteErrorKind.Conflict)]
        [TestCase(HttpStatusCode.PreconditionFailed, FirewallWriteErrorKind.Conflict)]
        [TestCase(HttpStatusCode.TooManyRequests, FirewallWriteErrorKind.RateLimited)]
        [TestCase(HttpStatusCode.BadGateway, FirewallWriteErrorKind.Unreachable)]
        [TestCase(HttpStatusCode.ServiceUnavailable, FirewallWriteErrorKind.Unreachable)]
        [TestCase(HttpStatusCode.GatewayTimeout, FirewallWriteErrorKind.Unreachable)]
        [TestCase(HttpStatusCode.InternalServerError, FirewallWriteErrorKind.FirewallError)]
        [TestCase(HttpStatusCode.BadRequest, FirewallWriteErrorKind.Rejected)]
        [TestCase(HttpStatusCode.UnprocessableEntity, FirewallWriteErrorKind.Rejected)]
        [TestCase(HttpStatusCode.OK, FirewallWriteErrorKind.Unknown)]
        public void HttpStatus_IsMappedForVendorsWithoutOwnCode(HttpStatusCode statusCode, FirewallWriteErrorKind kind)
        {
            Assert.That(FirewallWriteErrors.FromStatusCode(statusCode), Is.EqualTo(kind));
        }

        [Test]
        public void Transport_DistinguishesNoAnswerFromRefusedCertificate()
        {
            HttpRequestException certificateRefused = new("SSL connection could not be established", new AuthenticationException("remote certificate invalid"));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(FirewallWriteErrors.FromTransport(TimedOut()), Is.EqualTo(FirewallWriteErrorKind.Unreachable));
                Assert.That(FirewallWriteErrors.FromTransport(NoAnswer(new HttpRequestException("Connection refused"))), Is.EqualTo(FirewallWriteErrorKind.Unreachable));
                Assert.That(FirewallWriteErrors.FromTransport(NoAnswer(certificateRefused)), Is.EqualTo(FirewallWriteErrorKind.Configuration));
                Assert.That(FirewallWriteErrors.FromTransport(Response(HttpStatusCode.BadRequest, "{}")), Is.Null);
            }
        }

        [Test]
        public void Exceptions_OutsideAnAnswerAreClassified()
        {
            FirewallWriteError known = new(FirewallWriteErrorKind.NotFound, "native", 404, "code");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(FirewallWriteErrors.FromException(new FirewallWriteException(known, "message")), Is.SameAs(known));
                Assert.That(FirewallWriteErrors.FromException(new ConfigException("template")).Kind, Is.EqualTo(FirewallWriteErrorKind.Configuration));
                Assert.That(FirewallWriteErrors.FromException(new HttpRequestException("down")).Kind, Is.EqualTo(FirewallWriteErrorKind.Unreachable));
                Assert.That(FirewallWriteErrors.FromException(new TaskCanceledException("t", new TimeoutException())).Kind,
                    Is.EqualTo(FirewallWriteErrorKind.Unreachable));
                Assert.That(FirewallWriteErrors.FromException(new InvalidOperationException("odd")).Kind, Is.EqualTo(FirewallWriteErrorKind.Unknown));
            }
        }

        /// <summary>The failed call keeps the firewall's own message, code and status next to the normalized kind.</summary>
        [Test]
        public async Task CheckPoint_ObjectNotFoundKeepsTheNativeError()
        {
            const string nativeError = "{\"code\":\"generic_err_object_not_found\",\"message\":\"Requested object [h1] not found\"}";
            ModifyFirewallResult result = await WriteHost(FirewallVendors.kCheckPointR8x,
                request => request.Resource == "login" ? Ok(kCheckPointLogin) : Response(HttpStatusCode.NotFound, nativeError));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.NotFound));
                Assert.That(result.Error?.NativeCode, Is.EqualTo("generic_err_object_not_found"));
                Assert.That(result.Error?.NativeMessage, Does.Contain("Requested object [h1] not found"));
                Assert.That(result.Error?.StatusCode, Is.EqualTo(404));
                Assert.That(result.Error?.Operation, Is.EqualTo(FirewallWriteOperation.HostCreate));
                Assert.That(result.IsRetryable, Is.False);
                Assert.That(result.Steps.Single().Error, Is.EqualTo(result.Error));
            }
        }

        [TestCase(HttpStatusCode.Conflict, "{\"code\":\"generic_err_object_locked\"}", FirewallWriteErrorKind.Conflict, true)]
        [TestCase(HttpStatusCode.BadRequest, "{\"code\":\"err_validation_failed\",\"errors\":[{\"message\":\"More than one object named 'h1' exists.\"}]}",
            FirewallWriteErrorKind.AlreadyExists, false)]
        [TestCase(HttpStatusCode.BadRequest, "{\"code\":\"generic_err_invalid_parameter\"}", FirewallWriteErrorKind.Rejected, false)]
        [TestCase(HttpStatusCode.InternalServerError, "{\"code\":\"generic_internal_error\"}", FirewallWriteErrorKind.FirewallError, true)]
        [TestCase(HttpStatusCode.ServiceUnavailable, "", FirewallWriteErrorKind.Unreachable, true)]
        public async Task CheckPoint_ErrorCodesAreNormalized(HttpStatusCode statusCode, string content, FirewallWriteErrorKind kind, bool retryable)
        {
            ModifyFirewallResult result = await WriteHost(FirewallVendors.kCheckPointR8x,
                request => request.Resource == "login" ? Ok(kCheckPointLogin) : Response(statusCode, content));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Error?.Kind, Is.EqualTo(kind));
                Assert.That(result.IsRetryable, Is.EqualTo(retryable));
            }
        }

        [Test]
        public async Task LoginFailures_AreAuthenticationErrorsAndNotRetried()
        {
            ModifyFirewallResult checkPoint = await WriteHost(FirewallVendors.kCheckPointR8x,
                _ => Response(HttpStatusCode.BadRequest, "{\"code\":\"err_login_failed\",\"message\":\"Authentication to server failed.\"}"));
            ModifyFirewallResult fortiManager = await WriteHost(FirewallVendors.kFortiManager5ff,
                _ => Ok("{\"result\":[{\"status\":{\"code\":-22,\"message\":\"Login fail\"}}]}"));
            ModifyFirewallResult azure = await WriteHost(FirewallVendors.kAzure,
                _ => Response(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_client\",\"error_description\":\"AADSTS7000215: Invalid client secret.\"}"));
            List<ModifyFirewallResult> results = [checkPoint, fortiManager, azure];
            using (Assert.EnterMultipleScope())
            {
                foreach (ModifyFirewallResult result in results)
                {
                    Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.Authentication), result.Message);
                    Assert.That(result.IsRetryable, Is.False);
                    Assert.That(result.Message, Does.Contain("login"));
                }
                Assert.That(checkPoint.Error?.NativeMessage, Does.Contain("Authentication to server failed."));
                Assert.That(fortiManager.Error?.NativeCode, Is.EqualTo("-22"));
                Assert.That(azure.Error?.NativeCode, Is.EqualTo("invalid_client"));
                Assert.That(azure.Error?.NativeMessage, Does.Contain("AADSTS7000215"));
            }
        }

        [Test]
        public async Task Timeouts_AreRetried()
        {
            ModifyFirewallResult result = await WriteHost(FirewallVendors.kFortiOsGateway, _ => TimedOut());
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.Unreachable));
                Assert.That(result.Error?.StatusCode, Is.Null);
                Assert.That(result.Error?.NativeMessage, Is.EqualTo("The request timed out."));
                Assert.That(result.IsRetryable, Is.True);
            }
        }

        [TestCase("-2", FirewallWriteErrorKind.AlreadyExists)]
        [TestCase("-3", FirewallWriteErrorKind.NotFound)]
        [TestCase("-10", FirewallWriteErrorKind.Rejected)]
        [TestCase("-11", FirewallWriteErrorKind.Authentication)]
        [TestCase("-1", FirewallWriteErrorKind.Unknown)]
        public async Task FortiManager_StatusCodesAreNormalized(string statusCode, FirewallWriteErrorKind kind)
        {
            ModifyFirewallResult result = await WriteHost(FirewallVendors.kFortiManager5ff, request => request.Body!.Contains("/sys/login/user")
                ? Ok(kFortiManagerOk)
                : Ok($"{{\"result\":[{{\"status\":{{\"code\":{statusCode},\"message\":\"native fmg message\"}}}}]}}"));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Error?.Kind, Is.EqualTo(kind));
                Assert.That(result.Error?.NativeCode, Is.EqualTo(statusCode));
                Assert.That(result.Error?.NativeMessage, Does.Contain("native fmg message"));
                Assert.That(result.Error?.StatusCode, Is.EqualTo(200));
            }
        }

        [TestCase(HttpStatusCode.InternalServerError, "{\"status\":\"error\",\"error\":-5}", FirewallWriteErrorKind.AlreadyExists, "-5", false)]
        [TestCase(HttpStatusCode.NotFound, "{\"status\":\"error\",\"error\":-3}", FirewallWriteErrorKind.NotFound, "-3", false)]
        [TestCase(HttpStatusCode.InternalServerError, "{\"status\":\"error\",\"error\":-8}", FirewallWriteErrorKind.Rejected, "-8", false)]
        [TestCase(HttpStatusCode.InternalServerError, "{\"status\":\"error\",\"error\":-9}", FirewallWriteErrorKind.Rejected, "-9", false)]
        [TestCase(HttpStatusCode.InternalServerError, "{\"status\":\"error\",\"error\":-650}", FirewallWriteErrorKind.Rejected, "-650", false)]
        [TestCase(HttpStatusCode.InternalServerError, "{\"status\":\"error\",\"error\":-651}", FirewallWriteErrorKind.Rejected, "-651", false)]
        [TestCase(HttpStatusCode.InternalServerError, "{\"status\":\"error\",\"error\":-6}", FirewallWriteErrorKind.FirewallError, "-6", true)]
        [TestCase(HttpStatusCode.InternalServerError, "{}", FirewallWriteErrorKind.FirewallError, null, true)]
        [TestCase(HttpStatusCode.Unauthorized, "", FirewallWriteErrorKind.Authentication, null, false)]
        public async Task FortiOs_ErrorNumbersAreNormalized(HttpStatusCode statusCode, string content, FirewallWriteErrorKind kind,
            string? nativeCode, bool retryable)
        {
            ModifyFirewallResult result = await WriteHost(FirewallVendors.kFortiOsGateway, _ => Response(statusCode, content));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Error?.Kind, Is.EqualTo(kind));
                Assert.That(result.Error?.NativeCode, Is.EqualTo(nativeCode));
                Assert.That(result.Error?.NativeMessage, Is.EqualTo(content));
                Assert.That(result.Error?.StatusCode, Is.EqualTo((int)statusCode));
                Assert.That(result.IsRetryable, Is.EqualTo(retryable));
            }
        }

        [Test]
        public async Task OpnSense_ValidationErrorsAreRejections()
        {
            ModifyFirewallResult result = await WriteHost(FirewallVendors.kOpnSense,
                _ => Ok("{\"result\":\"failed\",\"validations\":{\"alias.name\":\"An alias with this name already exists.\"}}"));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.Rejected));
                Assert.That(result.Error?.NativeCode, Is.EqualTo("validations"));
                Assert.That(result.Error?.NativeMessage, Does.Contain("An alias with this name already exists."));
                Assert.That(result.IsRetryable, Is.False);
            }
        }

        [TestCase(HttpStatusCode.Forbidden, "AuthorizationFailed", FirewallWriteErrorKind.Authentication, false)]
        [TestCase(HttpStatusCode.Conflict, "AnotherOperationInProgress", FirewallWriteErrorKind.Conflict, true)]
        [TestCase(HttpStatusCode.BadRequest, "ResourceGroupNotFound", FirewallWriteErrorKind.NotFound, false)]
        [TestCase(HttpStatusCode.BadRequest, "InvalidResourceName", FirewallWriteErrorKind.Rejected, false)]
        public async Task Azure_ErrorCodesAreNormalized(HttpStatusCode statusCode, string code, FirewallWriteErrorKind kind, bool retryable)
        {
            ModifyFirewallResult result = await WriteHost(FirewallVendors.kAzure, request => request.Resource switch
            {
                string resource when resource.Contains("oauth2") => Ok(kAzureToken),
                string resource when resource.Contains("/resourcegroups/rg1") => Ok(kAzureLocation),
                _ => Response(statusCode, $"{{\"error\":{{\"code\":\"{code}\",\"message\":\"native azure message\"}}}}")
            });
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Error?.Kind, Is.EqualTo(kind));
                Assert.That(result.Error?.NativeCode, Is.EqualTo(code));
                Assert.That(result.Error?.NativeMessage, Does.Contain("native azure message"));
                Assert.That(result.IsRetryable, Is.EqualTo(retryable));
            }
        }

        [Test]
        public async Task FailedReadOfRuntimeData_IsClassifiedLikeACall()
        {
            ModifyFirewallResult result = await WriteHost(FirewallVendors.kAzure, request => request.Resource switch
            {
                string resource when resource.Contains("oauth2") => Ok(kAzureToken),
                _ => Response(HttpStatusCode.TooManyRequests, "{\"error\":{\"code\":\"SubscriptionRequestsThrottled\"}}")
            });
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.RateLimited));
                Assert.That(result.Message, Does.Contain("Could not read the location"));
                Assert.That(result.IsRetryable, Is.True);
            }
        }

        /// <summary>Failures before anything is sent are classified as well, so the caller can fail the task at once.</summary>
        [Test]
        public async Task FailuresBeforeSending_AreNotRetried()
        {
            FirewallWriteContext noSecret = Context(FirewallVendors.kFortiOsGateway);
            noSecret.Management.ExportCredential.Secret = "";
            GatewayDeviceSpecificSettings brokenTemplate = new();
            brokenTemplate.FwWriteTemplates[FirewallWriteOperation.HostCreate] = "{\"name\":";
            ModifyFirewallResult missingCredentials = await Writer(noSecret).Writer.ModifyFirewall(Task(WfTaskType.object_create, Network("h1", "10.0.0.1")));
            ModifyFirewallResult invalidTemplate = await Writer(Context(FirewallVendors.kCheckPointR8x, brokenTemplate)).Writer
                .ModifyFirewall(Task(WfTaskType.object_create, Network("h1", "10.0.0.1")));
            ModifyFirewallResult missingElements = await Writer(Context(FirewallVendors.kCheckPointR8x)).Writer.ModifyFirewall(Task(WfTaskType.access));
            ModifyFirewallResult unsupportedDevice = await Writer(Context(31)).Writer.ModifyFirewall(Task(WfTaskType.group_delete));
            List<ModifyFirewallResult> results = [missingCredentials, invalidTemplate, missingElements, unsupportedDevice];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(missingCredentials.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.Configuration));
                Assert.That(invalidTemplate.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.Configuration));
                Assert.That(missingElements.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
                Assert.That(unsupportedDevice.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.Unsupported));
                Assert.That(results.Any(result => result.IsRetryable), Is.False);
            }
        }

        [Test]
        public async Task Success_HasNoError()
        {
            ModifyFirewallResult result = await WriteHost(FirewallVendors.kFortiOsGateway, _ => Ok("{\"status\":\"success\"}"));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Error, Is.Null);
                Assert.That(result.IsRetryable, Is.False);
                Assert.That(result.Steps.Single().Error, Is.Null);
            }
        }
    }
}
