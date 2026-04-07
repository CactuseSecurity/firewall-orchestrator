using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Ui.Services;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ReportScheduleNotificationHelperTest
    {
        [Test]
        public async Task CreateNotifications_MultipleFormats_AssignsSharedAttachmentBundle()
        {
            RecordingNotificationApiConnection apiConnection = new();
            ReportSchedule reportSchedule = NewReportSchedule(GlobalConst.kHtml, GlobalConst.kPdf);

            List<FwoNotification> createdNotifications = await ReportScheduleNotificationHelper.CreateNotifications(apiConnection,
                reportSchedule, "report@example.org", "Subject", "Body");

            ClassicAssert.AreEqual(2, createdNotifications.Count);
            ClassicAssert.AreEqual(2, apiConnection.AddCalls.Count);
            ClassicAssert.IsTrue(createdNotifications.All(notification => notification.BundleType == BundleType.Attachments));
            ClassicAssert.IsTrue(createdNotifications.All(notification => !string.IsNullOrWhiteSpace(notification.BundleId)));
            ClassicAssert.AreEqual(createdNotifications[0].BundleId, createdNotifications[1].BundleId);
            ClassicAssert.AreEqual(NotificationLayout.HtmlAsAttachment, createdNotifications[0].Layout);
            ClassicAssert.AreEqual(NotificationLayout.PdfAsAttachment, createdNotifications[1].Layout);
            ClassicAssert.AreEqual(createdNotifications[0].BundleId, apiConnection.AddCalls[0].BundleId);
            ClassicAssert.AreEqual(createdNotifications[0].BundleId, apiConnection.AddCalls[1].BundleId);
        }

        [Test]
        public async Task SyncNotifications_ToEmailFalse_DeletesExistingNotifications()
        {
            RecordingNotificationApiConnection apiConnection = new();
            ReportSchedule reportSchedule = NewReportSchedule(GlobalConst.kHtml);
            reportSchedule.Notifications =
            [
                NewNotification(11, NotificationLayout.HtmlAsAttachment),
                NewNotification(12, NotificationLayout.PdfAsAttachment)
            ];

            List<FwoNotification> syncedNotifications = await ReportScheduleNotificationHelper.SyncNotifications(apiConnection,
                reportSchedule, false, "report@example.org", "Subject", "Body");

            ClassicAssert.AreEqual(0, syncedNotifications.Count);
            CollectionAssert.AreEquivalent(new[] { 11, 12 }, apiConnection.DeletedIds);
            ClassicAssert.AreEqual(0, apiConnection.AddCalls.Count);
            ClassicAssert.AreEqual(0, apiConnection.UpdateCalls.Count);
        }

        [Test]
        public async Task SyncNotifications_ReusesBundleIdUpdatesExistingAndDeletesRemovedFormats()
        {
            RecordingNotificationApiConnection apiConnection = new();
            ReportSchedule reportSchedule = NewReportSchedule(GlobalConst.kHtml, GlobalConst.kCsv);
            reportSchedule.Notifications =
            [
                NewNotification(21, NotificationLayout.HtmlAsAttachment, "bundle-1"),
                NewNotification(22, NotificationLayout.PdfAsAttachment, "bundle-1")
            ];

            List<FwoNotification> syncedNotifications = await ReportScheduleNotificationHelper.SyncNotifications(apiConnection,
                reportSchedule, true, "report@example.org", "Updated Subject", "Updated Body");

            ClassicAssert.AreEqual(2, syncedNotifications.Count);
            ClassicAssert.AreEqual(1, apiConnection.UpdateCalls.Count);
            ClassicAssert.AreEqual(1, apiConnection.AddCalls.Count);
            CollectionAssert.AreEquivalent(new[] { 22 }, apiConnection.DeletedIds);
            ClassicAssert.AreEqual(21, apiConnection.UpdateCalls[0].Id);
            ClassicAssert.AreEqual("bundle-1", apiConnection.UpdateCalls[0].BundleId);
            ClassicAssert.AreEqual(BundleType.Attachments.ToString(), apiConnection.UpdateCalls[0].BundleType);
            ClassicAssert.AreEqual(NotificationLayout.CsvAsAttachment, syncedNotifications.Single(notification => notification.Id != 21).Layout);
            ClassicAssert.IsTrue(syncedNotifications.All(notification => notification.BundleType == BundleType.Attachments));
            ClassicAssert.IsTrue(syncedNotifications.All(notification => notification.BundleId == "bundle-1"));
        }

        private static ReportSchedule NewReportSchedule(params string[] formats)
        {
            return new ReportSchedule
            {
                Id = 7,
                Name = "Weekly Report",
                OutputFormat = formats.Select(format => new FileFormat { Name = format }).ToList()
            };
        }

        private static FwoNotification NewNotification(int id, NotificationLayout layout, string? bundleId = null)
        {
            return new FwoNotification
            {
                Id = id,
                Layout = layout,
                BundleType = bundleId == null ? null : BundleType.Attachments,
                BundleId = bundleId,
                NotificationClient = NotificationClient.Report,
                Channel = NotificationChannel.Email,
                RecipientTo = EmailRecipientOption.OtherAddresses,
                EmailAddressTo = "existing@example.org",
                RecipientCc = EmailRecipientOption.None,
                EmailAddressCc = "",
                EmailSubject = "Old Subject",
                EmailBody = "Old Body",
                Deadline = NotificationDeadline.None
            };
        }

        private sealed class RecordingNotificationApiConnection : SimulatedApiConnection
        {
            private int nextId = 100;

            internal List<NotificationMutationCall> AddCalls { get; } = [];
            internal List<NotificationMutationCall> UpdateCalls { get; } = [];
            internal List<int> DeletedIds { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                if (query == NotificationQueries.addNotification && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    AddCalls.Add(NotificationMutationCall.From(variables));
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewId = nextId++ }]
                    });
                }

                if (query == NotificationQueries.updateNotification && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    UpdateCalls.Add(NotificationMutationCall.From(variables));
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { UpdatedId = ReadInt(variables, "id") }]
                    });
                }

                if (query == NotificationQueries.deleteNotification && typeof(QueryResponseType) == typeof(object))
                {
                    DeletedIds.Add(ReadInt(variables, "id"));
                    return Task.FromResult((QueryResponseType)(object)new object());
                }

                throw new NotImplementedException();
            }

            private static int ReadInt(object? source, string propertyName)
            {
                object? value = ReadValue(source, propertyName);
                return value is int intValue ? intValue : 0;
            }

            private static object? ReadValue(object? source, string propertyName)
            {
                return source?.GetType().GetProperty(propertyName)?.GetValue(source);
            }
        }

        private sealed class NotificationMutationCall
        {
            internal int Id { get; init; }
            internal string Name { get; init; } = "";
            internal string EmailAddressTo { get; init; } = "";
            internal string Subject { get; init; } = "";
            internal string EmailBody { get; init; } = "";
            internal string Layout { get; init; } = "";
            internal string? BundleType { get; init; }
            internal string? BundleId { get; init; }

            internal static NotificationMutationCall From(object? variables)
            {
                return new NotificationMutationCall
                {
                    Id = ReadInt(variables, "id"),
                    Name = ReadString(variables, "name"),
                    EmailAddressTo = ReadString(variables, "emailAddressTo"),
                    Subject = ReadString(variables, "subject"),
                    EmailBody = ReadString(variables, "emailBody"),
                    Layout = ReadString(variables, "layout"),
                    BundleType = ReadNullableString(variables, "bundleType"),
                    BundleId = ReadNullableString(variables, "bundleId")
                };
            }

            private static int ReadInt(object? source, string propertyName)
            {
                object? value = ReadValue(source, propertyName);
                return value is int intValue ? intValue : 0;
            }

            private static string ReadString(object? source, string propertyName)
            {
                return ReadNullableString(source, propertyName) ?? "";
            }

            private static string? ReadNullableString(object? source, string propertyName)
            {
                object? value = ReadValue(source, propertyName);
                return value?.ToString();
            }

            private static object? ReadValue(object? source, string propertyName)
            {
                return source?.GetType().GetProperty(propertyName)?.GetValue(source);
            }
        }
    }
}
