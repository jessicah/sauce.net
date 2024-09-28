using System.Text;
using System.Text.Json;
using Websocket.Client;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace Sauce
{
    public sealed class Client : IDisposable
    {
        private ILogger _logger;
        private HttpClient _httpClient;
        private static long _initialId;

        private static JsonSerializerOptions _options;

        static Client()
        {
            _initialId = Environment.TickCount64;

            if (_initialId < 0)
                _initialId = -_initialId;

            _options = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };
        }

        public Client(ILogger<Client> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _httpClient.BaseAddress = new Uri("http://192.168.1.100:1080");

            if (_httpClient.BaseAddress is null)
            {
                throw new ArgumentException("BaseAddress must be set on HttpClient", nameof(httpClient));
            }
        }

        private WebsocketClient NewWebsocketClient()
        {
            try
            {
                var scheme = _httpClient.BaseAddress!.Scheme == "https" ? "wss" : "ws";

                var websocketClient = new WebsocketClient(new Uri($"{scheme}://{_httpClient.BaseAddress.Authority}/api/ws/events"))
                {
                    Name = "SauceClient.Net",
                    ReconnectTimeout = TimeSpan.FromMinutes(5),
                    ErrorReconnectTimeout = TimeSpan.FromSeconds(10)
                };

                return websocketClient;
            } catch (Exception exn)
            {
                Console.WriteLine($"unable to create new websocket client: {exn.Message}");
                throw;
            }
        }

        public async Task<WebsocketClient> SubscribeTo<T>(Subscription subscription, Action<T> action)
        {
            var websocketClient = NewWebsocketClient();

            websocketClient.ReconnectionHappened.Subscribe(message =>
            {
                var reason = message.Type switch
                {
                    ReconnectionType.Initial => "initial",
                    ReconnectionType.Lost => "lost",
                    ReconnectionType.NoMessageReceived => "no messages",
                    ReconnectionType.Error => "error",
                    ReconnectionType.ByUser => "user requested",
                    ReconnectionType.ByServer => "server requested",
                    _ => "unknown"
                };

                LogMessages.SauceReconnected(_logger, subscription.UId, subscription.Path, reason);

                websocketClient.Send(subscription.Message);
            });

            websocketClient.DisconnectionHappened.Subscribe(message =>
            {
                LogMessages.SauceDisconnected(_logger, subscription.UId, subscription.Path);
            });

            websocketClient.MessageReceived.Subscribe(message =>
            {
                if (string.IsNullOrWhiteSpace(message.Text))
                {
                    return;
                }

                if (0 == Interlocked.Exchange(ref subscription._processingMessage, 1))
                {
                    try
                    {
                        var response = JsonSerializer.Deserialize<WebsocketResponse<T>>(message.Text, _options);

                        if (response?.Type == "response")
                        {
                            Console.WriteLine($"have response data, skipping: {message.Text}");
                            return;
                        }

                        if (response is null || response.Success == false || response.Data is null) return;

                        LogMessages.SauceMessage(_logger, subscription.UId, subscription.Path, response.Data, message.Text);

                        action(response.Data);
                    }
                    catch (JsonException exception)
                    {
                        DebugException(exception, message.Text, subscription.Path);
                    }
                    finally
                    {
                        Interlocked.Exchange(ref subscription._processingMessage, 0);
                    }
                }
                else
                {
                    Console.WriteLine("Sauce.Client.SubscribeTo.MessageReceived: already processing message");
                }
            });

            if (websocketClient.IsStarted == false)
            {
                try
                {
                    await websocketClient.Start();
                } catch (ObjectDisposedException)
                {
                    Console.WriteLine("Unable to start client, disposed");
                }
                catch (Exception exn)
                {
                    Console.WriteLine($"unable to start client: {exn.Message}");
                    throw;
                }
            }

            return websocketClient;
        }

        public static void UnsubscribeFrom(WebsocketClient client, Subscription subscription)
        {
            client.Send(Unsubscribe(subscription.SubId));
        }

        public async Task<T?> Call<T>(string methodName, params object[] arguments)
        {
            var url = $"/api/rpc/v2/{methodName}";

            foreach (var argument in arguments)
            {
                var encodedArgument = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(argument)));
                url = $"{url}/{encodedArgument}";
            }

            string text = string.Empty;

            try
            {
                text = await _httpClient.GetStringAsync(url);
                var json = JsonSerializer.Deserialize<CallResponse<T>>(text, _options);

                if (json is null || json.Success == false) return default;

                LogMessages.SauceCall(_logger, methodName, json, text);

                return json.Value;
            }
            catch (JsonException exception)
            {
                DebugException(exception, text, methodName);

                throw;
            }
            catch (Exception)
            {
                return default;
            }
        }

        public void DebugException(JsonException exception, string json, string path)
        {
            if (exception.LineNumber.HasValue == false || exception.BytePositionInLine.HasValue == false)
            {
                LogMessages.SauceUnknownParsingError(_logger, path, json, string.Empty);

                return;
            }

            long lineNumber = exception.LineNumber.Value;
            long lineOffset = exception.BytePositionInLine.Value;

            int offset = 0;

            var fragment = json;

            while (lineNumber > 0)
            {
                offset = fragment.IndexOf('\n', offset);
                if (offset < 0)
                {
                    LogMessages.SauceUnknownParsingError(_logger, path, fragment, json);

                    return;
                }
                --lineNumber;
            }

            var substring = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(fragment.Substring(offset)).Skip((int)lineOffset).ToArray());

            LogMessages.SauceParsingError(_logger, path, exception.Path ?? string.Empty, substring, json);
        }

        public static Subscription Watching(bool persistent = false) => Subscribe("athlete/watching", persistent);
        public static Subscription Groups() => Subscribe("groups");
        public static Subscription Nearby() => Subscribe("nearby");
        public static Subscription Chat() => Subscribe("chat", true);
        public static Subscription RideOn() => Subscribe("rideon");

        private static Subscription Subscribe(string path, bool persistent = false)
        {
            long uid = Interlocked.Increment(ref _initialId);
            long subId = Interlocked.Increment(ref _initialId);

            if (persistent)
            {
                return new(uid, subId, path, $$"""
                {
                    "type": "request",
                    "uid": {{uid}},
                    "data": {
                        "method": "subscribe",
                        "arg": {
                            "event": "{{path}}",
                            "subId": {{subId}},
                            "persistent": true
                        }
                    }
                }
                """);
            }
            else
            {
                return new(uid, subId, path, $$"""
                {
                    "type": "request",
                    "uid": {{uid}},
                    "data": {
                        "method": "subscribe",
                        "arg": {
                            "event": "{{path}}",
                            "subId": {{subId}}
                        }
                    }
                }
                """);
            }
        }

        private static string Unsubscribe(long subId)
        {
            long uid = Interlocked.Increment(ref _initialId);

            return $$"""
            {
                "type": "request",
                "uid": {{uid}},
                "data": {
                    "method": "unsubscribe",
                    "arg": {
                        "subId": {{subId}}
                    }
                }
            }
            """;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
