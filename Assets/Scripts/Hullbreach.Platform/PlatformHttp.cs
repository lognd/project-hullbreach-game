using System;
using System.Collections.Generic;

namespace Hullbreach.Platform
{
    // The verbs the auth, match and queue endpoints use.
    // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
    public enum PlatformHttpMethod
    {
        Get,
        Post,
        Delete,
    }

    // One request, shaped so a fake can assert on it without a socket.
    // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
    public sealed class PlatformHttpRequest
    {
        public readonly PlatformHttpMethod Method;
        public readonly string Url;

        // JSON text, or null for a request without a body.
        public readonly string Body;

        // Header values can be secrets (bearer token, server key): never log them.
        public readonly IReadOnlyList<KeyValuePair<string, string>> Headers;

        public readonly double TimeoutSeconds;

        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public PlatformHttpRequest(PlatformHttpMethod method, string url, string body,
            IReadOnlyList<KeyValuePair<string, string>> headers, double timeoutSeconds)
        {
            Method = method;
            Url = url;
            Body = body;
            Headers = headers ?? Array.Empty<KeyValuePair<string, string>>();
            TimeoutSeconds = timeoutSeconds;
        }

        // Linear scan: requests carry at most two headers.
        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public string Header(string name)
        {
            foreach (var header in Headers)
                if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
                    return header.Value;
            return null;
        }
    }

    // Either an HTTP status with a body, or a transport failure (no status at all).
    // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
    public readonly struct PlatformHttpResponse
    {
        // 0 when the request never got an HTTP answer.
        public readonly int StatusCode;

        public readonly string Body;

        // Null on any HTTP answer, including 4xx/5xx.
        public readonly string TransportError;

        PlatformHttpResponse(int statusCode, string body, string transportError)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
            TransportError = transportError;
        }

        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public bool IsTransportFailure => TransportError != null;

        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public static PlatformHttpResponse Http(int statusCode, string body) =>
            new PlatformHttpResponse(statusCode, body, null);

        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public static PlatformHttpResponse Transport(string error) =>
            new PlatformHttpResponse(0, string.Empty, string.IsNullOrEmpty(error) ? "transport error" : error);
    }

    // The seam between engine-free logic and a real HTTP stack (UnityWebRequest
    // in the game, HttpClient in the live test). The gate needs onComplete on
    // the caller's thread; UnityWebRequest guarantees that.
    // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
    public interface IPlatformHttp
    {
        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        void Send(PlatformHttpRequest request, Action<PlatformHttpResponse> onComplete);
    }
}
