using FWO.Api.Client;
using NSubstitute;

namespace FWO.Test.Mocks
{
    public class MockApiConnection : Mock<ApiConnection>
    {
        public List<(string Query, object Variables)> SentQueries { get; } = new();

        protected override void Configure(ApiConnection sub)
        {
            // Log sent queries and variables

            sub.When(x => x.SendQueryAsync<dynamic>(Arg.Any<string>(), Arg.Any<object>()))
            .Do(ci =>
            {
                var query = ci.ArgAt<string>(0);
                var vars = ci.ArgAt<object>(1);
                SentQueries.Add((query, vars));
            });
        }
    }    
}
