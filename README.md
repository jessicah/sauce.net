# Example Use in ASP.NET Core

```cs
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddHttpClient<Sauce.Client>(client =>
    {
        // Instance of Sauce connecting to
        client.BaseAddress = new Uri("http://192.168.1.100:1080");
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        return new SocketsHttpHandler()
        {
            PooledConnectionLifetime = TimeSpan.FromHours(1)
        };
    })
    .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

builder.Services
    .AddScoped<Sauce.Client>();
```

# Subscripting to Messages

```cs
// You can use DI to assign to _sauce via ctor()
private Sauce.Client _sauce;
private Subscription? _subscription;
private WebsocketClient? _websocketClient;

private async Task DoInitializeAsync()
{
  // this is a predefined subscription, you can create your own
  _subscription = Sauce.Client.Watching();
  _websocketClient = await _sauce.SubscribeTo<Watched>(_subscription, async watched =>
  {
    // process the `Watched watched` input
  });
}

public void Dispose()
{
  if (_websocketClient is not null && _subscription is not null)
  {
    Sauce.Client.UnsubscribeFrom(_websocketClient, _subscription);
    _websocketClient.Dispose();
    _websocketClient = null;
    _subscription = null;
  }
}
```

The class passed to `SubscribeTo` is a model you define, which will be constructed
from the JSON received via the websocket. This way you can define only the fields
you are interested in receiving.
