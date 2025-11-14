using FWO.Basics.Interfaces;
using NSubstitute;

namespace FWO.Logging
{
    public class MockLogger : Mock<ILogger>
    {
        public Dictionary<DateTime, string> Logmessages = new();
        protected override void Configure(ILogger sub)
        {
                        // Info
            sub.When(x => x.TryWriteInfo(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()))
               .Do(ci =>
               {
                    Logmessages[DateTime.Now] = $"{ci.ArgAt<string>(0)} - {ci.ArgAt<string>(1)}";
               });

        }
    }
}


///
/// 
///         public List<(string Query, object Variables)> SentQueries { get; } = new();

        // protected override void Configure(ApiConnection sub)
        // {
        //     // Log sent queries and variables

        //     sub.When(x => x.SendQueryAsync<dynamic>(Arg.Any<string>(), Arg.Any<object>()))
        //     .Do(ci =>
        //     {
        //         var query = ci.ArgAt<string>(0);
        //         var vars = ci.ArgAt<object>(1);
        //         SentQueries.Add((query, vars));
        //     });
        // }