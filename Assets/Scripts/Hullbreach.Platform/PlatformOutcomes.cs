namespace Hullbreach.Platform
{
    // One status per thing the sign-in screen must say differently.
    // frob:doc docs/platform-link.md#failure-modes
    public enum LoginStatus
    {
        Success,
        // 401: unknown user and wrong password are deliberately indistinguishable.
        InvalidCredentials,
        // 429 from the platform's failed-login limiter.
        RateLimited,
        // No HTTP answer at all: DNS, refused, timeout, insecure-HTTP block.
        Unreachable,
        // Any other answer (422, 5xx, unparseable body).
        Failed,
    }

    // Result of a login call. Token is the one secret here: ToString leaves it out.
    // frob:doc docs/platform-link.md#failure-modes
    public readonly struct LoginOutcome
    {
        public readonly LoginStatus Status;

        // Empty unless Status is Success.
        public readonly string Token;

        // The platform's canonical username on success, else what was typed.
        public readonly string Username;

        // Safe to show the player and to log.
        public readonly string Message;

        // frob:doc docs/platform-link.md#failure-modes
        public LoginOutcome(LoginStatus status, string token, string username, string message)
        {
            Status = status;
            Token = token ?? string.Empty;
            Username = username ?? string.Empty;
            Message = message ?? string.Empty;
        }

        // frob:doc docs/platform-link.md#failure-modes
        public bool IsSuccess => Status == LoginStatus.Success;

        // frob:doc docs/platform-link.md#failure-modes
        public override string ToString() => $"{Status} user='{Username}' ({Message})";
    }

    // Server-side verdict on a token.
    // frob:doc docs/platform-link.md#failure-modes
    public enum SessionStatus
    {
        Valid,
        // 401: missing, malformed, expired or revoked token (the platform does not say which).
        Invalid,
        Unreachable,
        Failed,
    }

    // Result of GET /auth/session. The platform returns user_id and role, not a username.
    // frob:doc docs/platform-link.md#failure-modes
    public readonly struct SessionOutcome
    {
        public readonly SessionStatus Status;
        public readonly string UserId;
        public readonly string Role;
        public readonly string Message;

        // frob:doc docs/platform-link.md#failure-modes
        public SessionOutcome(SessionStatus status, string userId, string role, string message)
        {
            Status = status;
            UserId = userId ?? string.Empty;
            Role = role ?? string.Empty;
            Message = message ?? string.Empty;
        }

        // frob:doc docs/platform-link.md#failure-modes
        public bool IsValid => Status == SessionStatus.Valid;

        // frob:doc docs/platform-link.md#failure-modes
        public override string ToString() => $"{Status} user_id='{UserId}' role='{Role}' ({Message})";
    }
}
