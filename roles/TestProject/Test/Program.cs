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

// admin jwt
string jwt = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6ImFkbWluIiwieC1oYXN1cmEtdXNlci1pZCI6IjciLCJ4LWhhc3VyYS11dWlkIjoidWlkPWFkbWluLG91PXRlbmFudDAsb3U9b3BlcmF0b3Isb3U9dXNlcixkYz1md29yY2gsZGM9aW50ZXJuYWwiLCJ4LWhhc3VyYS10ZW5hbnQtaWQiOiIxIiwieC1oYXN1cmEtdmlzaWJsZS1tYW5hZ2VtZW50cyI6InsgMiwxLDU1IH0iLCJ4LWhhc3VyYS12aXNpYmxlLWRldmljZXMiOiJ7IDEsMiw1NiB9Iiwicm9sZSI6ImFkbWluIiwieC1oYXN1cmEtYWxsb3dlZC1yb2xlcyI6WyJhZG1pbiJdLCJ4LWhhc3VyYS1kZWZhdWx0LXJvbGUiOiJhZG1pbiIsIm5iZiI6MTY5MDEyMDMyOCwiZXhwIjoxNjkwMTYzNTg4LCJpYXQiOjE2OTAxMjAzMjgsImlzcyI6IkZXTyBNaWRkbGV3YXJlIE1vZHVsZSIsImF1ZCI6IkZXTyJ9.i98Ht5x4uU5mBJUKuz9_w9m6cAi3D2x7-6GiXTGbpQRJld529erAT1P1dURXpKHn1DmNDBy8fRnF863kUxesfENAVUxwNMnxtC4nSzmzc_kHkkbvZhON6L3lwDml5_xs3Ie189UH0AJTkFy91jpo5KEdDtv4ufHPDlLCRWyGFEPpWuGIIKQiFhWFGk2LwiDwphLdFNx8Hsnf_D-HGf13ipItvdgJkGayFkIUqgMK7PlLuPnJL-lQwUQTVtrCcBdeO8G3hwtCyJDy7TSL_g_rd_Kf1D1BKcEBHcNigmjLfOVyyyzqOLBL7baaPrPIfZ1u_PrHE_-PQucGtlRB8DQWwA";
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

var subscription = stream.Subscribe(x =>
{
    Console.WriteLine(JsonConvert.SerializeObject(x, Formatting.Indented));
});


await Task.Delay(10_000);

subscription.Dispose();

string jwt2 = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6InVzZXIxX2RlbW8iLCJ4LWhhc3VyYS11c2VyLWlkIjoiNTciLCJ4LWhhc3VyYS11dWlkIjoidWlkPXVzZXIxX2RlbW8sb3U9dGVuYW50MV9kZW1vLG91PW9wZXJhdG9yLG91PXVzZXIsZGM9ZndvcmNoLGRjPWludGVybmFsIiwieC1oYXN1cmEtdGVuYW50LWlkIjoiMiIsIngtaGFzdXJhLXZpc2libGUtbWFuYWdlbWVudHMiOiJ7IDEgfSIsIngtaGFzdXJhLXZpc2libGUtZGV2aWNlcyI6InsgMSB9Iiwicm9sZSI6InJlcG9ydGVyIiwieC1oYXN1cmEtYWxsb3dlZC1yb2xlcyI6WyJyZXBvcnRlciJdLCJ4LWhhc3VyYS1kZWZhdWx0LXJvbGUiOiJyZXBvcnRlciIsIm5iZiI6MTY5MDEyMDM3NCwiZXhwIjoxNjkwMTYzNjM0LCJpYXQiOjE2OTAxMjAzNzQsImlzcyI6IkZXTyBNaWRkbGV3YXJlIE1vZHVsZSIsImF1ZCI6IkZXTyJ9.G6wdq_mVrbdCit9Pg6fyLsTHAOQ1Hgnbq3Q2i746ELlYxsGGr3vHCJFteZe0Uli_BjIiY99pHeZSH7PR6wOJXzC6wIDaZgIAQZo3ZuArr_WhjMzKT0cGR8cp-nWg2N-6hzz0B2FHA090Cy33dy0e-3NcyOC9Q-0o2hRwE7tTcjcYHuMKa_Fok7LWbDrLZFweD1IKh2UWQU451oiBg5Kgr1Ow1DpUrxMLTdyRHx7jGwDYpE7QjLUabH_MSWpLyGCVyaacGLlf5Zid5KBsRglZ5EB7l9tyNOV9Rx8WZk_HTdE-ol6xAqATGoBCntpq84nziXDb-WWtTqKAfQfgSK72PA";
graphQlClient.HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt2); // Change jwt in auth header
graphQlClient.Options.ConfigureWebSocketConnectionInitPayload = httpClientOptions => new { headers = new { authorization = $"Bearer {jwt2}" } };

Console.WriteLine("---- NEW JWT ----");
Console.WriteLine("---- NEW JWT ----");
Console.WriteLine("---- NEW JWT ----");

response = await graphQlClient.SendQueryAsync<dynamic>(request);
Console.WriteLine(JsonConvert.SerializeObject(response, Formatting.Indented));

stream = graphQlClient.CreateSubscriptionStream<dynamic>(streamRequest,
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