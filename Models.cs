using System.Text.Json.Serialization;

namespace Sauce
{
    public record CallResponse<T>(
            [property: JsonPropertyName("success")] bool Success,
            [property: JsonPropertyName("value")] T Value
        );

    public record PayloadWrapper<T>(
        [property: JsonPropertyName("data")] T Data
    );

    public record Subscription(long UId, long SubId, string Path, string Message)
    {
        internal int _processingMessage = 0;
    }

    public record WebsocketResponse<T>(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("uid")] int Id,
        [property: JsonPropertyName("data")] T Data
    );
}
