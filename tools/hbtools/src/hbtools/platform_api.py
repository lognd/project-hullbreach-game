"""The few platform API calls the doctor and play scripts make, with plain verdicts."""

from __future__ import annotations

import logging
from enum import Enum

from pydantic import BaseModel
from typani import Err, Ok, Result, propagate

from hbtools.errors import HttpError, PlatformError
from hbtools.net import Http, HttpReply

logger = logging.getLogger(__name__)

# The detail strings the platform uses on GET /auth/session (ticket ~48H1VAK).
KEY_MISSING = "server key missing"
KEY_INVALID = "invalid server key"
KEY_NOT_CONFIGURED = "server key not configured"
NOT_AUTHENTICATED = "not authenticated"

# A token no session can ever have; used to probe the key without a login.
PROBE_TOKEN = "hullbreach-doctor-probe-not-a-real-token"


class Health(BaseModel):
    """GET /health: the platform is up and which version it runs."""

    status: str
    version: str = "unknown"


class Login(BaseModel):
    """A successful sign-in: the bearer token and the account name."""

    token: str
    username: str

    def __repr__(self) -> str:
        """Never show the token, even in a debugger or a log line."""
        return f"Login(username={self.username!r}, token=<redacted>)"

    __str__ = __repr__


class KeyVerdict(str, Enum):
    """What GET /auth/session said about this game server's key."""

    ACCEPTED = "accepted"
    NOT_ENFORCED = "not_enforced"
    MISSING = "missing"
    INVALID = "invalid"
    NOT_CONFIGURED = "not_configured"
    UNKNOWN = "unknown"


def classify_session_reply(reply: HttpReply) -> KeyVerdict:
    """Map a /auth/session answer to a key verdict (the key is checked first)."""
    detail = reply.detail.lower()
    if reply.status == 503 and KEY_NOT_CONFIGURED in detail:
        return KeyVerdict.NOT_CONFIGURED
    if reply.status in (401, 403) and KEY_INVALID in detail:
        return KeyVerdict.INVALID
    if reply.status in (401, 403) and KEY_MISSING in detail:
        return KeyVerdict.MISSING
    if reply.status == 200 or (reply.status == 401 and NOT_AUTHENTICATED in detail):
        return KeyVerdict.ACCEPTED
    return KeyVerdict.UNKNOWN


def _platform_err(error: HttpError) -> PlatformError:
    return (
        PlatformError.Unexpected
        if error is HttpError.BadBody
        else (PlatformError.Unreachable)
    )


class PlatformApi:
    """A thin client for the platform's /api/v1 endpoints."""

    def __init__(self, http: Http, api_base: str, timeout: float = 5.0) -> None:
        """Bind to one platform, e.g. http://127.0.0.1:8000/api/v1."""
        self._http = http
        self._base = api_base.rstrip("/")
        self._timeout = timeout

    def _call(
        self,
        method: str,
        path: str,
        headers: dict[str, str] | None = None,
        payload: dict[str, str] | None = None,
    ) -> Result[HttpReply, PlatformError]:
        return self._http.request(
            method,
            self._base + path,
            headers=headers,
            payload=payload,
            timeout=self._timeout,
        ).map_err(_platform_err)

    @propagate
    def health(self) -> Result[Health, PlatformError]:
        """GET /health; Ok only for a 200 with a status field."""
        reply = self._call("GET", "/health").unwrap()
        if reply.status != 200 or not isinstance(reply.body, dict):
            return Err(PlatformError.Unexpected).note(f"/health -> {reply.status}")
        return Ok(Health.model_validate(reply.body))

    def ready(self) -> Result[HttpReply, PlatformError]:
        """GET /ready; any status is returned so the doctor can explain a 503."""
        return self._call("GET", "/ready")

    @propagate
    def login(self, username: str, password: str) -> Result[Login, PlatformError]:
        """POST /auth/login; the password is sent once and never kept or logged."""
        reply = self._call(
            "POST",
            "/auth/login",
            payload={"username": username, "password": password},
        ).unwrap()
        if reply.status == 401:
            return Err(PlatformError.BadCredentials)
        if reply.status == 429:
            return Err(PlatformError.RateLimited)
        body = reply.body if isinstance(reply.body, dict) else {}
        token = body.get("token")
        if reply.status != 200 or not isinstance(token, str):
            return Err(PlatformError.Unexpected).note(f"/auth/login -> {reply.status}")
        user = body.get("user") if isinstance(body.get("user"), dict) else {}
        name = str(user.get("username") or username)
        logger.info("signed in to the platform as %s", name)
        return Ok(Login(token=token, username=name))

    @propagate
    def register(
        self, username: str, email: str, password: str
    ) -> Result[str, PlatformError]:
        """POST /auth/register; Ok with the username (also when it already exists)."""
        reply = self._call(
            "POST",
            "/auth/register",
            payload={"username": username, "email": email, "password": password},
        ).unwrap()
        if reply.status in (200, 201, 409):
            logger.info("register %s -> %s", username, reply.status)
            return Ok(username)
        return Err(PlatformError.Refused).note(f"{reply.status}: {reply.detail}")

    def session(
        self, token: str, server_key: str | None
    ) -> Result[HttpReply, PlatformError]:
        """GET /auth/session exactly as the game server calls it."""
        headers = {"Authorization": f"Bearer {token}"}
        if server_key:
            headers["X-Server-Key"] = server_key
        return self._call("GET", "/auth/session", headers=headers)

    @propagate
    def key_verdict(
        self, server_key: str | None, token: str | None = None
    ) -> Result[KeyVerdict, PlatformError]:
        """Probe /auth/session to say whether this server key would be accepted."""
        # Without a key: a platform that enforces keys says "server key missing".
        bare = self.session(PROBE_TOKEN, None).unwrap()
        bare_verdict = classify_session_reply(bare)
        if bare_verdict is KeyVerdict.NOT_CONFIGURED:
            return Ok(KeyVerdict.NOT_CONFIGURED)
        if bare_verdict is not KeyVerdict.MISSING:
            logger.info("platform does not enforce server keys (%s)", bare.status)
            return Ok(KeyVerdict.NOT_ENFORCED)
        if not server_key:
            return Ok(KeyVerdict.MISSING)
        reply = self.session(token or PROBE_TOKEN, server_key).unwrap()
        verdict = classify_session_reply(reply)
        logger.info("server key verdict: %s (%s)", verdict.value, reply.status)
        return Ok(verdict)
