using FWO.Basics;
using FWO.Data.Modelling;
using FWO.Services.EventMediator.Events;
using FWO.Test.Mocks;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components.Forms;
using NUnit.Framework;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    public class FileUploadServiceTest
    {
        private static readonly string kAllowedFileFormats = ".png,.csv";
        private static readonly string kLogoText = "logo-bytes";
        private static readonly List<string> kValidImportCsvLines = new List<string>
        {
            "App-Server-Name,External-App-ID,App-Server-Typ,App-IP-Address-Range",
            "Server1,App1,TypeA,not-an-ip",
            "Server2,App2,MissingType,10.0.0.2/32",
            "broken|line"
        };
        private static readonly string kValidImportCsv = string.Join(Environment.NewLine, kValidImportCsvLines);

        [Test]
        public async Task ReadFileToBytes_WithSupportedFile_PublishesSuccessAndAllowsLogoImport()
        {
            RecordingEventMediator mediator = new();
            FileUploadService service = CreateService(mediator);
            ReadOnlyMemory<byte> logoBytes = GetLogoBytes();
            InputFileChangeEventArgs args = CreateInputFileChangeEventArgs("logo.png", "image/png", logoBytes, logoBytes.Length);

            FileUploadEventArgs result = await service.ReadFileToBytes(args);
            FileUploadEventArgs logoResult = service.ImportCustomLogo();

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(logoResult.Success, Is.True);
                Assert.That(logoResult.Data, Is.EqualTo(Convert.ToBase64String(logoBytes.ToArray())));
                Assert.That(mediator.PublishedEvents, Has.Count.EqualTo(2));
                Assert.That(mediator.PublishedEvents[0].Name, Is.EqualTo(nameof(FileUploadService.ReadFileToBytes)));
                Assert.That(mediator.PublishedEvents[1].Name, Is.EqualTo(nameof(FileUploadService.ImportCustomLogo)));
            });
        }

        [Test]
        public async Task ReadFileToBytes_RejectsUnsupportedExtension()
        {
            RecordingEventMediator mediator = new();
            FileUploadService service = CreateService(mediator);
            ReadOnlyMemory<byte> logoBytes = GetLogoBytes();
            InputFileChangeEventArgs args = CreateInputFileChangeEventArgs("logo.txt", "text/plain", logoBytes, logoBytes.Length);

            FileUploadEventArgs result = await service.ReadFileToBytes(args);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error!.Message, Is.EqualTo("E5430"));
                Assert.That(mediator.PublishedEvents, Has.Count.EqualTo(1));
                Assert.That(mediator.PublishedEvents[0].Name, Is.EqualTo(nameof(FileUploadService.ReadFileToBytes)));
            });
        }

        [Test]
        public async Task ReadFileToBytes_RejectsOversizedFile()
        {
            RecordingEventMediator mediator = new();
            FileUploadService service = CreateService(mediator);
            ReadOnlyMemory<byte> logoBytes = GetLogoBytes();
            InputFileChangeEventArgs args = CreateInputFileChangeEventArgs("logo.png", "image/png", logoBytes, GlobalConst.MaxUploadFileSize + 1);

            FileUploadEventArgs result = await service.ReadFileToBytes(args);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error!.Message, Is.EqualTo("E5431"));
                Assert.That(mediator.PublishedEvents, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task ImportAppServersFromCsv_PublishesErrorsForMalformedAndInvalidRows()
        {
            RecordingEventMediator mediator = new();
            FileUploadService service = CreateService(mediator);
            SetUploadedData(service, kValidImportCsv);

            await service.ImportAppServersFromCSV("apps.csv");

            AppServerImportEvent importEvent = (AppServerImportEvent)mediator.PublishedEvents.Single().Event;
            AppServerImportEventArgs eventArgs = importEvent.EventArgs ?? throw new AssertionException("Missing app server import event args.");

            Assert.Multiple(() =>
            {
                Assert.That(mediator.PublishedEvents[0].Name, Is.EqualTo(nameof(FileUploadService.ImportAppServersFromCSV)));
                Assert.That(eventArgs.Success, Is.False);
                Assert.That(eventArgs.Errors, Has.Count.EqualTo(3));
                Assert.That(eventArgs.Appserver, Is.Empty);
                Assert.That(eventArgs.Errors.Any(error => error.Message == "E5422"), Is.True);
                Assert.That(eventArgs.Errors.Any(error => error.Message == "E5423"), Is.True);
                Assert.That(eventArgs.Errors.Any(error => error.Message?.Contains("owner_appservertype_notfound") == true), Is.True);
            });
        }

        [Test]
        public async Task ImportComplianceMatrix_PublishesMiddlewareSuccess()
        {
            RecordingEventMediator mediator = new();
            TestMiddlewareClient middlewareClient = new();
            middlewareClient.UseHandler(new SingleResponseHandler(CreateJsonResponse(HttpStatusCode.OK, "Ok: imported")));
            FileUploadService service = CreateService(mediator, middlewareClient);
            SetUploadedData(service, "matrix-data");

            await service.ImportComplianceMatrix("matrix.csv");

            FileUploadEvent importEvent = (FileUploadEvent)mediator.PublishedEvents.Single().Event;
            FileUploadEventArgs eventArgs = importEvent.EventArgs ?? throw new AssertionException("Missing file upload event args.");

            Assert.Multiple(() =>
            {
                Assert.That(mediator.PublishedEvents[0].Name, Is.EqualTo(nameof(FileUploadService.ImportComplianceMatrix)));
                Assert.That(eventArgs.Success, Is.True);
                Assert.That(eventArgs.Data, Is.EqualTo("Ok: imported"));
            });
        }

        [Test]
        public async Task ImportComplianceMatrix_PublishesMiddlewareError()
        {
            RecordingEventMediator mediator = new();
            TestMiddlewareClient middlewareClient = new();
            middlewareClient.UseHandler(new SingleResponseHandler(CreateJsonResponse(HttpStatusCode.InternalServerError, "middleware failed")));
            FileUploadService service = CreateService(mediator, middlewareClient);
            SetUploadedData(service, "matrix-data");

            await service.ImportComplianceMatrix("matrix.csv");

            FileUploadEvent importEvent = (FileUploadEvent)mediator.PublishedEvents.Single().Event;
            FileUploadEventArgs eventArgs = importEvent.EventArgs ?? throw new AssertionException("Missing file upload event args.");

            Assert.Multiple(() =>
            {
                Assert.That(eventArgs.Success, Is.False);
                Assert.That(eventArgs.Data, Is.EqualTo("middleware failed"));
            });
        }

        private static FileUploadService CreateService(RecordingEventMediator mediator, TestMiddlewareClient? middlewareClient = null)
        {
            SimulatedUserConfig userConfig = new()
            {
                ModNamingConvention = "{}",
                ModAppServerTypes = JsonSerializer.Serialize(new List<AppServerType> { new AppServerType { Id = 1, Name = "TypeA" } })
            };
            userConfig.User.Name = "tester";
            userConfig.User.Dn = "uid=tester,ou=people,dc=example,dc=com";

            MockApiConnection apiConnection = new();
            return new FileUploadService(apiConnection.AsSub(), userConfig, middlewareClient ?? new TestMiddlewareClient(), kAllowedFileFormats, mediator);
        }

        private static ReadOnlyMemory<byte> GetLogoBytes()
        {
            return Encoding.UTF8.GetBytes(kLogoText);
        }

        private static InputFileChangeEventArgs CreateInputFileChangeEventArgs(string fileName, string contentType, ReadOnlyMemory<byte> content, long size)
        {
            TestBrowserFile browserFile = new(fileName, contentType, content, size);
            return new InputFileChangeEventArgs(new List<IBrowserFile> { browserFile });
        }

        private static void SetUploadedData(FileUploadService service, string data)
        {
            PropertyInfo property = typeof(FileUploadService).GetProperty("UploadedData", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(typeof(FileUploadService).FullName, "UploadedData");
            property.SetValue(service, Encoding.UTF8.GetBytes(data));
        }

        private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string body)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }

        private sealed class TestBrowserFile : IBrowserFile
        {
            private readonly ReadOnlyMemory<byte> content;

            public TestBrowserFile(string name, string contentType, ReadOnlyMemory<byte> content, long size)
            {
                Name = name;
                ContentType = contentType;
                this.content = content;
                Size = size;
            }

            public string Name { get; }
            public DateTimeOffset LastModified { get; } = DateTimeOffset.UtcNow;
            public long Size { get; }
            public string ContentType { get; }

            public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            {
                return new MemoryStream(content.ToArray(), writable: false);
            }
        }

        private sealed class SingleResponseHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage response;

            public SingleResponseHandler(HttpResponseMessage response)
            {
                this.response = response;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(response);
            }
        }
    }
}
