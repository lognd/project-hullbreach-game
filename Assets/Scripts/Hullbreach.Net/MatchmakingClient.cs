using System;
using System.Collections.Generic;
using Hullbreach.Platform;

namespace Hullbreach.Net
{
    // The platform's view of the caller's place: the `state` field of a QueueStatus.
    // frob:doc docs/reference/hullbreach-net.md#matchmaking
    public enum QueueWireState
    {
        Idle,
        Queued,
        Matched,
    }

    // Parsed QueueStatus body of POST/GET /api/v1/queue.
    // frob:doc docs/reference/hullbreach-net.md#matchmaking
    public sealed class QueueStatus
    {
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public QueueWireState State { get; set; }
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public double Rating { get; set; }
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public double WaitedSeconds { get; set; }
        // Current rating window the platform pairs within.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public double Window { get; set; }
        // Handoff parsed from `match`; unset (IsOk false) unless State is Matched.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public ConfigResult<MatchHandoff> Match { get; set; }
    }

    // One status per way a queue call can end, so callers never see raw HTTP codes.
    // frob:doc docs/reference/hullbreach-net.md#matchmaking
    public enum QueueCallStatus
    {
        // 200 with a readable QueueStatus (join/poll) or 204 (cancel).
        Ok,
        // 401: the token is missing, expired or revoked.
        Unauthorized,
        // 409 already_matched: an earlier match is still unacknowledged; cancel once, then rejoin.
        AlreadyMatched,
        // No HTTP answer at all.
        Unreachable,
        // 5xx.
        ServerError,
        // Any other refusal (422, unexpected 4xx).
        Rejected,
        // 200 but the body was not a QueueStatus.
        BadResponse,
    }

    // Result of one queue call; Status is null unless the call returned one.
    // frob:doc docs/reference/hullbreach-net.md#matchmaking
    public readonly struct QueueOutcome
    {
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public QueueOutcome(QueueCallStatus status, QueueStatus queueStatus, string message)
        {
            Status = status;
            Queue = queueStatus;
            Message = message ?? string.Empty;
        }

        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public QueueCallStatus Status { get; }
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public QueueStatus Queue { get; }
        // Safe to show and to log; the token never appears in it.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public string Message { get; }

        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public bool IsOk => Status == QueueCallStatus.Ok;
        // Transient failures are worth another poll; the rest end the search.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public bool IsRetryable => Status == QueueCallStatus.Unreachable || Status == QueueCallStatus.ServerError;

        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public override string ToString() => $"{Status} ({Message})";
    }

    // Typed calls to /api/v1/queue over the platform HTTP seam; every answer or transport
    // failure becomes a QueueOutcome and nothing throws on server input.
    // frob:doc docs/reference/hullbreach-net.md#matchmaking
    // frob:ticket 01M3DG5Y1PX311E24E62K5FAKG
    public sealed class MatchmakingClient
    {
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public const string QueuePath = "/api/v1/queue";

        readonly PlatformConfig _config;
        readonly IPlatformHttp _http;
        readonly IPlatformLog _log;

        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public MatchmakingClient(PlatformConfig config, IPlatformHttp http, IPlatformLog log = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _log = log ?? NullPlatformLog.Instance;
        }

        // POST: joins the queue (a repeat join keeps the place).
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public void Join(string token, Action<QueueOutcome> onComplete) =>
            Send(PlatformHttpMethod.Post, "join", token, onComplete);

        // GET: reads the caller's status; the platform pairs players on every poll.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public void Poll(string token, Action<QueueOutcome> onComplete) =>
            Send(PlatformHttpMethod.Get, "poll", token, onComplete);

        // DELETE: leaves the queue and acknowledges a made match; always 204 when it works.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public void Cancel(string token, Action<QueueOutcome> onComplete) =>
            Send(PlatformHttpMethod.Delete, "cancel", token, onComplete);

        void Send(PlatformHttpMethod method, string what, string token, Action<QueueOutcome> onComplete)
        {
            if (onComplete == null) throw new ArgumentNullException(nameof(onComplete));
            if (string.IsNullOrEmpty(token))
            {
                var none = new QueueOutcome(QueueCallStatus.Unauthorized, null, "not signed in");
                _log.Warning($"Hullbreach matchmaking: {what} not sent: {none.Message}.");
                onComplete(none);
                return;
            }

            var headers = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Authorization", "Bearer " + token),
            };
            var request = new PlatformHttpRequest(method, _config.BaseUrl + QueuePath, null, headers,
                _config.TimeoutSeconds);
            _http.Send(request, response =>
            {
                QueueOutcome outcome = Map(method, response);
                if (outcome.IsOk) _log.Info($"Hullbreach matchmaking: {what} ok.");
                else _log.Warning($"Hullbreach matchmaking: {what} failed: {outcome}.");
                onComplete(outcome);
            });
        }

        // Pure mapping of an HTTP answer; cancel treats any 2xx as success.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public static QueueOutcome Map(PlatformHttpMethod method, PlatformHttpResponse response)
        {
            if (response.IsTransportFailure)
                return new QueueOutcome(QueueCallStatus.Unreachable, null,
                    "platform unreachable (" + response.TransportError + ")");

            int code = response.StatusCode;
            if (method == PlatformHttpMethod.Delete && code >= 200 && code < 300)
                return new QueueOutcome(QueueCallStatus.Ok, null, "left the queue");
            if (code == 200 && method != PlatformHttpMethod.Delete) return ParseStatus(response.Body);
            if (code == 401) return new QueueOutcome(QueueCallStatus.Unauthorized, null, "sign in again");
            if (code == 409 && Kind(response.Body) == "already_matched")
                return new QueueOutcome(QueueCallStatus.AlreadyMatched, null, "an earlier match is unacknowledged");
            if (code >= 500) return new QueueOutcome(QueueCallStatus.ServerError, null, "platform error (HTTP " + code + ")");
            return new QueueOutcome(QueueCallStatus.Rejected, null, "queue request refused (HTTP " + code + ")");
        }

        static QueueOutcome ParseStatus(string body)
        {
            if (!PlatformJson.TryParseObject(body, out var root))
                return Bad("queue status is not a JSON object");
            string state = PlatformJson.GetString(root, "state");
            var status = new QueueStatus
            {
                Rating = Number(root, "rating"),
                WaitedSeconds = Number(root, "waited_seconds"),
                Window = Number(root, "window"),
            };
            switch (state)
            {
                case "idle":
                    status.State = QueueWireState.Idle;
                    break;
                case "queued":
                    status.State = QueueWireState.Queued;
                    break;
                case "matched":
                    status.State = QueueWireState.Matched;
                    var match = PlatformJson.GetObject(root, "match");
                    status.Match = match == null
                        ? ConfigResult<MatchHandoff>.Fail("matched status has no match")
                        : MatchHandoff.Parse(PlatformJson.GetString(match, "match_id"),
                            PlatformJson.GetString(match, "server_address"),
                            PlatformJson.GetString(match, "opponent_id"));
                    break;
                default:
                    return Bad("unknown queue state '" + state + "'");
            }
            return new QueueOutcome(QueueCallStatus.Ok, status, "state " + state);
        }

        static QueueOutcome Bad(string why) => new QueueOutcome(QueueCallStatus.BadResponse, null, why);

        static double Number(Dictionary<string, object> obj, string key) =>
            obj.TryGetValue(key, out object value) && value is double d ? d : 0.0;

        static string Kind(string body) =>
            PlatformJson.TryParseObject(body, out var root) ? PlatformJson.GetString(root, "kind") ?? string.Empty
                : string.Empty;
    }
}
