using Bunit;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Pages.Reporting;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiRuleDetailsTest
    {
        private const string kOlderRecertifierDn = "uid=bob,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal";
        private const string kLatestRecertifierDn = "uid=alice,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal";

        private readonly SimulatedUserConfig userConfig = new();

        private BunitContext CreateContext()
        {
            BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private static IRenderedComponent<RuleDetails> Render(BunitContext context, Rule rule, bool extended = false)
        {
            return context.Render<RuleDetails>(parameters => parameters
                .Add(p => p.Rule, rule)
                .Add(p => p.Extended, extended));
        }

        /// <summary>
        /// The details as title and value pairs, in the order they are shown.
        /// </summary>
        private static List<(string Title, string Data)> Details(IRenderedComponent<RuleDetails> component)
        {
            return [.. component.FindAll("b").Select(title => (title.TextContent, title.NextSibling?.TextContent.Trim() ?? ""))];
        }

        private string DetailValue(IRenderedComponent<RuleDetails> component, string titleKey)
        {
            return Details(component).Single(detail => detail.Title == userConfig.GetText(titleKey)).Data;
        }

        private static Recertification BuildRecertification(DateTime date, bool recertified, string userDn, string comment)
        {
            return new Recertification
            {
                RecertDate = date,
                Recertified = recertified,
                UserDn = userDn,
                Comment = comment,
                FwoOwner = new FwoOwner { Name = "App owner" }
            };
        }

        [Test]
        public async Task ARuleWithoutHistoryShowsNoDetails()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<RuleDetails> component = Render(context, new Rule());

            Assert.That(Details(component), Is.Empty);
        }

        [Test]
        public async Task TheExtendedViewAddsTheRuleProperties()
        {
            await using BunitContext context = CreateContext();
            Rule rule = new() { Name = "web access", Uid = "uid-1", Action = "accept", Track = "log", Comment = "for the web", Disabled = false };

            IRenderedComponent<RuleDetails> component = Render(context, rule, extended: true);

            Assert.That(DetailValue(component, "name"), Is.EqualTo("web access"));
            Assert.That(DetailValue(component, "uid"), Is.EqualTo("uid-1"));
            Assert.That(DetailValue(component, "action"), Is.EqualTo("accept"));
            Assert.That(DetailValue(component, "track"), Is.EqualTo("log"));
            Assert.That(DetailValue(component, "comment"), Is.EqualTo("for the web"));
            Assert.That(DetailValue(component, "enabled"), Is.EqualTo(true.ToString()));
        }

        [Test]
        public async Task CreationAndHitsAreShown()
        {
            await using BunitContext context = CreateContext();
            DateTime created = new(2026, 1, 2, 3, 4, 5);
            DateTime firstHit = new(2026, 2, 3, 4, 5, 6);
            Rule rule = new();
            rule.Metadata.CreatedImport = new ImportControl { StartTime = created };
            rule.Metadata.FirstHit = firstHit;

            IRenderedComponent<RuleDetails> component = Render(context, rule);

            Assert.That(DetailValue(component, "created"), Is.EqualTo(created.ToString()));
            Assert.That(DetailValue(component, "first_hit"), Is.EqualTo(firstHit.ToString()));
            Assert.That(Details(component).Select(detail => detail.Title), Has.None.EqualTo(userConfig.GetText("last_hit_per_gateway")));
        }

        [Test]
        public async Task TheLastHitIsListedPerGatewaySortedByName()
        {
            await using BunitContext context = CreateContext();
            DateTime zuluHit = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime alphaHit = new(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);
            DateTime unnamedHit = new(2026, 3, 3, 0, 0, 0, DateTimeKind.Utc);
            Rule rule = new();
            rule.Metadata.RuleHits =
            [
                new RuleHit { DeviceId = 7, LastHit = zuluHit, Gateway = new RuleHitGateway { Name = "zulu" } },
                new RuleHit { DeviceId = 9, LastHit = unnamedHit },
                new RuleHit { DeviceId = 8, LastHit = alphaHit, Gateway = new RuleHitGateway { Name = "Alpha" } }
            ];

            IRenderedComponent<RuleDetails> component = Render(context, rule);

            List<(string Title, string Data)> details = Details(component);
            int perGateway = details.FindIndex(detail => detail.Title == userConfig.GetText("last_hit_per_gateway"));
            // a gateway without name is shown by its id and sorts first
            Assert.That(details.Skip(perGateway + 1).Take(3).ToList(), Is.EqualTo(new List<(string, string)>
            {
                ("9", unnamedHit.ToString()),
                ("Alpha", alphaHit.ToString()),
                ("zulu", zuluHit.ToString())
            }));
        }

        [Test]
        public async Task TheRecertificationHistoryNamesTheLatestRecertifierAndEveryDecision()
        {
            await using BunitContext context = CreateContext();
            DateTime older = new(2026, 1, 1, 0, 0, 0);
            DateTime latest = new(2026, 6, 1, 0, 0, 0);
            Rule rule = new();
            rule.Metadata.RecertHistory =
            [
                BuildRecertification(older, recertified: true, kOlderRecertifierDn, "still needed"),
                BuildRecertification(latest, recertified: false, kLatestRecertifierDn, "obsolete")
            ];

            IRenderedComponent<RuleDetails> component = Render(context, rule);

            string latestUser = new DistName(kLatestRecertifierDn).UserName;
            string olderUser = new DistName(kOlderRecertifierDn).UserName;
            Assert.That(DetailValue(component, "last_recertifier"), Is.EqualTo(latestUser));
            Assert.That(DetailValue(component, "last_certify_date"), Is.EqualTo(older.ToString()));
            Assert.That(DetailValue(component, "decert_date"), Is.EqualTo(latest.ToString()));
            Assert.That(DetailValue(component, "recert_comment"), Is.EqualTo("obsolete"));
            Assert.That(DetailValue(component, "recert_history"), Is.EqualTo(":"));
            Assert.That(DetailValue(component, older.ToString()), Is.EqualTo(
                $"{userConfig.GetText("recertified_by")} {olderUser} {userConfig.GetText("as_owner")} App owner {userConfig.GetText("comment")}: still needed"));
            Assert.That(DetailValue(component, latest.ToString()), Does.StartWith($"{userConfig.GetText("decertified_by")} {latestUser} "));
        }

        [Test]
        public async Task ARecertificationWithoutUserShowsNoRecertifier()
        {
            await using BunitContext context = CreateContext();
            Rule rule = new();
            Recertification recertification = BuildRecertification(new DateTime(2026, 1, 1), recertified: true, "", "");
            recertification.UserDn = null!;
            rule.Metadata.RecertHistory = [recertification];

            IRenderedComponent<RuleDetails> component = Render(context, rule);

            Assert.That(Details(component).Select(detail => detail.Title), Has.None.EqualTo(userConfig.GetText("last_recertifier")));
        }

        [Test]
        public async Task ARuleWithoutMetadataShowsOnlyItsProperties()
        {
            await using BunitContext context = CreateContext();
            Rule rule = new() { Name = "web access", Metadata = null! };

            IRenderedComponent<RuleDetails> component = Render(context, rule, extended: true);

            Assert.That(Details(component).Select(detail => detail.Title), Does.Contain(userConfig.GetText("name")));
            Assert.That(Details(component).Select(detail => detail.Title), Has.None.EqualTo(userConfig.GetText("created")));
        }
    }
}
