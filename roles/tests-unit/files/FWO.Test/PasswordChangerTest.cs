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
        [TestCaseSource(nameof(InvalidInputCases))]
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

            string policyFailureInput = new('x', 2);
            string result = await changer.ChangePassword(BuildInput('O'), policyFailureInput, policyFailureInput, CreateUserConfig(), globalConfig);

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
            string oldInput = BuildInput('O');
            string newInput = BuildInput('N');

            string result = await changer.ChangePassword(oldInput, newInput, newInput, userConfig, CreateGlobalConfig());

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("ldap says no"));
                Assert.That(middlewareClient.ChangePasswordCallCount, Is.EqualTo(1));
                Assert.That(middlewareClient.LastChangePasswordRequest, Is.Not.Null);
                Assert.That(middlewareClient.LastChangePasswordRequest!.LdapId, Is.EqualTo(23));
                Assert.That(middlewareClient.LastChangePasswordRequest.UserId, Is.EqualTo(42));
                Assert.That(middlewareClient.LastChangePasswordRequest.OldPassword, Is.EqualTo(oldInput));
                Assert.That(middlewareClient.LastChangePasswordRequest.NewPassword, Is.EqualTo(newInput));
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

            string result = await changer.ChangePassword(BuildInput('O'), BuildInput('N'), BuildInput('N'), CreateUserConfig(), CreateGlobalConfig());

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

            string result = await changer.ChangePassword(BuildInput('O'), BuildInput('N'), BuildInput('N'), CreateUserConfig(), CreateGlobalConfig());

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

        private static IEnumerable<TestCaseData> InvalidInputCases()
        {
            string oldInput = BuildInput('O');
            string newInput = BuildInput('N');
            string otherInput = BuildInput('X');
            string sameInput = BuildInput('S');
            yield return new TestCaseData("", newInput, newInput, "E5401");
            yield return new TestCaseData(oldInput, "", "", "E5402");
            yield return new TestCaseData(sameInput, sameInput, sameInput, "E5403");
            yield return new TestCaseData(oldInput, newInput, otherInput, "E5404");
        }

        private static string BuildInput(char marker)
        {
            return string.Concat("Input", marker, 123);
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
