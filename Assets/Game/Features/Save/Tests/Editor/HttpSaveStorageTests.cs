using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Commands;
using Game.Http;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Save.Config;
using Save.Identity;
using Save.Storage;

namespace Save.Tests.Editor
{
    public sealed class HttpSaveStorageTests
    {
        private readonly List<LocalDiskStorage> _storages = new();

        [TearDown]
        public async Task TearDown()
        {
            foreach (var storage in _storages)
                await storage.DeleteAsync(CancellationToken.None);
            _storages.Clear();
        }

        [Test]
        public async Task SaveAsync_WritesLocalFirst_ThenPostsJsonStringToServer()
        {
            const string saveJson = "{\"Meta\":{\"Revision\":7},\"Modules\":{}}";
            var connection = new FakeConnectionService(FakeResponse.Success("{}"));
            var local = CreateLocalStorage();
            var storage = CreateStorage(local, connection);

            await storage.SaveAsync(saveJson, CancellationToken.None);

            Assert.That(await local.LoadAsync(CancellationToken.None), Is.EqualTo(saveJson));
            Assert.That(connection.Requests, Has.Count.EqualTo(1));
            Assert.That(connection.Requests[0].Method, Is.EqualTo(HTTPMethods.Post));
            Assert.That(connection.Requests[0].Url, Is.EqualTo("https://example.test/api/v1/save/global"));
            Assert.That(connection.Requests[0].Headers["Content-Type"], Is.EqualTo("application/json"));

            var body = JObject.Parse(connection.Requests[0].RawText);
            Assert.That(body["playerId"]?.Value<string>(), Is.EqualTo("player-1"));
            Assert.That(body["data"]?.Type, Is.EqualTo(JTokenType.String));
            Assert.That(body["data"]?.Value<string>(), Is.EqualTo(saveJson));
        }

        [Test]
        public async Task SaveAsync_WhenServerFails_KeepsLocalData()
        {
            const string saveJson = "{\"Meta\":{\"Revision\":8},\"Modules\":{}}";
            var connection = new FakeConnectionService(FakeResponse.Failure(503, "unavailable"));
            var local = CreateLocalStorage();
            var storage = CreateStorage(local, connection);

            await storage.SaveAsync(saveJson, CancellationToken.None);

            Assert.That(await local.LoadAsync(CancellationToken.None), Is.EqualTo(saveJson));
        }

        [Test]
        public async Task LoadAsync_WhenServerSucceeds_UpdatesLocalCache()
        {
            var serverSave = "{\"Meta\":{\"Revision\":9},\"Modules\":{\"shop\":{\"Version\":1,\"Json\":{}}}}";
            var envelope = JObject.FromObject(new { data = JObject.Parse(serverSave), lastModified = 123L }).ToString();
            var connection = new FakeConnectionService(FakeResponse.Success(envelope));
            var local = CreateLocalStorage();
            await local.SaveAsync("{\"Meta\":{\"Revision\":1},\"Modules\":{}}", CancellationToken.None);
            var storage = CreateStorage(local, connection);

            var loaded = await storage.LoadAsync(CancellationToken.None);

            Assert.That(JObject.Parse(loaded)["Meta"]?["Revision"]?.Value<int>(), Is.EqualTo(9));
            Assert.That(JObject.Parse(await local.LoadAsync(CancellationToken.None))["Meta"]?["Revision"]?.Value<int>(), Is.EqualTo(9));
            Assert.That(connection.Requests[0].Method, Is.EqualTo(HTTPMethods.Get));
            Assert.That(connection.Requests[0].Url, Is.EqualTo("https://example.test/api/v1/save/global?playerId=player-1"));
        }

        [Test]
        public async Task LoadAsync_WhenServerFails_ReturnsLocalCache()
        {
            const string localSave = "{\"Meta\":{\"Revision\":5},\"Modules\":{}}";
            var connection = new FakeConnectionService(FakeResponse.Failure(503, "unavailable"));
            var local = CreateLocalStorage();
            await local.SaveAsync(localSave, CancellationToken.None);
            var storage = CreateStorage(local, connection);

            var loaded = await storage.LoadAsync(CancellationToken.None);

            Assert.That(loaded, Is.EqualTo(localSave));
        }

        private LocalDiskStorage CreateLocalStorage()
        {
            var storage = new LocalDiskStorage($"codex_http_save_test_{Guid.NewGuid():N}.json");
            _storages.Add(storage);
            return storage;
        }

        private static HttpSaveStorage CreateStorage(LocalDiskStorage local, FakeConnectionService connection)
        {
            return new HttpSaveStorage(
                new TestSaveBackendConfig(),
                local,
                new TestIdentityProvider(),
                connection,
                new NoOpLogger(),
                new NoOpErrorReporter());
        }

        private sealed class TestSaveBackendConfig : ISaveBackendConfig
        {
            public string BaseUrl => "https://example.test/api/v1/";
            public string SavePath => "save/global";
            public int RequestTimeoutMs => 5000;
            public int RetryCount => 0;
        }

        private sealed class TestIdentityProvider : IPlayerIdentityProvider
        {
            public string GetPlayerId() => "player-1";
        }

        private sealed class NoOpLogger : ICommandLogger
        {
            public void Log(CommandLogLevel level, string message) { }
            public void LogException(Exception exception, string message = null) { }
        }

        private sealed class NoOpErrorReporter : ICommandErrorReporter
        {
            public void Report(Exception exception, string message = null) { }
        }

        private sealed class FakeConnectionService : IConnectionService
        {
            private readonly Queue<FakeResponse> _responses = new();
            public readonly List<CapturedRequest> Requests = new();

            public FakeConnectionService(params FakeResponse[] responses)
            {
                foreach (var response in responses)
                    _responses.Enqueue(response);
            }

            public bool IsConnected => true;
            public ConnectionCheckBehaviour CheckInternetBehaviour => ConnectionCheckBehaviour.ErrorLogsWithComplete;

            public IRequest CreateRequest(IRequestParams p)
            {
                var response = _responses.Count > 0 ? _responses.Dequeue() : FakeResponse.Success("{}");
                var captured = new CapturedRequest(p.UriPath.ToString(), p.MethodType);
                Requests.Add(captured);
                return new FakeRequest(p, captured, response);
            }

            public void HandleNoInternet(Action callbackOnAvailable) { }
            public void SubscribeOnceOnInternetBecomeAvailable(Action callback) { }
            public void UnsubscribeFromInternetBecomeAvailable(Action callback) { }
        }

        private sealed class FakeRequest : IRequest
        {
            private readonly IRequestParams _params;
            private readonly CapturedRequest _captured;
            private readonly FakeResponse _response;

            public FakeRequest(IRequestParams parameters, CapturedRequest captured, FakeResponse response)
            {
                _params = parameters;
                _captured = captured;
                _response = response;
            }

            public RequestStates State { get; private set; } = RequestStates.Finished;
            public Exception Exception { get; private set; }
            public TimeSpan RequestTimeout { get; set; }

            public void AddHeader(string name, string value) => _captured.Headers[name] = value;
            public void SetHeader(string name, string value) => _captured.Headers[name] = value;
            public void AddField(string name, string value) { }
            public void AddBinaryData(string name, byte[] value) { }
            public void SetRawData(byte[] data) => _captured.RawText = Encoding.UTF8.GetString(data);

            public void Send()
            {
                State = RequestStates.Finished;
                _params.RequestFinished?.Invoke(this, _response);
            }

            public UniTask SendAsync()
            {
                Send();
                return UniTask.CompletedTask;
            }

            public void Abort()
            {
                State = RequestStates.Aborted;
            }
        }

        private sealed class FakeResponse : IResponse
        {
            private readonly string _text;

            private FakeResponse(bool isSuccess, string text, int statusCode, string errorMessage)
            {
                IsSuccess = isSuccess;
                _text = text;
                StatusCode = statusCode;
                ErrorMessage = errorMessage;
            }

            public bool IsSuccess { get; }
            public byte[] Data => Encoding.UTF8.GetBytes(_text ?? string.Empty);
            public string DataAsText => _text;
            public int StatusCode { get; }
            public string ErrorMessage { get; }
            public string GetFirstHeaderValue(string name) => null;

            public static FakeResponse Success(string text) => new(true, text, 200, null);
            public static FakeResponse Failure(int statusCode, string errorMessage) => new(false, "", statusCode, errorMessage);
        }

        private sealed class CapturedRequest
        {
            public CapturedRequest(string url, HTTPMethods method)
            {
                Url = url;
                Method = method;
            }

            public string Url { get; }
            public HTTPMethods Method { get; }
            public Dictionary<string, string> Headers { get; } = new();
            public string RawText { get; set; }
        }
    }
}
