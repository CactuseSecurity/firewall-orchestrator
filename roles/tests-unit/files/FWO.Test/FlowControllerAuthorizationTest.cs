using FWO.Basics;
using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Authorization;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test;

[TestFixture]
internal class FlowControllerAuthorizationTest
{
    [TestCase(typeof(FlowCatalogController), nameof(FlowCatalogController.GetAddressObjects))]
    [TestCase(typeof(FlowCatalogController), nameof(FlowCatalogController.GetAddressGroups))]
    [TestCase(typeof(FlowCatalogController), nameof(FlowCatalogController.GetServiceObjects))]
    [TestCase(typeof(FlowCatalogController), nameof(FlowCatalogController.GetServiceGroups))]
    [TestCase(typeof(FlowCatalogController), nameof(FlowCatalogController.GetTimeObjects))]
    [TestCase(typeof(FlowCatalogController), nameof(FlowCatalogController.GetServiceObjectId))]
    [TestCase(typeof(FlowCatalogController), nameof(FlowCatalogController.GetAddressObjectId))]
    [TestCase(typeof(FlowComplianceController), nameof(FlowComplianceController.GetFlowComplianceState))]
    [TestCase(typeof(FlowComplianceController), nameof(FlowComplianceController.GetPolicyIds))]
    [TestCase(typeof(FlowRequestController), nameof(FlowRequestController.GetNetObjectValidity))]
    [TestCase(typeof(FlowRequestController), nameof(FlowRequestController.GetNetGroupValidity))]
    [TestCase(typeof(FlowRequestController), nameof(FlowRequestController.GetRequestStatus))]
    public void ReadOnlyFlowEndpoints_AllowAdminAndAuditor(Type controllerType, string methodName)
    {
        AuthorizeAttribute authorize = GetAuthorizeAttribute(controllerType, methodName);

        Assert.That(authorize.Roles, Is.EqualTo($"{Roles.Admin}, {Roles.Auditor}"));
    }

    [TestCase(typeof(FlowRequestController), nameof(FlowRequestController.GenerateAddressObjectName))]
    [TestCase(typeof(FlowRequestController), nameof(FlowRequestController.GenerateServiceObjectName))]
    [TestCase(typeof(FlowRequestController), nameof(FlowRequestController.CreateRequest))]
    public void WriteOrGenerationFlowEndpoints_RemainAdminOnly(Type controllerType, string methodName)
    {
        AuthorizeAttribute authorize = GetAuthorizeAttribute(controllerType, methodName);

        Assert.That(authorize.Roles, Is.EqualTo($"{Roles.Admin}"));
    }

    private static AuthorizeAttribute GetAuthorizeAttribute(Type controllerType, string methodName)
    {
        MethodInfo method = controllerType.GetMethod(methodName)!;
        AuthorizeAttribute? authorizeAttribute = method.GetCustomAttribute<AuthorizeAttribute>();

        Assert.That(authorizeAttribute, Is.Not.Null, $"Expected [Authorize] on {controllerType.Name}.{methodName}.");
        return authorizeAttribute!;
    }
}
