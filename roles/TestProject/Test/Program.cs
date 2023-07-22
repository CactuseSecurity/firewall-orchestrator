using FWO.Middleware.Client;
using FWO.Middleware.RequestParameters;
using GraphQL;
using GraphQL.Client;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Newtonsoft.Json;
using System.Text.Json;

// Allow all certificates | TODO: REMOVE IF SERVER GOT VALID CERTIFICATE
HttpClientHandler Handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
};

GraphQLHttpClient graphQlClient = new GraphQLHttpClient(new GraphQLHttpClientOptions()
{
    EndPoint = new Uri("https://localhost:9443/api/v1/graphql"),
    HttpMessageHandler = Handler,
    UseWebSocketForQueriesAndMutations = false, // TODO: Use websockets for performance reasons          
    ConfigureWebsocketOptions = webSocketOptions => webSocketOptions.RemoteCertificateValidationCallback += (message, cert, chain, errors) => true
}, new NewtonsoftJsonSerializer());

// 1 hour timeout
//MiddlewareClient mwClient = new MiddlewareClient("https://localhost:8880/");
//var jwt = await mwClient.AuthenticateUser(new AuthenticationTokenGetParameters
//{
//    Username = "user1_demo",
//    Password = "cactus1"
//});

string jwt = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6InVzZXIxX2RlbW8iLCJ4LWhhc3VyYS11c2VyLWlkIjoiMzciLCJ4LWhhc3VyYS11dWlkIjoidWlkPXVzZXIxX2RlbW8sb3U9dGVuYW50MV9kZW1vLG91PW9wZXJhdG9yLG91PXVzZXIsZGM9ZndvcmNoLGRjPWludGVybmFsIiwieC1oYXN1cmEtdGVuYW50LWlkIjoiMiIsIngtaGFzdXJhLXZpc2libGUtbWFuYWdlbWVudHMiOiJ7IDEgfSIsIngtaGFzdXJhLXZpc2libGUtZGV2aWNlcyI6InsgMSB9Iiwicm9sZSI6WyJyZXBvcnRlciIsInJlY2VydGlmaWVyIl0sIngtaGFzdXJhLWFsbG93ZWQtcm9sZXMiOlsicmVwb3J0ZXIiLCJyZWNlcnRpZmllciJdLCJ4LWhhc3VyYS1kZWZhdWx0LXJvbGUiOiJyZXBvcnRlciIsIm5iZiI6MTY4OTk2NDE3NywiZXhwIjoxNjkwMDA3NDM3LCJpYXQiOjE2ODk5NjQxNzcsImlzcyI6IkZXTyBNaWRkbGV3YXJlIE1vZHVsZSIsImF1ZCI6IkZXTyJ9.qB7T3fDE_qN2iCcv7UWVDqa4aERw76LGGIQVVcw4eTgnISpnb5tuHZY-zAZFRZ2JiEJqgVpPouk8qhyZ1_aIIqsg5aY43Ghv0Wh6Cf1xqkz84nNqv5M_Uil7rGdywVnZI5NtQUDjYUL3su67MEYgMdi1t7UG6KzgE-52i3dcQJnzMJHu_k_A506MQ1UjLFBivxyIcWZLPSLhlYXs9x-hUPFeNRRRGO6rOder_2Z3N8cAJjpcCyPupS1cqB26fgr8gc_vtmSyZgUWRXKC-AnryvF3pFmsYcKNjCjKVwo1NSlA7kBj4AbAbM7xVZLwvM0sbWX3r-SS2bi1Jmm0vF_ynw";
graphQlClient.HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt); // Change jwt in auth header
graphQlClient.Options.ConfigureWebSocketConnectionInitPayload = httpClientOptions => new { headers = new { authorization = $"Bearer {jwt}" } };


GraphQLRequest request = new GraphQLRequest(@"
query subscribeGeneratedReportsChanges {
  report(order_by:{report_id:desc}) {
    report_id
    report_name
    report_start_time
    report_end_time
    report_type
    description
    uiuser {
      uiuser_username
    }
    report_template {
      report_template_name
    }
  }
}");

dynamic response = await graphQlClient.SendQueryAsync<dynamic>(request);
Console.WriteLine(JsonConvert.SerializeObject(response, Formatting.Indented));

GraphQLRequest streamRequest = new GraphQLRequest(@"
subscription subscribeGeneratedReportsChanges {
  report(order_by:{report_id:desc}) {
    report_id
    report_name
    report_start_time
    report_end_time
    report_type
    description
    uiuser {
      uiuser_username
    }
    report_template {
      report_template_name
    }
  }
}");

IObservable<GraphQLResponse<dynamic>> stream = graphQlClient.CreateSubscriptionStream<dynamic>(streamRequest,
    ex =>
    {
        Console.WriteLine(ex.Message);
    }
);

stream.Subscribe(x =>
{
    Console.WriteLine(JsonConvert.SerializeObject(x, Formatting.Indented));
});

while (true)
{
    Console.ReadLine();
}