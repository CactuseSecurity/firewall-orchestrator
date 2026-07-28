using FWO.Basics;
using FWO.Data.Modelling;
using FWO.Services.EventMediator.Events;
using FWO.Test.Mocks;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components.Forms;
using NUnit.Framework;
using RestSharp;
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
        private static readonly List<string> kValidImportCsvLines =
        [
            "App-Server-Name,External-App-ID,App-Server-Typ,App-IP-Address-Range",
            "Server1,App1,TypeA,not-an-ip",
            "Server2,App2,MissingType,10.0.0.2/32",
            "broken|line"
        ];
        private static readonly string kValidImportCsv = string.Join(Environment.NewLine, kValidImportCsvLines);

        [Test]
        public async Task ReadFileToBytes_WithSupportedFile_PublishesSuccessAndAllowsLogoImport()
        {
            RecordingEventMediator mediator = new();
            using TestMiddlewareClient middlewareClient = new();
            FileUploadService service = CreateService(mediator, middlewareClient);
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
            using TestMiddlewareClient middlewareClient = new();
            FileUploadService service = CreateService(mediator, middlewareClient);
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
            using TestMiddlewareClient middlewareClient = new();
            FileUploadService service = CreateService(mediator, middlewareClient);
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
            using TestMiddlewareClient middlewareClient = new();
            FileUploadService service = CreateService(mediator, middlewareClient);
            await service.ReadFileToBytes(CreateInputFileChangeEventArgs("apps.csv", "text/csv", Encoding.UTF8.GetBytes(kValidImportCsv), kValidImportCsv.Length));

            await service.ImportAppServersFromCSV("apps.csv");

            AppServerImportEvent importEvent = (AppServerImportEvent)mediator.PublishedEvents.Last(eventItem => eventItem.Name == nameof(FileUploadService.ImportAppServersFromCSV)).Event;
            AppServerImportEventArgs eventArgs = importEvent.EventArgs ?? throw new AssertionException("Missing app server import event args.");

            Assert.Multiple(() =>
            {
                Assert.That(mediator.PublishedEvents.Any(eventItem => eventItem.Name == nameof(FileUploadService.ImportAppServersFromCSV)), Is.True);
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
            using TestMiddlewareClient middlewareClient = new();
            middlewareClient.UseHandler(new SingleResponseHandler(HttpStatusCode.OK, JsonSerializer.Serialize("Ok: imported")));
            FileUploadService service = CreateService(mediator, middlewareClient);
            await service.ReadFileToBytes(CreateInputFileChangeEventArgs("matrix.csv", "text/csv", Encoding.UTF8.GetBytes("matrix-data"), "matrix-data".Length));

            await service.ImportComplianceMatrix("matrix.csv");

            FileUploadEvent importEvent = (FileUploadEvent)mediator.PublishedEvents.Last(eventItem => eventItem.Name == nameof(FileUploadService.ImportComplianceMatrix)).Event;
            FileUploadEventArgs eventArgs = importEvent.EventArgs ?? throw new AssertionException("Missing file upload event args.");

            Assert.Multiple(() =>
            {
                Assert.That(mediator.PublishedEvents.Any(eventItem => eventItem.Name == nameof(FileUploadService.ImportComplianceMatrix)), Is.True);
                Assert.That(eventArgs.Success, Is.True);
                Assert.That(eventArgs.Data, Is.EqualTo("Ok: imported"));
            });
        }

        [Test]
        public async Task ImportComplianceMatrix_PublishesMiddlewareError()
        {
            RecordingEventMediator mediator = new();
            using TestMiddlewareClient middlewareClient = new();
            middlewareClient.UseHandler(new SingleResponseHandler(HttpStatusCode.InternalServerError, JsonSerializer.Serialize("middleware failed")));
            FileUploadService service = CreateService(mediator, middlewareClient);
            await service.ReadFileToBytes(CreateInputFileChangeEventArgs("matrix.csv", "text/csv", Encoding.UTF8.GetBytes("matrix-data"), "matrix-data".Length));

            await service.ImportComplianceMatrix("matrix.csv");

            FileUploadEvent importEvent = (FileUploadEvent)mediator.PublishedEvents.Last(eventItem => eventItem.Name == nameof(FileUploadService.ImportComplianceMatrix)).Event;
            FileUploadEventArgs eventArgs = importEvent.EventArgs ?? throw new AssertionException("Missing file upload event args.");

            Assert.Multiple(() =>
            {
                Assert.That(eventArgs.Success, Is.False);
                Assert.That((string?)eventArgs.Data, Is.EqualTo("middleware failed"));
            });
        }

        [Test]
        public void GetResponseMessage_ReturnsErrorMessage_WhenPresent()
        {
            string? message = InvokeGetResponseMessage(new RestResponse<string>(new RestRequest())
            {
                ErrorMessage = "middleware failed",
                Content = "\"ignored\""
            });

            Assert.That(message, Is.EqualTo("middleware failed"));
        }

        [Test]
        public void GetResponseMessage_ReturnsNull_WhenContentIsEmpty()
        {
            string? message = InvokeGetResponseMessage(new RestResponse<string>(new RestRequest())
            {
                Content = ""
            });

            Assert.That(message, Is.Null);
        }

        [Test]
        public void GetResponseMessage_DeserializesJsonStringContent()
        {
            string? message = InvokeGetResponseMessage(new RestResponse<string>(new RestRequest())
            {
                Content = JsonSerializer.Serialize("middleware failed")
            });

            Assert.That(message, Is.EqualTo("middleware failed"));
        }

        [Test]
        public void GetResponseMessage_ReturnsNull_WhenContentIsJsonNull()
        {
            string? message = InvokeGetResponseMessage(new RestResponse<string>(new RestRequest())
            {
                Content = "null"
            });

            Assert.That(message, Is.Null);
        }

        [Test]
        public void GetResponseMessage_ReturnsFallbackMessage_WhenContentIsNotJson()
        {
            string? message = InvokeGetResponseMessage(new RestResponse<string>(new RestRequest())
            {
                Content = "<html><body>Internal Server Error</body></html>"
            });

            Assert.That(message, Is.EqualTo("file_upload_failed"));
        }

        private static FileUploadService CreateService(RecordingEventMediator mediator, TestMiddlewareClient middlewareClient)
        {
            SimulatedUserConfig userConfig = new()
            {
                ModNamingConvention = "{}",
                ModAppServerTypes = JsonSerializer.Serialize<List<AppServerType>>([new AppServerType { Id = 1, Name = "TypeA" }])
            };
            userConfig.User.Name = "tester";
            userConfig.User.Dn = "uid=tester,ou=people,dc=example,dc=com";

            MockApiConnection apiConnection = new();
            return new FileUploadService(apiConnection.AsSub(), userConfig, middlewareClient, kAllowedFileFormats, mediator);
        }

        private static ReadOnlyMemory<byte> GetLogoBytes()
        {
            return Encoding.UTF8.GetBytes(kLogoText);
        }

        private static InputFileChangeEventArgs CreateInputFileChangeEventArgs(string fileName, string contentType, ReadOnlyMemory<byte> content, long size)
        {
            TestBrowserFile browserFile = new(fileName, contentType, content, size);
            return new InputFileChangeEventArgs([browserFile]);
        }

        private static string? InvokeGetResponseMessage(RestResponse<string> response)
        {
            using TestMiddlewareClient middlewareClient = new();
            return InvokeGetResponseMessage(CreateService(new RecordingEventMediator(), middlewareClient), response);
        }

        private static string? InvokeGetResponseMessage(FileUploadService service, RestResponse<string> response)
        {
            MethodInfo method = typeof(FileUploadService).GetMethod("GetResponseMessage", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(FileUploadService).FullName, "GetResponseMessage");

            object?[] invokeArgs = new object?[] { response };
            return (string?)method.Invoke(service, invokeArgs);
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

    }
}
