using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class NotificationLastSentHelperTest
    {
        [Test]
        public async Task UpdateAsync_SkipsEmptyAndInvalidIds()
        {
            RecordingApiConnection apiConnection = new();

            int affectedRows = await NotificationLastSentHelper.UpdateAsync(apiConnection, new List<int> { 0, -1 });

            Assert.Multiple(() =>
            {
                Assert.That(affectedRows, Is.Zero);
                Assert.That(apiConnection.UpdatedIds, Is.Empty);
            });
        }

        [Test]
        public async Task UpdateAsync_DeduplicatesPositiveIds()
        {
            RecordingApiConnection apiConnection = new() { AffectedRows = 2 };

            int affectedRows = await NotificationLastSentHelper.UpdateAsync(apiConnection, new List<int> { 7, 0, 7, -1, 9 });

            Assert.Multiple(() =>
            {
                Assert.That(affectedRows, Is.EqualTo(2));
                Assert.That(apiConnection.UpdatedIds, Is.EqualTo(new List<int> { 7, 9 }));
            });
        }

        [Test]
        public async Task UpdateAsync_ReturnsAffectedRowsForPartialUpdate()
        {
            RecordingApiConnection apiConnection = new() { AffectedRows = 1 };

            int affectedRows = await NotificationLastSentHelper.UpdateAsync(apiConnection, new List<int> { 7, 9 });

            Assert.That(affectedRows, Is.EqualTo(1));
        }

        [Test]
        public async Task UpdateAsync_ReturnsZeroWhenMutationFails()
        {
            RecordingApiConnection apiConnection = new() { ThrowOnUpdate = true };

            int affectedRows = await NotificationLastSentHelper.UpdateAsync(apiConnection, new List<int> { 7 });

            Assert.That(affectedRows, Is.Zero);
        }

        private sealed class RecordingApiConnection : SimulatedApiConnection
        {
            public List<int> UpdatedIds { get; private set; } = [];
            public int AffectedRows { get; init; }
            public bool ThrowOnUpdate { get; init; }

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null,
                QueryChunkingOptions? chunkingOptions = null)
            {
                Assert.That(query, Is.EqualTo(NotificationQueries.updateNotificationsLastSent));
                UpdatedIds = GetIds(variables);
                if (ThrowOnUpdate)
                {
                    throw new InvalidOperationException("update failed");
                }

                return Task.FromResult((T)(object)new ReturnId { AffectedRows = AffectedRows });
            }

            private static List<int> GetIds(object? variables)
            {
                object? value = variables?.GetType().GetProperties().FirstOrDefault(property => property.Name == "ids")?.GetValue(variables);
                return value is IEnumerable<int> ids ? [.. ids] : [];
            }
        }
    }
}
