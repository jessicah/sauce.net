using Microsoft.Extensions.Logging;

namespace Sauce
{
    public static partial class LogMessages
    {
        [LoggerMessage(LogLevel.Debug, "Sauce Connected: {Id}: {Endpoint}: {Reason}")]
        public static partial void SauceReconnected(ILogger logger, long id, string endpoint, string reason);

        [LoggerMessage(LogLevel.Debug, "Sauce Disconnected: {Endpoint}: {Id}")]
        public static partial void SauceDisconnected(ILogger logger, long id, string endpoint);

        [LoggerMessage(LogLevel.Information, "Sauce Message: {Id}: {Endpoint}: {ParsedMessage}; {RawMessage}")]
        public static partial void SauceMessage(ILogger logger, long id, string endpoint, object parsedMessage, string rawMessage);

        [LoggerMessage(LogLevel.Information, "Sauce Call: {Endpoint}: {ParsedMessage}: {RawMessage}")]
        public static partial void SauceCall(ILogger logger, string endpoint, object parsedMessage, string rawMessage);

        [LoggerMessage(LogLevel.Error, "Sauce Parsing Error: {Endpoint}: Unknown: {RawMessage}; FullValue: {FullMessage}")]
        public static partial void SauceUnknownParsingError(ILogger logger, string endpoint, string rawMessage, string fullMessage);

        [LoggerMessage(LogLevel.Error, "Sauce Parsing Error: {Endpoint}: Path: {Path}; Value: {RawMessage}; FullValue: {FullMessage}")]
        public static partial void SauceParsingError(ILogger logger, string endpoint, string path, string rawMessage, string fullMessage);
    }
}
