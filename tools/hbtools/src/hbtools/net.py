"""HTTP, UDP port and LAN address helpers behind protocols that tests can fake."""

from __future__ import annotations

import ipaddress
import json
import logging
import socket
import urllib.error
import urllib.parse
import urllib.request
from typing import Any, Protocol

from pydantic import BaseModel
from typani import Err, Ok, Result

from hbtools.errors import HttpError

logger = logging.getLogger(__name__)

# Headers whose values must never reach a log line.
SECRET_HEADERS = frozenset({"authorization", "x-server-key"})


class HttpReply(BaseModel):
    """An HTTP answer of any status; the body is parsed JSON when it was JSON."""

    status: int
    body: Any = None
    text: str = ""
    headers: dict[str, str] = {}

    @property
    def detail(self) -> str:
        """FastAPI's error message ({"detail": "..."}), or the raw text."""
        if isinstance(self.body, dict) and isinstance(self.body.get("detail"), str):
            return self.body["detail"]
        return self.text.strip()[:200]


class Http(Protocol):
    """The tiny HTTP client the doctor needs."""

    def request(
        self,
        method: str,
        url: str,
        *,
        headers: dict[str, str] | None = None,
        payload: dict[str, Any] | None = None,
        timeout: float = 5.0,
    ) -> Result[HttpReply, HttpError]:
        """Ok with any status code; Err only when nothing usable came back."""
        ...


def safe_headers(headers: dict[str, str] | None) -> dict[str, str]:
    """Headers with secret values replaced, for logging."""
    return {
        k: ("<redacted>" if k.lower() in SECRET_HEADERS else v)
        for k, v in (headers or {}).items()
    }


class UrllibHttp:
    """Http over the standard library, so the tools need no extra packages."""

    def request(
        self,
        method: str,
        url: str,
        *,
        headers: dict[str, str] | None = None,
        payload: dict[str, Any] | None = None,
        timeout: float = 5.0,
    ) -> Result[HttpReply, HttpError]:
        """Ok with any status code; Err only when nothing usable came back."""
        parsed = urllib.parse.urlparse(url)
        if parsed.scheme not in ("http", "https") or not parsed.netloc:
            return Err(HttpError.BadUrl).note(url)
        data = None
        all_headers = {"Accept": "application/json", **(headers or {})}
        if payload is not None:
            data = json.dumps(payload).encode("utf-8")
            all_headers["Content-Type"] = "application/json"
        logger.debug("%s %s headers=%s", method, url, safe_headers(all_headers))
        req = urllib.request.Request(url, data=data, method=method, headers=all_headers)
        try:
            with urllib.request.urlopen(req, timeout=timeout) as resp:
                return Ok(_reply(resp.status, resp.read(), dict(resp.headers)))
        except urllib.error.HTTPError as exc:
            return Ok(_reply(exc.code, exc.read(), dict(exc.headers or {})))
        except TimeoutError:
            logger.info("timeout: %s %s", method, url)
            return Err(HttpError.TimedOut).note(url)
        except (urllib.error.URLError, OSError) as exc:
            logger.info("unreachable: %s %s (%s)", method, url, exc)
            return Err(HttpError.Unreachable).note(f"{url}: {exc}")


def _reply(status: int, raw: bytes, headers: dict[str, str]) -> HttpReply:
    text = raw.decode("utf-8", errors="replace")
    try:
        body = json.loads(text) if text.strip() else None
    except ValueError:
        body = None
    logger.debug("-> %s", status)
    return HttpReply(status=status, body=body, text=text, headers=headers)


class Sockets(Protocol):
    """The socket facts the doctor needs: is a UDP port free, what are my IPs."""

    def udp_port_free(self, port: int) -> bool:
        """True when nothing on this computer is using the UDP port."""
        ...

    def local_addresses(self) -> list[str]:
        """IPv4 addresses of this computer (no loopback)."""
        ...


class RealSockets:
    """Sockets that really bind and look up addresses."""

    def udp_port_free(self, port: int) -> bool:
        """True when nothing on this computer is using the UDP port."""
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        try:
            sock.bind(("0.0.0.0", port))
        except OSError as exc:
            logger.info("udp %s busy: %s", port, exc)
            return False
        finally:
            sock.close()
        return True

    def local_addresses(self) -> list[str]:
        """IPv4 addresses of this computer (no loopback)."""
        found: list[str] = []
        # Connecting a UDP socket sends nothing but picks the outgoing interface.
        probe = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        try:
            probe.connect(("192.0.2.1", 9))
            found.append(probe.getsockname()[0])
        except OSError as exc:
            logger.info("no default route: %s", exc)
        finally:
            probe.close()
        try:
            infos = socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET)
        except OSError as exc:
            logger.info("hostname lookup failed: %s", exc)
            infos = []
        for info in infos:
            addr = str(info[4][0])
            if addr not in found:
                found.append(addr)
        return [a for a in found if not a.startswith("127.")]


def is_private(address: str) -> bool:
    """True for home/office network addresses (192.168.x, 10.x, 172.16-31.x)."""
    try:
        ip = ipaddress.IPv4Address(address)
    except ValueError:
        return False
    return ip.is_private and not ip.is_loopback and not ip.is_link_local


def best_lan_address(addresses: list[str]) -> str | None:
    """The address to give a friend on the same Wi-Fi: prefer 192.168, then 10, 172."""
    private = [a for a in addresses if is_private(a)]
    for prefix in ("192.168.", "10.", "172."):
        for a in private:
            if a.startswith(prefix):
                return a
    return private[0] if private else None


def is_loopback_url(url: str) -> bool:
    """True when a URL points at this same computer (127.x or localhost)."""
    host = (urllib.parse.urlparse(url).hostname or "").lower()
    return host == "localhost" or host.startswith("127.") or host == "::1"
