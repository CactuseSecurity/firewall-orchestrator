using FWO.Basics;
using FWO.Data.Middleware;
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
        private static readonly List<int> kFiveItems = [1, 2, 3, 4, 5];
        private static readonly List<int> kSecondPageOfTwo = [3, 4];
        private const int kMaxLimit = GlobalConst.kDefaultMaxRestResponseObjectLimit;
        private const int kSmallMaxLimit = 20;
        private static readonly List<string> kLimitAndOffsetKeys = ["limit", "offset"];

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
        public void CatalogRequestsDefaultToTheConfiguredMaximumPage()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new GetAddressObjectsRequest().Limit, Is.Null);
                Assert.That(new GetServiceObjectsRequest().Limit, Is.Null);
                Assert.That(new GetTimeObjectsRequest().Limit, Is.Null);
                Assert.That(new GetAddressGroupsRequest().Limit, Is.Null);
                Assert.That(new GetServiceGroupsRequest().Limit, Is.Null);
                Assert.That(new GetAddressObjectsRequest().Offset, Is.Null);
                Assert.That(FlowCatalogPaging.GetPageSize(new GetAddressGroupsRequest(), kSmallMaxLimit), Is.EqualTo(kSmallMaxLimit));
            });
        }

        [Test]
        public void ValidPagingPasses()
        {
            GetAddressObjectsRequest request = new() { Limit = kMaxLimit, Offset = 0 };

            Assert.Multiple(() =>
            {
                Assert.That(FlowCatalogPaging.TryValidate(request, kMaxLimit, out ActionResult? errorResult), Is.True);
                Assert.That(errorResult, Is.Null);
            });
        }

        [Test]
        public void OmittedOffsetPasses()
        {
            GetTimeObjectsRequest request = new() { Offset = null };

            Assert.That(FlowCatalogPaging.TryValidate(request, kMaxLimit, out _), Is.True);
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
                Assert.That(FlowCatalogPaging.TryValidate(request, kMaxLimit, out _), Is.True);
                Assert.That(FlowCatalogPaging.GetPageSize(request, kMaxLimit), Is.EqualTo(kMaxLimit));
                Assert.That(FlowCatalogPaging.GetPageSize(new GetServiceGroupsRequest { Limit = 20 }, kSmallMaxLimit), Is.EqualTo(20));
            });
        }

        [Test]
        public void InvalidLimitAndOffsetAreReportedTogether()
        {
            GetServiceGroupsRequest request = new() { Limit = 0, Offset = -1 };

            bool isValid = FlowCatalogPaging.TryValidate(request, kSmallMaxLimit, out ActionResult? errorResult);

            string message = ((BadRequestObjectResult)errorResult!).Value!.ToString()!;
            Assert.Multiple(() =>
            {
                Assert.That(isValid, Is.False);
                Assert.That(message, Does.Contain("'limit'"));
                Assert.That(message, Does.Contain("'offset'"));
                Assert.That(message, Does.Contain(kSmallMaxLimit.ToString()));
            });
        }

        [Test]
        public void LimitAboveTheMaximumIsRejected()
        {
            GetAddressObjectsRequest request = new() { Limit = kMaxLimit + 1 };

            Assert.That(FlowCatalogPaging.TryValidate(request, kMaxLimit, out _), Is.False);
        }

        [Test]
        public void PagedRootSchemaListsThePagingKeys()
        {
            RequestRootValidationSchema schema = RequestRootValidationSchema.ForPagedVisibleInRequest("getAddressObjects");

            Assert.That(schema.AllowedKeys.Select(key => key.JsonName), Is.EquivalentTo(kPagedRootKeys));
        }

        [Test]
        public void FromOrderedItemsCutsThePageAtTheOffset()
        {
            ListPage<int> page = ListPage<int>.FromOrderedItems(kFiveItems, 2, 2);

            Assert.Multiple(() =>
            {
                Assert.That(page.Items, Is.EqualTo(kSecondPageOfTwo));
                Assert.That(page.HasMore, Is.True);
            });
        }

        [Test]
        public void FromOrderedItemsReportsTheLastPage()
        {
            ListPage<int> lastPage = ListPage<int>.FromOrderedItems(kFiveItems, 2, 4);
            ListPage<int> behindTheEnd = ListPage<int>.FromOrderedItems(kFiveItems, 2, 10);
            ListPage<int> withoutOffset = ListPage<int>.FromOrderedItems(kThreeItems, 3, null);

            Assert.Multiple(() =>
            {
                Assert.That(lastPage.Items, Has.Count.EqualTo(1));
                Assert.That(lastPage.HasMore, Is.False);
                Assert.That(behindTheEnd.Items, Is.Empty);
                Assert.That(behindTheEnd.HasMore, Is.False);
                Assert.That(withoutOffset.Items, Is.EqualTo(kThreeItems));
                Assert.That(withoutOffset.HasMore, Is.False);
            });
        }

        [TestCase(500, 500)]
        [TestCase(GlobalConst.kMinRestResponseObjectLimit, GlobalConst.kMinRestResponseObjectLimit)]
        [TestCase(1, GlobalConst.kMinRestResponseObjectLimit)]
        [TestCase(GlobalConst.kMinRestResponseObjectLimit - 1, GlobalConst.kMinRestResponseObjectLimit)]
        [TestCase(0, GlobalConst.kDefaultMaxRestResponseObjectLimit)]
        [TestCase(-5, GlobalConst.kDefaultMaxRestResponseObjectLimit)]
        [TestCase(GlobalConst.kMaxRestResponseObjectLimitCeiling, GlobalConst.kMaxRestResponseObjectLimitCeiling)]
        [TestCase(GlobalConst.kMaxRestResponseObjectLimitCeiling + 1, GlobalConst.kMaxRestResponseObjectLimitCeiling)]
        public void MaxObjectLimitIsTakenFromTheConfiguration(int configuredLimit, int expectedLimit)
        {
            SimulatedGlobalConfig globalConfig = new() { MaxRestResponseObjectLimit = configuredLimit };

            Assert.That(ListPaging.GetMaxObjectLimit(globalConfig), Is.EqualTo(expectedLimit));
        }

        [Test]
        public void MaxObjectLimitDefaultsToTenThousand()
        {
            Assert.That(ListPaging.GetMaxObjectLimit(new SimulatedGlobalConfig()), Is.EqualTo(10000));
        }

        [TestCase(null, null, true, true)]
        [TestCase(1, 0, true, true)]
        [TestCase(kSmallMaxLimit, 3, true, true)]
        [TestCase(0, 0, false, true)]
        [TestCase(kSmallMaxLimit + 1, 0, false, true)]
        [TestCase(1, -1, true, false)]
        public void ValidatePagingReportsEveryInvalidKeyAtItsPath(int? limit, int? offset, bool limitValid, bool offsetValid)
        {
            Dictionary<string, string[]> errors = ListPaging.ValidatePaging(limit, offset, kSmallMaxLimit, "options.limit", "options.offset");

            Assert.Multiple(() =>
            {
                Assert.That(errors.ContainsKey("options.limit"), Is.EqualTo(!limitValid));
                Assert.That(errors.ContainsKey("options.offset"), Is.EqualTo(!offsetValid));
                if (!limitValid)
                {
                    Assert.That(errors["options.limit"][0], Does.Contain(kSmallMaxLimit.ToString()));
                }
            });
        }

        [Test]
        public void TryValidateReturnsAValidationProblemForInvalidPaging()
        {
            PagingParameters paging = new() { Limit = kSmallMaxLimit + 1, Offset = -1 };

            bool isValid = ListPaging.TryValidate(paging, kSmallMaxLimit, out ActionResult? errorResult);

            ValidationProblemDetails problem = (ValidationProblemDetails)((BadRequestObjectResult)errorResult!).Value!;
            Assert.Multiple(() =>
            {
                Assert.That(isValid, Is.False);
                Assert.That(problem.Status, Is.EqualTo(StatusCodes.Status400BadRequest));
                Assert.That(problem.Errors.Keys, Is.EquivalentTo(kLimitAndOffsetKeys));
            });
        }

        [Test]
        public void TryValidateAcceptsOmittedPaging()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ListPaging.TryValidate(new PagingParameters(), kSmallMaxLimit, out ActionResult? errorResult), Is.True);
                Assert.That(errorResult, Is.Null);
            });
        }

        [TestCase(null, 5, "false")]
        [TestCase(2, 2, "true")]
        [TestCase(5, 5, "false")]
        public void TakePageCutsThePageAndSetsTheHeader(int? limit, int expectedCount, string expectedHeader)
        {
            DefaultHttpContext context = new();

            List<int> page = ListPaging.TakePage(context, kFiveItems, new PagingParameters { Limit = limit }, kSmallMaxLimit);

            Assert.Multiple(() =>
            {
                Assert.That(page, Has.Count.EqualTo(expectedCount));
                Assert.That(context.Response.Headers[ListPaging.kHasMoreHeader].ToString(), Is.EqualTo(expectedHeader));
            });
        }

        [Test]
        public void LimitDescriptionNamesTheSettingAndTheHeader()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ListPaging.kLimitDescription, Does.Contain(ListPaging.kMaxObjectLimitSetting));
                Assert.That(ListPaging.kLimitDescription, Does.Contain(ListPaging.kHasMoreHeader));
                Assert.That(ListPaging.kLimitDescription, Does.Contain(GlobalConst.kDefaultMaxRestResponseObjectLimit.ToString()));
            });
        }
    }
}
