"""The platform client and the server-key diagnosis (old and new platforms)."""

from __future__ import annotations

import pytest
from typani import Err, Ok

from hbtools.errors import HttpError, PlatformError
from hbtools.platform_api import (
    PROBE_TOKEN,
    KeyVerdict,
    Login,
    PlatformApi,
    classify_session_reply,
)

from .conftest import FakeHttp, reply

API = "http://127.0.0.1:8000/api/v1"
SESSION = API + "/auth/session"
KEY = "placeholder-server-key"


@pytest.mark.parametrize(
    ("status", "detail", "verdict"),
    [
        (503, "server key not configured", KeyVerdict.NOT_CONFIGURED),
        (401, "server key missing", KeyVerdict.MISSING),
        (401, "invalid server key", KeyVerdict.INVALID),
        (403, "Invalid server key", KeyVerdict.INVALID),
        (401, "not authenticated", KeyVerdict.ACCEPTED),
        (500, "boom", KeyVerdict.UNKNOWN),
    ],
)
def test_classify_session_reply(status: int, detail: str, verdict: KeyVerdict) -> None:
    assert classify_session_reply(reply(status, {"detail": detail})) is verdict


def test_classify_ok_reply_is_accepted() -> None:
    assert (
        classify_session_reply(reply(200, {"user_id": "u1", "role": "player"}))
        is KeyVerdict.ACCEPTED
    )


def _platform(enforced: str | None, keys: tuple[str, ...] = (KEY,)) -> FakeHttp:
    """A platform: None = old (no key check), 'none' = no keys configured, 'on'."""

    def handle(method: str, url: str, headers: dict[str, str]):
        assert url == SESSION
        if enforced is None:
            return reply(401, {"detail": "not authenticated"})
        if enforced == "none":
            return reply(503, {"detail": "server key not configured"})
        given = headers.get("X-Server-Key")
        if not given:
            return reply(401, {"detail": "server key missing"})
        if given not in keys:
            return reply(401, {"detail": "invalid server key"})
        if headers.get("Authorization") == "Bearer good-token":
            return reply(200, {"user_id": "u1", "role": "player"})
        return reply(401, {"detail": "not authenticated"})

    return FakeHttp(handler=handle)


@pytest.mark.parametrize(
    ("enforced", "key", "verdict"),
    [
        (None, None, KeyVerdict.NOT_ENFORCED),
        (None, KEY, KeyVerdict.NOT_ENFORCED),
        ("none", KEY, KeyVerdict.NOT_CONFIGURED),
        ("on", None, KeyVerdict.MISSING),
        ("on", "wrong-placeholder", KeyVerdict.INVALID),
        ("on", KEY, KeyVerdict.ACCEPTED),
    ],
)
def test_key_verdict(
    enforced: str | None, key: str | None, verdict: KeyVerdict
) -> None:
    api = PlatformApi(_platform(enforced), API)
    assert api.key_verdict(key) == Ok(verdict)


def test_key_verdict_uses_real_token_and_never_sends_key_on_the_bare_probe() -> None:
    http = _platform("on")
    api = PlatformApi(http, API)
    assert api.key_verdict(KEY, "good-token") == Ok(KeyVerdict.ACCEPTED)
    bare, keyed = http.calls
    assert "X-Server-Key" not in bare[2]
    assert bare[2]["Authorization"] == f"Bearer {PROBE_TOKEN}"
    assert keyed[2] == {"Authorization": "Bearer good-token", "X-Server-Key": KEY}


def test_key_verdict_unreachable() -> None:
    api = PlatformApi(FakeHttp(), API)
    assert api.key_verdict(KEY) == Err(PlatformError.Unreachable)


def test_login_statuses() -> None:
    url = API + "/auth/login"
    ok = FakeHttp(
        routes={
            ("POST", url): reply(200, {"token": "t0k3n", "user": {"username": "ann"}})
        }
    )
    assert PlatformApi(ok, API).login("ann", "pw").unwrap().username == "ann"
    bad = FakeHttp(routes={("POST", url): reply(401, {"detail": "bad"})})
    assert PlatformApi(bad, API).login("ann", "pw").is_err
    match PlatformApi(bad, API).login("ann", "pw"):
        case Err(PlatformError.BadCredentials):
            pass
        case other:
            raise AssertionError(other)
    limited = FakeHttp(routes={("POST", url): reply(429, {"detail": "slow"})})
    match PlatformApi(limited, API).login("ann", "pw"):
        case Err(PlatformError.RateLimited):
            pass
        case other:
            raise AssertionError(other)


def test_register_accepts_existing_user() -> None:
    url = API + "/auth/register"
    for status in (201, 409):
        http = FakeHttp(routes={("POST", url): reply(status, {"detail": "x"})})
        assert PlatformApi(http, API).register("ann", "a@example.com", "pw") == Ok(
            "ann"
        )


def test_health_reads_version() -> None:
    http = FakeHttp(
        routes={
            ("GET", API + "/health"): reply(200, {"status": "ok", "version": "1.2"})
        }
    )
    assert PlatformApi(http, API).health().unwrap().version == "1.2"
    down = PlatformApi(
        FakeHttp(routes={("GET", API + "/health"): HttpError.TimedOut}), API
    )
    assert down.health().is_err


def test_login_never_shows_its_token() -> None:
    login = Login(token="super-secret-token", username="ann")
    assert "super-secret-token" not in repr(login)
    assert "super-secret-token" not in str(login)
