using System;
using System.Text;
using Hullbreach.Platform;
using UnityEngine;
using UnityEngine.Networking;

namespace Hullbreach.NetCode.Entities
{
    // IPlatformHttp over UnityWebRequest: it runs on every target we ship (desktop,
    // Linux dedicated server, IL2CPP, WebGL) and completes on the main thread, which
    // the ECS systems that consume the callbacks require.
    // frob:doc docs/platform-link.md#http-transport
    public sealed class HullbreachUnityPlatformHttp : IPlatformHttp
    {
        // frob:doc docs/platform-link.md#http-transport
        public void Send(PlatformHttpRequest request, Action<PlatformHttpResponse> onComplete)
        {
            var web = new UnityWebRequest(request.Url,
                request.Method == PlatformHttpMethod.Post ? UnityWebRequest.kHttpVerbPOST : UnityWebRequest.kHttpVerbGET)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Math.Max(1, (int)Math.Ceiling(request.TimeoutSeconds)),
            };
            if (request.Body != null)
            {
                web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.Body));
                web.SetRequestHeader("Content-Type", "application/json");
            }
            web.SetRequestHeader("Accept", "application/json");
            foreach (var header in request.Headers)
                web.SetRequestHeader(header.Key, header.Value);

            UnityWebRequestAsyncOperation operation;
            try
            {
                operation = web.SendWebRequest();
            }
            catch (InvalidOperationException exception)
            {
                // Plain http:// is refused before any I/O unless Player Settings allow it.
                web.Dispose();
                onComplete(PlatformHttpResponse.Transport("request blocked: " + exception.Message));
                return;
            }

            operation.completed += _ =>
            {
                PlatformHttpResponse response = web.result == UnityWebRequest.Result.Success ||
                                                web.result == UnityWebRequest.Result.ProtocolError
                    ? PlatformHttpResponse.Http((int)web.responseCode, web.downloadHandler?.text)
                    : PlatformHttpResponse.Transport(web.error);
                web.Dispose();
                onComplete(response);
            };
        }
    }

    // Debug.Log adapter; the engine-free layer already keeps secrets out of messages.
    // frob:doc docs/platform-link.md#http-transport
    public sealed class HullbreachUnityPlatformLog : IPlatformLog
    {
        // frob:doc docs/platform-link.md#http-transport
        public void Info(string message) => Debug.Log(message);

        // frob:doc docs/platform-link.md#http-transport
        public void Warning(string message) => Debug.LogWarning(message);
    }

    // Process-wide platform wiring, built lazily from args/env and logged once.
    // frob:doc docs/platform-link.md#configuration
    public static class HullbreachPlatformRuntime
    {
        static PlatformConfig s_config;
        static PlatformClient s_client;
        static SessionTokenStore s_store;

        // frob:doc docs/platform-link.md#configuration
        public static readonly IPlatformLog Log = new HullbreachUnityPlatformLog();

        // frob:doc docs/platform-link.md#configuration
        public static PlatformConfig Config
        {
            get
            {
                if (s_config != null) return s_config;
                s_config = PlatformConfig.FromEnvironment(Environment.GetCommandLineArgs(),
                    Environment.GetEnvironmentVariable, Log);
                Debug.Log("Hullbreach platform: config " + s_config.Describe());
                return s_config;
            }
        }

        // frob:doc docs/platform-link.md#configuration
        public static PlatformClient Client =>
            s_client ??= new PlatformClient(Config, new HullbreachUnityPlatformHttp(), Log);

        // Remembered session file under the per-user data folder (S07-3).
        // frob:doc docs/platform-link.md#remembered-session
        public static SessionTokenStore TokenStore =>
            s_store ??= new SessionTokenStore(System.IO.Path.Combine(Application.persistentDataPath, "platform-session.txt"), Log);

        // Headless demo clients cannot click the sign-in screen, so a token handed
        // over in the environment signs them in before the first scene loads.
        // frob:doc docs/platform-link.md#client-session
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SignInFromEnvironment() =>
            PlatformSession.SignInFromEnvironment(Environment.GetEnvironmentVariable, Log);

        // Statics survive play-mode restarts when domain reload is off; start clean each run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_config = null;
            s_client = null;
            s_store = null;
        }
    }
}
