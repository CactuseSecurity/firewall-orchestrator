using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Test.Mocks;
using FWO.Ui.Services;
using NUnit.Framework;
using RestSharp;
using System.Net;

namespace FWO.Test
{
    [TestFixture]
    public class PasswordChangerTest
    {
        private const string OldInput = "OldValue1";
        private const string NewInput = "NewValue1";
        private const string OtherInput = "OtherValue1";
        private const string SameInput = "SameValue1";

        [TestCase("", NewInput, NewInput, "E5401")]
        [TestCase(OldInput, "", "", "E5402")]
        [TestCase(SameInput, SameInput, SameInput, "E5403")]
        [TestCase(OldInput, NewInput, OtherInput, "E5404")]
        public async Task ChangePasswordRejectsInvalidInputBeforeCallingMiddleware(string oldPassword, string newPassword1, string newPassword2, string expectedError)
        {
            MockMiddlewareClient middlewareClient = new();
            PasswordChanger changer = new(middlewareClient);

            string result = await changer.ChangePassword(oldPassword, newPassword1, newPassword2, CreateUserConfig(), CreateGlobalConfig());

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expectedError));
                Assert.That(middlewareClient.ChangePasswordCallCount, Is.EqualTo(0));
                Assert.That(middlewareClient.LastChangePasswordRequest, Is.Null);
            });
        }

        [Test]
        public async Task ChangePasswordRejectsPasswordPolicyViolationBeforeCallingMiddleware()
        {
            MockMiddlewareClient middlewareClient = new();
            PasswordChanger changer = new(middlewareClient);
            GlobalConfig globalConfig = CreateGlobalConfig();
            globalConfig.PwMinLength = 12;

            string result = await changer.ChangePassword(OldInput, "short", "short", CreateUserConfig(), globalConfig);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("E541112"));
                Assert.That(middlewareClient.ChangePasswordCallCount, Is.EqualTo(0));
            });
        }

        [Test]
        public async Task ChangePasswordSendsExpectedMiddlewareParametersAndReturnsResponseData()
        {
            MockMiddlewareClient middlewareClient = new()
            {
                ChangePasswordResponse = CreateStringResponse(HttpStatusCode.OK, "ldap says no")
            };
            PasswordChanger changer = new(middlewareClient);
            UserConfig userConfig = CreateUserConfig();
            userConfig.SetExecutionMode(Roles.Admin);

            string result = await changer.ChangePassword(OldInput, NewInput, NewInput, userConfig, CreateGlobalConfig());

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("ldap says no"));
                Assert.That(middlewareClient.ChangePasswordCallCount, Is.EqualTo(1));
                Assert.That(middlewareClient.LastChangePasswordRequest, Is.Not.Null);
                Assert.That(middlewareClient.LastChangePasswordRequest!.LdapId, Is.EqualTo(23));
                Assert.That(middlewareClient.LastChangePasswordRequest.UserId, Is.EqualTo(42));
                Assert.That(middlewareClient.LastChangePasswordRequest.OldPassword, Is.EqualTo(OldInput));
                Assert.That(middlewareClient.LastChangePasswordRequest.NewPassword, Is.EqualTo(NewInput));
                Assert.That(middlewareClient.LastChangePasswordRequest.ExecutionMode, Is.EqualTo(Roles.Admin));
            });
        }

        [TestCase(HttpStatusCode.InternalServerError, "error", "internal error")]
        [TestCase(HttpStatusCode.OK, null, "internal error")]
        public async Task ChangePasswordReturnsInternalErrorForFailedOrEmptyMiddlewareResponse(HttpStatusCode statusCode, string? responseData, string expectedError)
        {
            MockMiddlewareClient middlewareClient = new()
            {
                ChangePasswordResponse = CreateStringResponse(statusCode, responseData)
            };
            PasswordChanger changer = new(middlewareClient);

            string result = await changer.ChangePassword(OldInput, NewInput, NewInput, CreateUserConfig(), CreateGlobalConfig());

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expectedError));
                Assert.That(middlewareClient.ChangePasswordCallCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task ChangePasswordReturnsExceptionMessageWhenMiddlewareCallThrows()
        {
            MockMiddlewareClient middlewareClient = new()
            {
                ChangePasswordException = new InvalidOperationException("middleware unavailable")
            };
            PasswordChanger changer = new(middlewareClient);

            string result = await changer.ChangePassword(OldInput, NewInput, NewInput, CreateUserConfig(), CreateGlobalConfig());

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("middleware unavailable"));
                Assert.That(middlewareClient.ChangePasswordCallCount, Is.EqualTo(1));
            });
        }

        private static UserConfig CreateUserConfig()
        {
            UserConfig userConfig = new SimulatedUserConfig();
            userConfig.User.DbId = 42;
            userConfig.User.Language = GlobalConst.kEnglish;
            userConfig.User.Roles = [Roles.Modeller, Roles.Admin];
            userConfig.User.LdapConnection = new UiLdapConnection { Id = 23 };
            return userConfig;
        }

        private static GlobalConfig CreateGlobalConfig()
        {
            return new SimulatedGlobalConfig
            {
                PwMinLength = 3,
                PwUpperCaseRequired = false,
                PwLowerCaseRequired = false,
                PwNumberRequired = false,
                PwSpecialCharactersRequired = false
            };
        }

        private static RestResponse<string> CreateStringResponse(HttpStatusCode statusCode, string? data)
        {
            return new RestResponse<string>(new RestRequest())
            {
                StatusCode = statusCode,
                Data = data,
                ResponseStatus = statusCode == HttpStatusCode.OK ? ResponseStatus.Completed : ResponseStatus.Error,
                IsSuccessStatusCode = statusCode == HttpStatusCode.OK
            };
        }
    }
}
