using System;
using System.Collections.Generic;

namespace Hullbreach.Platform
{
    // Typed calls to the platform auth API; turns every HTTP answer or transport
    // failure into an outcome so callers never see exceptions or raw status codes.
    // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
    public sealed class PlatformClient
    {
        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public const string LoginPath = "/api/v1/auth/login";

        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public const string SessionPath = "/api/v1/auth/session";

        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public const string ServerKeyHeader = "X-Server-Key";

        readonly PlatformConfig _config;
        readonly IPlatformHttp _http;
        readonly IPlatformLog _log;

        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public PlatformClient(PlatformConfig config, IPlatformHttp http, IPlatformLog log = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _log = log ?? NullPlatformLog.Instance;
        }

        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public PlatformConfig Config => _config;

        // Player sign-in. The password goes only into the request body; logs carry the username.
        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public void Login(string username, string password, Action<LoginOutcome> onComplete)
        {
            if (onComplete == null) throw new ArgumentNullException(nameof(onComplete));
            username = username?.Trim() ?? string.Empty;
            password = password ?? string.Empty;
            if (username.Length == 0 || password.Length == 0)
            {
                onComplete(new LoginOutcome(LoginStatus.InvalidCredentials, null, username,
                    "Enter a username and password."));
                return;
            }

            string body = "{\"username\":" + PlatformJson.Quote(username) +
                          ",\"password\":" + PlatformJson.Quote(password) + "}";
            var request = new PlatformHttpRequest(PlatformHttpMethod.Post, _config.BaseUrl + LoginPath, body,
                null, _config.TimeoutSeconds);
            _log.Info($"Hullbreach platform: login request for '{username}' to {_config.BaseUrl}.");
            _http.Send(request, response =>
            {
                LoginOutcome outcome = MapLogin(username, response);
                if (outcome.IsSuccess)
                    _log.Info($"Hullbreach platform: login succeeded for '{outcome.Username}'.");
                else
                    _log.Warning($"Hullbreach platform: login failed for '{username}': {outcome.Status} ({outcome.Message}).");
                onComplete(outcome);
            });
        }

        // Game-server check of a player's token. Sends X-Server-Key when configured so the
        // call keeps working once the platform starts requiring it for server routes.
        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public void ValidateSession(string token, Action<SessionOutcome> onComplete)
        {
            if (onComplete == null) throw new ArgumentNullException(nameof(onComplete));
            if (string.IsNullOrEmpty(token))
            {
                onComplete(new SessionOutcome(SessionStatus.Invalid, null, null, "no platform token presented"));
                return;
            }

            var headers = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Authorization", "Bearer " + token),
            };
            if (_config.HasServerKey)
                headers.Add(new KeyValuePair<string, string>(ServerKeyHeader, _config.ServerKey));

            var request = new PlatformHttpRequest(PlatformHttpMethod.Get, _config.BaseUrl + SessionPath, null,
                headers, _config.TimeoutSeconds);
            _http.Send(request, response =>
            {
                SessionOutcome outcome = MapSession(response);
                if (outcome.Status == SessionStatus.Unreachable || outcome.Status == SessionStatus.Failed)
                    _log.Warning($"Hullbreach platform: session check {outcome.Status} ({outcome.Message}).");
                onComplete(outcome);
            });
        }

        // frob:doc docs/platform-link.md#failure-modes
        public static LoginOutcome MapLogin(string typedUsername, PlatformHttpResponse response)
        {
            if (response.IsTransportFailure)
                return new LoginOutcome(LoginStatus.Unreachable, null, typedUsername,
                    "Cannot reach the Hullbreach servers (" + response.TransportError + ").");

            switch (response.StatusCode)
            {
                case 200:
                    if (PlatformJson.TryParseObject(response.Body, out var root))
                    {
                        string token = PlatformJson.GetString(root, "token");
                        string name = PlatformJson.GetString(PlatformJson.GetObject(root, "user"), "username");
                        if (!string.IsNullOrEmpty(token))
                            return new LoginOutcome(LoginStatus.Success, token,
                                string.IsNullOrEmpty(name) ? typedUsername : name, "Signed in.");
                    }
                    return new LoginOutcome(LoginStatus.Failed, null, typedUsername,
                        "The server sent an unexpected sign-in response.");
                case 401:
                    return new LoginOutcome(LoginStatus.InvalidCredentials, null, typedUsername,
                        "Wrong username or password.");
                case 429:
                    return new LoginOutcome(LoginStatus.RateLimited, null, typedUsername,
                        "Too many sign-in attempts. Wait a minute and try again.");
                case 422:
                    return new LoginOutcome(LoginStatus.Failed, null, typedUsername,
                        "The server rejected the sign-in request (HTTP 422).");
                default:
                    return new LoginOutcome(LoginStatus.Failed, null, typedUsername,
                        "Sign-in failed (HTTP " + response.StatusCode + ").");
            }
        }

        // frob:doc docs/platform-link.md#failure-modes
        public static SessionOutcome MapSession(PlatformHttpResponse response)
        {
            if (response.IsTransportFailure)
                return new SessionOutcome(SessionStatus.Unreachable, null, null,
                    "platform unreachable (" + response.TransportError + ")");

            switch (response.StatusCode)
            {
                case 200:
                    if (PlatformJson.TryParseObject(response.Body, out var root))
                    {
                        string userId = PlatformJson.GetString(root, "user_id");
                        if (!string.IsNullOrEmpty(userId))
                            return new SessionOutcome(SessionStatus.Valid, userId,
                                PlatformJson.GetString(root, "role"), "session valid");
                    }
                    return new SessionOutcome(SessionStatus.Failed, null, null, "unexpected session response");
                case 401:
                    // The platform answers a bad server key with the same 401, so this also
                    // covers a misconfigured key once /session starts requiring one.
                    return new SessionOutcome(SessionStatus.Invalid, null, null, "platform session invalid or expired");
                default:
                    return new SessionOutcome(SessionStatus.Failed, null, null,
                        "session check failed (HTTP " + response.StatusCode + ")");
            }
        }
    }
}
