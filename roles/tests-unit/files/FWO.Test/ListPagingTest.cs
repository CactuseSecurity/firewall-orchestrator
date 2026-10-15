using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class ListPagingTest
    {
        private static readonly List<int> kThreeItems = [1, 2, 3];
        private static readonly List<int> kFirstTwoItems = [1, 2];
        private static readonly List<string> kPagedRootKeys = ["filter", "limit", "offset"];

        [Test]
        public void FromLookaheadTrimsTheExtraItemAndReportsMore()
        {
            ListPage<int> page = ListPage<int>.FromLookahead(kThreeItems.ToList(), 2);

            Assert.Multiple(() =>
            {
                Assert.That(page.Items, Is.EqualTo(kFirstTwoItems));
                Assert.That(page.HasMore, Is.True);
            });
        }

        [TestCase(3)]
        [TestCase(5)]
        public void FromLookaheadWithoutExtraItemReportsNoMore(int pageSize)
        {
            ListPage<int> page = ListPage<int>.FromLookahead(kThreeItems.ToList(), pageSize);

            Assert.Multiple(() =>
            {
                Assert.That(page.Items, Is.EqualTo(kThreeItems));
                Assert.That(page.HasMore, Is.False);
            });
        }

        [Test]
        public void LookaheadVariablesRequestOneItemMoreThanThePage()
        {
            Dictionary<string, object> variables = [];

            ListPaging.AddLookaheadPagingVariables(variables, 50, 100);

            Assert.Multiple(() =>
            {
                Assert.That(variables["limit"], Is.EqualTo(51));
                Assert.That(variables["offset"], Is.EqualTo(100));
            });
        }

        [Test]
        public void LookaheadVariablesWithoutOffsetSkipNothing()
        {
            Dictionary<string, object> variables = [];

            ListPaging.AddLookaheadPagingVariables(variables, 50, null);

            Assert.That(variables["offset"], Is.EqualTo(0));
        }

        [TestCase(true, "true")]
        [TestCase(false, "false")]
        public void SetHasMoreHeaderWritesTheHeader(bool hasMore, string expectedValue)
        {
            DefaultHttpContext context = new();

            ListPaging.SetHasMoreHeader(context, hasMore);

            Assert.That(context.Response.Headers[ListPaging.kHasMoreHeader].ToString(), Is.EqualTo(expectedValue));
        }

        [Test]
        public void SetHasMoreHeaderWithoutContextDoesNothing()
        {
            Assert.DoesNotThrow(() => ListPaging.SetHasMoreHeader(null, true));
        }

        [Test]
        public void CatalogRequestsDefaultToTheMaximumPageFromTheStart()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new GetAddressObjectsRequest().Limit, Is.EqualTo(FlowCatalogPaging.kMaxObjectLimit));
                Assert.That(new GetServiceObjectsRequest().Limit, Is.EqualTo(FlowCatalogPaging.kMaxObjectLimit));
                Assert.That(new GetTimeObjectsRequest().Limit, Is.EqualTo(FlowCatalogPaging.kMaxObjectLimit));
                Assert.That(new GetAddressGroupsRequest().Limit, Is.EqualTo(FlowCatalogPaging.kMaxGroupLimit));
                Assert.That(new GetServiceGroupsRequest().Limit, Is.EqualTo(FlowCatalogPaging.kMaxGroupLimit));
                Assert.That(new GetAddressObjectsRequest().Offset, Is.Null);
            });
        }

        [Test]
        public void ValidPagingPasses()
        {
            GetAddressObjectsRequest request = new() { Limit = FlowCatalogPaging.kMaxObjectLimit, Offset = 0 };

            Assert.Multiple(() =>
            {
                Assert.That(FlowCatalogPaging.TryValidate(request, FlowCatalogPaging.kMaxObjectLimit, out ActionResult? errorResult), Is.True);
                Assert.That(errorResult, Is.Null);
            });
        }

        [Test]
        public void OmittedOffsetPasses()
        {
            GetTimeObjectsRequest request = new() { Offset = null };

            Assert.That(FlowCatalogPaging.TryValidate(request, FlowCatalogPaging.kMaxObjectLimit, out _), Is.True);
        }

        /// <summary>
        /// Review finding F3: an explicit null limit, as generated clients send it, selects the default page size
        /// instead of failing deserialization.
        /// </summary>
        [Test]
        public void NullLimitSelectsTheMaximumPageSize()
        {
            GetAddressObjectsRequest request = JsonSerializer.Deserialize<GetAddressObjectsRequest>("{\"limit\": null, \"offset\": null}")!;

            Assert.Multiple(() =>
            {
                Assert.That(request.Limit, Is.Null);
                Assert.That(FlowCatalogPaging.TryValidate(request, FlowCatalogPaging.kMaxObjectLimit, out _), Is.True);
                Assert.That(FlowCatalogPaging.GetPageSize(request, FlowCatalogPaging.kMaxObjectLimit), Is.EqualTo(FlowCatalogPaging.kMaxObjectLimit));
                Assert.That(FlowCatalogPaging.GetPageSize(new GetServiceGroupsRequest { Limit = 20 }, FlowCatalogPaging.kMaxGroupLimit), Is.EqualTo(20));
            });
        }

        [Test]
        public void InvalidLimitAndOffsetAreReportedTogether()
        {
            GetServiceGroupsRequest request = new() { Limit = 0, Offset = -1 };

            bool isValid = FlowCatalogPaging.TryValidate(request, FlowCatalogPaging.kMaxGroupLimit, out ActionResult? errorResult);

            string message = ((BadRequestObjectResult)errorResult!).Value!.ToString()!;
            Assert.Multiple(() =>
            {
                Assert.That(isValid, Is.False);
                Assert.That(message, Does.Contain("'limit'"));
                Assert.That(message, Does.Contain("'offset'"));
                Assert.That(message, Does.Contain(FlowCatalogPaging.kMaxGroupLimit.ToString()));
            });
        }

        [Test]
        public void LimitAboveTheMaximumIsRejected()
        {
            GetAddressObjectsRequest request = new() { Limit = FlowCatalogPaging.kMaxObjectLimit + 1 };

            Assert.That(FlowCatalogPaging.TryValidate(request, FlowCatalogPaging.kMaxObjectLimit, out _), Is.False);
        }

        [Test]
        public void PagedRootSchemaListsThePagingKeys()
        {
            RequestRootValidationSchema schema = RequestRootValidationSchema.ForPagedVisibleInRequest("getAddressObjects", FlowCatalogPaging.kMaxObjectLimit);

            Assert.That(schema.AllowedKeys.Select(key => key.JsonName), Is.EquivalentTo(kPagedRootKeys));
        }
    }
}
