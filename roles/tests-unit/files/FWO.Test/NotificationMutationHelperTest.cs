using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class NotificationMutationHelperTest
    {
        [Test]
        public async Task AddAsync_PersistsNotificationAndReturnsAssignedId()
        {
            RecordingNotificationApiConnection apiConnection = new();
            FwoNotification notification = NewNotification();

            FwoNotification persistedNotification = await NotificationMutationHelper.AddAsync(apiConnection, NotificationClient.Report, notification);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.AddCalls, Has.Count.EqualTo(1));
                Assert.That(persistedNotification.Id, Is.EqualTo(100));
                Assert.That(persistedNotification.NotificationClient, Is.EqualTo(NotificationClient.Report));
                Assert.That(persistedNotification.Name, Is.EqualTo(notification.Name));
                Assert.That(persistedNotification.EmailAddressTo, Is.EqualTo(notification.EmailAddressTo));
                Assert.That(apiConnection.AddCalls[0].Client, Is.EqualTo(NotificationClient.Report.ToString()));
                Assert.That(apiConnection.AddCalls[0].Name, Is.EqualTo(notification.Name));
                Assert.That(apiConnection.AddCalls[0].BundleId, Is.EqualTo(notification.BundleId));
            });
        }

        [Test]
        public async Task UpdateAsync_PersistsNotificationAndReturnsUpdatedCopy()
        {
            RecordingNotificationApiConnection apiConnection = new();
            FwoNotification notification = NewNotification();
            notification.Id = 17;
            notification.EmailSubject = "Updated Subject";

            FwoNotification persistedNotification = await NotificationMutationHelper.UpdateAsync(apiConnection, NotificationClient.Report, notification);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpdateCalls, Has.Count.EqualTo(1));
                Assert.That(persistedNotification.Id, Is.EqualTo(17));
                Assert.That(persistedNotification.EmailSubject, Is.EqualTo("Updated Subject"));
                Assert.That(apiConnection.UpdateCalls[0].Id, Is.EqualTo(17));
                Assert.That(apiConnection.UpdateCalls[0].Subject, Is.EqualTo("Updated Subject"));
                Assert.That(apiConnection.UpdateCalls[0].Layout, Is.EqualTo(notification.Layout.ToString()));
            });
        }

        [Test]
        public async Task DeleteAsync_DeletesNotificationById()
        {
            RecordingNotificationApiConnection apiConnection = new();

            await NotificationMutationHelper.DeleteAsync(apiConnection, 23);

            Assert.That(apiConnection.DeletedIds, Is.EqualTo(new[] { 23 }));
        }

        private static FwoNotification NewNotification()
        {
            return new FwoNotification
            {
                NotificationClient = NotificationClient.Report,
                Channel = NotificationChannel.Email,
                Name = "Weekly PDF",
                RecipientTo = EmailRecipientOption.OtherAddresses,
                EmailAddressTo = "report@example.org",
                RecipientCc = EmailRecipientOption.None,
                EmailAddressCc = "",
                EmailSubject = "Subject",
                EmailBody = "Body",
                ScheduleId = 11,
                BundleType = BundleType.Attachments,
                BundleId = "bundle-1",
                Layout = NotificationLayout.PdfAsAttachment,
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
            internal string Client { get; init; } = "";
            internal string Name { get; init; } = "";
            internal string Subject { get; init; } = "";
            internal string Layout { get; init; } = "";
            internal string? BundleId { get; init; }

            internal static NotificationMutationCall From(object? variables)
            {
                return new NotificationMutationCall
                {
                    Id = ReadInt(variables, "id"),
                    Client = ReadString(variables, "client"),
                    Name = ReadString(variables, "name"),
                    Subject = ReadString(variables, "subject"),
                    Layout = ReadString(variables, "layout"),
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
