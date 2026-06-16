using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Pages.Reporting.Reports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace FWO.Test
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiOwnerRecertTableTest : BunitContext
    {
        [Test]
        public void OwnerRecertTable_RendersMainResponsibleColumn()
        {
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            Services.AddScoped(_ => JSInterop.JSRuntime);
            Services.AddLocalization();

            List<FwoOwner> owners =
            [
                new()
                {
                    Id = 1,
                    Name = "Overdue Owner",
                    ExtAppId = "EXT-OVERDUE",
                    NextRecertDate = DateTime.Today.AddDays(-1),
                    LastRecertified = DateTime.Today.AddDays(-30),
                    LastRecertifierDn = "cn=recert.user,ou=users,dc=test,dc=local",
                    OwnerResponsibles =
                    [
                        new()
                        {
                            Dn = "cn=main.user,ou=users,dc=test,dc=local",
                            ResponsibleTypeId = GlobalConst.kOwnerResponsibleTypeMain
                        },
                        new()
                        {
                            Dn = "cn=second.user,ou=users,dc=test,dc=local",
                            ResponsibleTypeId = GlobalConst.kOwnerResponsibleTypeMain
                        }
                    ]
                }
            ];

            IRenderedComponent<OwnerRecertTable> cut = Render<OwnerRecertTable>(parameters => parameters
                .Add(p => p.Owners, owners));

            Assert.That(cut.Markup, Does.Contain("Main responsible person (DN)"));
            Assert.That(cut.Markup, Does.Contain("main.user, second.user"));
        }

        [Test]
        public void OwnerRecertTable_UsesCreationDateHintWhenOwnerSummaryDatesAreMissing()
        {
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            Services.AddScoped(_ => JSInterop.JSRuntime);
            Services.AddLocalization();

            List<FwoOwner> owners =
            [
                new()
                {
                    Id = 1,
                    Name = "Fallback Owner",
                    ExtAppId = "EXT-FALLBACK",
                    RecertInterval = 14,
                    ChangelogOwners =
                    [
                        new()
                        {
                            ChangeAction = ChangelogActionType.INSERT,
                            ChangeImport = new()
                            {
                                Time = new DateTime(2026, 2, 1)
                            }
                        }
                    ]
                }
            ];

            IRenderedComponent<OwnerRecertTable> cut = Render<OwnerRecertTable>(parameters => parameters
                .Add(p => p.Owners, owners));

            Assert.That(cut.Markup, Does.Contain("15.02.2026"));
            Assert.That(cut.Markup, Does.Contain("01.02.2026 (Created)"));
        }
    }
}
