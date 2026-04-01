using FWO.Api.Client;
using GraphQL;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    public class GraphQlApiSubscriptionTest
    {
        [Test]
        public void IsJwtExpired_WhenExceptionContainsMarker_ReturnsTrue()
        {
            bool isJwtExpired = InvokeIsJwtExpired(new InvalidOperationException("Connection closed because JWTExpired."));

            Assert.That(isJwtExpired, Is.True);
        }

        [Test]
        public void IsJwtExpired_WhenResponseContainsInvalidJwtCode_ReturnsTrue()
        {
            GraphQLResponse<dynamic> response = new()
            {
                Errors =
                [
                    new GraphQLError
                    {
                        Message = "invalid-jwt",
                        Extensions = new Map
                        {
                            { "code", "invalid-jwt" }
                        }
                    }
                ]
            };

            bool isJwtExpired = InvokeIsJwtExpired(response);

            Assert.That(isJwtExpired, Is.True);
        }

        [Test]
        public void IsJwtExpired_WhenResponseDoesNotContainJwtIndicators_ReturnsFalse()
        {
            GraphQLResponse<dynamic> response = new()
            {
                Errors =
                [
                    new GraphQLError
                    {
                        Message = "permission denied",
                        Extensions = new Map
                        {
                            { "code", "access-denied" }
                        }
                    }
                ]
            };

            bool isJwtExpired = InvokeIsJwtExpired(response);

            Assert.That(isJwtExpired, Is.False);
        }

        private static bool InvokeIsJwtExpired(Exception exception)
        {
            MethodInfo? method = typeof(GraphQlApiSubscription<object>).GetMethod("IsJwtExpired", BindingFlags.NonPublic | BindingFlags.Static, null, [typeof(Exception)], null);
            Assert.That(method, Is.Not.Null);
            return (bool)(method!.Invoke(null, [exception]) ?? false);
        }

        private static bool InvokeIsJwtExpired(GraphQLResponse<dynamic> response)
        {
            MethodInfo? method = typeof(GraphQlApiSubscription<object>).GetMethod("IsJwtExpired", BindingFlags.NonPublic | BindingFlags.Static, null, [typeof(GraphQLResponse<object>)], null);
            Assert.That(method, Is.Not.Null);
            return (bool)(method!.Invoke(null, [response]) ?? false);
        }
    }
}
