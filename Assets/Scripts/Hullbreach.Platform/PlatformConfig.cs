using System;
using System.Globalization;

namespace Hullbreach.Platform
{
    // Where the platform API lives and how strict the game server is about it.
    // Precedence: command-line arg > env var > default. The server key is env-only
    // so it never lands in shell history, process listings or launch scripts.
    // frob:doc docs/platform-link.md#configuration
    public sealed class PlatformConfig
    {
        // frob:doc docs/platform-link.md#configuration
        public const string DefaultBaseUrl = "http://127.0.0.1:8000";

        // frob:doc docs/platform-link.md#configuration
        public const double DefaultTimeoutSeconds = 5.0;

        // frob:doc docs/platform-link.md#configuration
        public const double DefaultHelloTimeoutSeconds = 10.0;

        // frob:doc docs/platform-link.md#configuration
        public const string UrlArg = "-hullbreachPlatformUrl";

        // frob:doc docs/platform-link.md#configuration
        public const string TimeoutArg = "-hullbreachPlatformTimeout";

        // frob:doc docs/platform-link.md#configuration
        public const string RequireAuthArg = "-hullbreachRequirePlatformAuth";

        // Recognised only to warn and ignore: keys must not travel on the command line.
        // frob:doc docs/platform-link.md#configuration
        public const string RejectedServerKeyArg = "-hullbreachPlatformServerKey";

        // frob:doc docs/platform-link.md#configuration
        public const string UrlEnv = "HULLBREACH_PLATFORM_URL";

        // frob:doc docs/platform-link.md#configuration
        public const string RequireAuthEnv = "HULLBREACH_REQUIRE_PLATFORM_AUTH";

        // frob:doc docs/platform-link.md#configuration
        public const string ServerKeyEnv = "HULLBREACH_PLATFORM_SERVER_KEY";

        // Never ends with '/', so paths can be appended directly.
        public readonly string BaseUrl;
        public readonly double TimeoutSeconds;
        public readonly bool RequireAuth;

        // Secret: only HasServerKey may appear in logs.
        public readonly string ServerKey;

        // How long a connected peer may stay silent before a required-auth server drops it.
        public readonly double HelloTimeoutSeconds;

        // frob:doc docs/platform-link.md#configuration
        public PlatformConfig(string baseUrl = DefaultBaseUrl, double timeoutSeconds = DefaultTimeoutSeconds,
            bool requireAuth = false, string serverKey = null,
            double helloTimeoutSeconds = DefaultHelloTimeoutSeconds)
        {
            BaseUrl = (string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.Trim()).TrimEnd('/');
            TimeoutSeconds = timeoutSeconds > 0 ? timeoutSeconds : DefaultTimeoutSeconds;
            RequireAuth = requireAuth;
            ServerKey = string.IsNullOrEmpty(serverKey) ? null : serverKey;
            HelloTimeoutSeconds = helloTimeoutSeconds > 0 ? helloTimeoutSeconds : DefaultHelloTimeoutSeconds;
        }

        // frob:doc docs/platform-link.md#configuration
        public bool HasServerKey => ServerKey != null;

        // Reads the process args/env through delegates so tests need no real environment.
        // Bad values fall back to defaults with a warning rather than aborting startup.
        // frob:doc docs/platform-link.md#configuration
        public static PlatformConfig FromEnvironment(string[] args, Func<string, string> getEnv, IPlatformLog log)
        {
            args = args ?? Array.Empty<string>();
            getEnv = getEnv ?? (_ => null);
            log = log ?? NullPlatformLog.Instance;

            string url = ArgValue(args, UrlArg) ?? NonEmpty(getEnv(UrlEnv)) ?? DefaultBaseUrl;
            if (!IsHttpUrl(url))
            {
                log.Warning($"Hullbreach platform: ignoring platform URL '{url}' (must be http:// or https://); using {DefaultBaseUrl}.");
                url = DefaultBaseUrl;
            }

            double timeout = DefaultTimeoutSeconds;
            string timeoutText = ArgValue(args, TimeoutArg);
            if (timeoutText != null &&
                (!double.TryParse(timeoutText, NumberStyles.Float, CultureInfo.InvariantCulture, out timeout) || timeout <= 0))
            {
                log.Warning($"Hullbreach platform: ignoring {TimeoutArg} '{timeoutText}'; using {DefaultTimeoutSeconds}s.");
                timeout = DefaultTimeoutSeconds;
            }

            bool requireAuth = HasFlag(args, RequireAuthArg) || IsTruthy(getEnv(RequireAuthEnv));

            if (HasFlag(args, RejectedServerKeyArg))
                log.Warning($"Hullbreach platform: {RejectedServerKeyArg} is not supported and was ignored; set {ServerKeyEnv} in the environment instead.");

            return new PlatformConfig(url, timeout, requireAuth, NonEmpty(getEnv(ServerKeyEnv)));
        }

        // The one line both client and server log at startup; never shows the key itself.
        // frob:doc docs/platform-link.md#configuration
        public string Describe() =>
            $"url={BaseUrl} timeout={TimeoutSeconds.ToString(CultureInfo.InvariantCulture)}s " +
            $"requireAuth={(RequireAuth ? "yes" : "no")} serverKey={(HasServerKey ? "set" : "not set")}";

        // frob:doc docs/platform-link.md#configuration
        public override string ToString() => Describe();

        static string ArgValue(string[] args, string name)
        {
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == name)
                    return NonEmpty(args[i + 1]);
            return null;
        }

        static bool HasFlag(string[] args, string name)
        {
            foreach (string arg in args)
                if (arg == name)
                    return true;
            return false;
        }

        static bool IsTruthy(string value)
        {
            if (value == null) return false;
            value = value.Trim();
            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsHttpUrl(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out Uri uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https");

        static string NonEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
