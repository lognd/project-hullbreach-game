# Platform link

How the game talks to the Hullbreach platform API (the FastAPI service in
the sibling `platform` repo): a player signs in from the game client, and
the game server checks that player's platform session before letting the
connection into the match. Tickets: S07-2 (sign-in screen, client side) and
S07-4 (server-side session check at connect), both under S07.

Two layers, split the same way as `Hullbreach.Net` and
`Hullbreach.NetCode.Entities` (see [netcode.md](netcode.md)):

- `Assets/Scripts/Hullbreach.Platform` is plain C# with no engine
  reference: configuration, the typed client for the auth endpoints, the
  outcome types, and the server's admission policy. It builds and tests
  under `tools/plaincs/run_tests.sh` with a fake HTTP transport.
- `Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatform*.cs` is
  the thin Unity layer: the `UnityWebRequest` transport, the hello and
  admission RPCs, and the two ECS systems that drive the policy.

## Configuration

Read once per process by `HullbreachPlatformRuntime.Config` and logged as
one line, e.g. `Hullbreach platform: config url=http://127.0.0.1:8000
timeout=5s requireAuth=no serverKey=not set`. Precedence is command-line
argument, then environment variable, then default. A malformed URL or
timeout is logged as a warning and replaced by the default rather than
aborting startup.

| Argument | Environment variable | Default | Meaning |
| --- | --- | --- | --- |
| `-hullbreachPlatformUrl <url>` | `HULLBREACH_PLATFORM_URL` | `http://127.0.0.1:8000` | Base URL of the platform API (http or https; a trailing `/` is dropped). Used by the client to sign in and by the server to validate sessions. |
| `-hullbreachPlatformTimeout <seconds>` | (none) | `5` | Per-request HTTP timeout. |
| `-hullbreachRequirePlatformAuth` | `HULLBREACH_REQUIRE_PLATFORM_AUTH` (`1`, `true` or `yes`) | not required | Server only. When set, every connection must present a platform session the platform accepts, or it is disconnected. When not set, everyone is admitted and signed-in players are shown as verified. |
| (never an argument) | `HULLBREACH_PLATFORM_SERVER_KEY` | unset | Server only. Sent as `X-Server-Key` on session checks. Must match one of the platform's `HULLBREACH_GAME_SERVER_API_KEYS`. Env-only so it never lands in shell history, process listings or committed launch scripts; `-hullbreachPlatformServerKey` is recognised only to log a warning and is otherwise ignored. |
| (internal) | (none) | `10` s | How long a required-auth server waits for a connection's hello before rejecting it (`PlatformConfig.HelloTimeoutSeconds`). |

The server logs its mode at startup either way: `server REQUIRES platform
sign-in; unverified players are disconnected.` or `server allows guests;
platform sign-in is optional`. A required-auth server without a server key
logs a warning.

Secrets (password, token, server key) never reach a log line: `Describe()`
prints only `serverKey=set|not set`, `LoginOutcome.ToString()` omits the
token, the client logs usernames only, and the tests assert all of this
against recorded logs, including the live integration test.

<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformConfig.cs::PlatformConfig -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformConfig.cs::PlatformConfig.DefaultBaseUrl -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformConfig.cs::PlatformConfig.DefaultTimeoutSeconds -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformConfig.cs::PlatformConfig.DefaultHelloTimeoutSeconds -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformConfig.cs::PlatformConfig.UrlArg -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformConfig.cs::PlatformConfig.TimeoutArg -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformConfig.cs::PlatformConfig.RequireAuthArg -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformConfig.cs::PlatformConfig.RejectedServerKeyArg -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformConfig.cs::PlatformConfig.UrlEnv -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformConfig.cs::PlatformConfig.RequireAuthEnv -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformConfig.cs::PlatformConfig.ServerKeyEnv -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformConfig.cs::PlatformConfig.PlatformConfig -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformConfig.cs::PlatformConfig.HasServerKey -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformConfig.cs::PlatformConfig.FromEnvironment -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformConfig.cs::PlatformConfig.Describe -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformConfig.cs::PlatformConfig.ToString -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformRuntime.cs::HullbreachPlatformRuntime -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformRuntime.cs::HullbreachPlatformRuntime.Log -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformRuntime.cs::HullbreachPlatformRuntime.Config -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformRuntime.cs::HullbreachPlatformRuntime.Client -->

## Handshake

1. The player signs in on the client (the S07-2 sign-in screen calls
   `PlatformClient.Login`); the token goes into `PlatformSession`, in
   memory only.
2. The client connects through the lobby/Relay or direct path as before.
   When the client world first sees its `NetworkId`,
   `HullbreachPlatformAuthClientSystem` sends one
   `HullbreachPlatformHelloRpc { Token, Username }`. A player who chose
   to play offline (and every thin client) sends the hello with an empty
   token.
3. On the server, `HullbreachPlatformAuthServerSystem` feeds connects,
   hellos and disconnects into `PlatformAdmissionGate`. For a non-empty
   token the gate calls `PlatformClient.ValidateSession`, i.e.
   `GET /api/v1/auth/session` with `Authorization: Bearer <token>` and
   `X-Server-Key: <key>`.
4. The gate emits one `AdmissionDecision` per verdict. The server system
   tags admitted connections with `HullbreachPlatformAdmitted` (only
   those are joined to the match and marked `NetworkStreamInGame` by
   `HullbreachNetCodeServerSystem`), sends the player a
   `HullbreachPlatformAdmissionRpc { Admitted, Verified, DisplayName,
   Reason }`, and disconnects rejected peers 0.5 s later so the reason
   arrives first.
5. The client stores the verdict in `HullbreachPlatformAdmission`;
   `HullbreachNetworkGameHud` shows its `StatusLine` (for example
   `Platform: signed in as pilot`).

Policy by mode:

| Situation | Not required (default) | Required |
| --- | --- | --- |
| Connect | Admitted at once as `Guest #<id>` | Held, not in the match |
| Hello with empty token | Stays guest | Rejected: "This server requires a Hullbreach account. Sign in and reconnect." |
| Token valid | Upgraded to verified, platform username shown | Admitted verified |
| Token invalid/expired (401) | Stays guest, reason shown in HUD | Rejected: "Your platform session is invalid or expired. Sign in again." |
| Platform unreachable / unexpected answer | Stays guest, reason shown | Rejected (fail closed): "Could not verify your platform session (...)" |
| No hello within 10 s | Unaffected | Rejected: "No platform sign-in received in time." |

Edge cases the gate handles: a second hello from the same connection and
a hello from an unknown connection are ignored with a warning; a session
check that completes after its peer left (or after the `NetworkId` was
reused by a new connection) is dropped by a per-connection generation
number; a peer whose check is in flight is not timed out.

Known limits, recorded for follow-up:

- `/api/v1/auth/session` returns `user_id` and `role`, not a username, so
  the displayed name is the one the client claims in its hello. It is
  only shown as verified when it arrives together with a token the
  platform accepted, but the platform does not confirm the pair. A
  platform change to return `username` from `/session` would close this.
- The platform route currently authenticates the player's bearer token
  only; it does not check `X-Server-Key` yet (`require_game_server`
  exists but no route uses it). The game server already sends the key so
  nothing changes on this side when the platform starts requiring it.
  The platform answers a bad key with the same 401 as a bad token, so a
  wrong key will show up as "invalid or expired" on every player.
- The hello travels inside the Netcode connection, which is not
  encrypted on the direct path. Relay connections are DTLS-protected.

<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformAdmissionGate.cs::AdmissionDecision -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformAdmissionGate.cs::AdmissionDecision.AdmissionDecision -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformAdmissionGate.cs::AdmissionDecision.ToString -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformAdmissionGate.cs::PlatformAdmissionGate -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformAdmissionGate.cs::PlatformAdmissionGate.MaxDisplayNameLength -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformAdmissionGate.cs::PlatformAdmissionGate.PlatformAdmissionGate -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformAdmissionGate.cs::PlatformAdmissionGate.RequireAuth -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformAdmissionGate.cs::PlatformAdmissionGate.OnConnected -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformAdmissionGate.cs::PlatformAdmissionGate.OnHello -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformAdmissionGate.cs::PlatformAdmissionGate.OnDisconnected -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformAdmissionGate.cs::PlatformAdmissionGate.Tick -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformAdmissionGate.cs::PlatformAdmissionGate.IsAdmitted -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformAdmissionGate.cs::PlatformAdmissionGate.TryTakeDecision -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformAdmissionGate.cs::PlatformAdmissionGate.GuestName -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformAdmissionGate.cs::PlatformAdmissionGate.SanitizeName -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformAuthClientSystem.cs::HullbreachPlatformAdmission -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformAuthClientSystem.cs::HullbreachPlatformAdmission.HasVerdict -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformAuthClientSystem.cs::HullbreachPlatformAdmission.Admitted -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformAuthClientSystem.cs::HullbreachPlatformAdmission.Verified -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformAuthClientSystem.cs::HullbreachPlatformAdmission.DisplayName -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformAuthClientSystem.cs::HullbreachPlatformAdmission.Reason -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformAuthClientSystem.cs::HullbreachPlatformAdmission.StatusLine -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformAuthClientSystem.cs::HullbreachPlatformAuthClientSystem -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformAuthServerSystem.cs::HullbreachPlatformAuthServerSystem -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformRpc.cs::HullbreachPlatformHelloRpc -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformRpc.cs::HullbreachPlatformAdmissionRpc -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformRpc.cs::HullbreachPlatformAdmitted -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformRpc.cs::HullbreachPlatformTracked -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformRpc.cs::HullbreachPlatformHelloSent -->

## Failure modes

What each platform answer becomes, and what the player sees.

Login (`PlatformClient.MapLogin`):

| Platform answer | `LoginStatus` | Message shown |
| --- | --- | --- |
| 200 with `token` and `user.username` | `Success` | "Signed in." |
| 200 with an unexpected body | `Failed` | "The server sent an unexpected sign-in response." |
| 401 | `InvalidCredentials` | "Wrong username or password." |
| 429 (5 failed attempts per username per minute) | `RateLimited` | "Too many sign-in attempts. Wait a minute and try again." |
| 422 | `Failed` | "The server rejected the sign-in request (HTTP 422)." |
| any other status | `Failed` | "Sign-in failed (HTTP n)." |
| no HTTP answer (refused, DNS, timeout, insecure HTTP blocked) | `Unreachable` | "Cannot reach the Hullbreach servers (...)." |
| empty username or password (no request sent) | `InvalidCredentials` | "Enter a username and password." |

Session check (`PlatformClient.MapSession`): 200 with `user_id` is
`Valid`; 401 is `Invalid` (missing, expired or revoked token, and also a
rejected server key); a transport failure is `Unreachable`; anything else
is `Failed`. The handshake table above shows how the gate reacts to each.

<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformClient.cs::PlatformClient.MapLogin -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformClient.cs::PlatformClient.MapSession -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformOutcomes.cs::LoginStatus -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformOutcomes.cs::LoginOutcome -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformOutcomes.cs::LoginOutcome.LoginOutcome -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformOutcomes.cs::LoginOutcome.IsSuccess -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformOutcomes.cs::LoginOutcome.ToString -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformOutcomes.cs::SessionStatus -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformOutcomes.cs::SessionOutcome -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformOutcomes.cs::SessionOutcome.SessionOutcome -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformOutcomes.cs::SessionOutcome.IsValid -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformOutcomes.cs::SessionOutcome.ToString -->

## HTTP transport

`IPlatformHttp` is the seam between the engine-free code and a real HTTP
stack. In the game it is `HullbreachUnityPlatformHttp`, built on
`UnityWebRequest`, chosen over `System.Net.Http.HttpClient` because:

- it works on every target the project builds, including the Linux
  dedicated server, IL2CPP players (where `HttpClient` needs extra
  stripping care) and WebGL (where `HttpClient` cannot open sockets);
- its completion callback runs on the main thread, which the ECS systems
  consuming the result require; an `HttpClient` task would need marshalling
  back to the main thread;
- it honours the project's Player Settings for insecure HTTP and
  certificates in one place.

Plain `http://` URLs are refused by Unity unless Player Settings allow
them. `ProjectSettings.asset` sets `insecureHttpOption: 1` ("Allowed in
development builds"), so the default `http://127.0.0.1:8000` works in
the editor and development builds; a release build must point at an
`https://` platform URL. A blocked request comes back as `Unreachable`
with the reason "request blocked: ...".

The live integration test uses its own blocking `HttpClient` transport,
which is fine under the .NET SDK where the test runs.

<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformRuntime.cs::HullbreachUnityPlatformHttp -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformRuntime.cs::HullbreachUnityPlatformHttp.Send -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformRuntime.cs::HullbreachUnityPlatformLog -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformRuntime.cs::HullbreachUnityPlatformLog.Info -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachPlatformRuntime.cs::HullbreachUnityPlatformLog.Warning -->

## Client session

`PlatformSession` holds the signed-in username and token for this run of
the game only; nothing is written to disk (persisting the session between
launches is S07-3). The sign-in screen writes it; the client handshake
system reads it when it says hello. Treat `Token` as a secret: it may be
passed to the handshake and nowhere else.

<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformSession.cs::PlatformSession -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformSession.cs::PlatformSession.IsSignedIn -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformSession.cs::PlatformSession.Username -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformSession.cs::PlatformSession.Token -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformSession.cs::PlatformSession.SignIn -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformSession.cs::PlatformSession.SignOut -->

## Hullbreach.Platform module reference

The remaining engine-free types: `PlatformClient` (typed calls to
`POST /api/v1/auth/login` and `GET /api/v1/auth/session` that never throw
for network or HTTP failures), the `IPlatformHttp` request/response
types, `PlatformJson` (a minimal JSON reader/writer: the assembly has no
engine or package references, so neither `JsonUtility` nor
`System.Text.Json` is available on every target), and the `IPlatformLog`
logging seam (`HullbreachUnityPlatformLog` adapts it to `Debug.Log`).

<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformClient.cs::PlatformClient -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformClient.cs::PlatformClient.LoginPath -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformClient.cs::PlatformClient.SessionPath -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformClient.cs::PlatformClient.ServerKeyHeader -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformClient.cs::PlatformClient.PlatformClient -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformClient.cs::PlatformClient.Config -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformClient.cs::PlatformClient.Login -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformClient.cs::PlatformClient.ValidateSession -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformHttp.cs::PlatformHttpMethod -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformHttp.cs::PlatformHttpRequest -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformHttp.cs::PlatformHttpRequest.PlatformHttpRequest -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformHttp.cs::PlatformHttpRequest.Header -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformHttp.cs::PlatformHttpResponse -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformHttp.cs::PlatformHttpResponse.IsTransportFailure -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformHttp.cs::PlatformHttpResponse.Http -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformHttp.cs::PlatformHttpResponse.Transport -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformHttp.cs::IPlatformHttp -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformHttp.cs::IPlatformHttp.Send -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformJson.cs::PlatformJson -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformJson.cs::PlatformJson.Quote -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformJson.cs::PlatformJson.TryParseObject -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformJson.cs::PlatformJson.GetString -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformJson.cs::PlatformJson.GetObject -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformLog.cs::IPlatformLog -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformLog.cs::IPlatformLog.Info -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformLog.cs::IPlatformLog.Warning -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformLog.cs::NullPlatformLog -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformLog.cs::NullPlatformLog.Instance -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformLog.cs::NullPlatformLog.Info -->
<!-- describes: Assets/Scripts/Hullbreach.Platform/PlatformLog.cs::NullPlatformLog.Warning -->

## Testing

- `tools/plaincs/run_tests.sh --filter "FullyQualifiedName~Hullbreach.Platform"`
  runs the unit tests in `Assets/Tests/EditMode/Hullbreach.Platform.Tests`
  against fakes.
- `PlatformLiveIntegrationTests` runs only when `HULLBREACH_PLATFORM_IT_URL`
  is set. It signs in with `HULLBREACH_PLATFORM_IT_USER` /
  `HULLBREACH_PLATFORM_IT_PASSWORD` (wrong password first, to prove the
  401 mapping), then runs the admission gate in required mode with
  `HULLBREACH_PLATFORM_SERVER_KEY`, admitting the real token and
  rejecting a forged one, and asserts no secret appeared in any log line.
  Pass the variables to the test host with `dotnet test -e`, which also
  works under WSL with the Windows `dotnet.exe` (WSLENV forwarding does
  not reach the test host there), e.g.
  `tools/plaincs/run_tests.sh --filter "FullyQualifiedName~PlatformLiveIntegrationTests" -e HULLBREACH_PLATFORM_IT_URL=http://127.0.0.1:8002 -e HULLBREACH_PLATFORM_IT_USER=<user> -e HULLBREACH_PLATFORM_IT_PASSWORD=<password> -e HULLBREACH_PLATFORM_SERVER_KEY=<dev key>`.
  Use a throwaway account and a dev-only key: `-e` values are visible in
  the process list.
  Add `--logger "console;verbosity=detailed"` to see its `[platform-it]`
  transcript.
