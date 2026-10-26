"""Tool settings resolved once: command line beats environment beats defaults."""

from __future__ import annotations

import logging
from pathlib import Path

from pydantic import BaseModel, Field

logger = logging.getLogger(__name__)

DEFAULT_PORT = 7979
DEFAULT_PLATFORM_URL = "http://127.0.0.1:8000"

URL_ENV = "HULLBREACH_PLATFORM_URL"
SERVER_KEY_ENV = "HULLBREACH_PLATFORM_SERVER_KEY"
TOKEN_ENV = "HULLBREACH_PLATFORM_TOKEN"
USERNAME_ENV = "HULLBREACH_PLATFORM_USERNAME"
PASSWORD_ENV = "HULLBREACH_PLATFORM_PASSWORD"
REQUIRE_AUTH_ENV = "HULLBREACH_REQUIRE_PLATFORM_AUTH"
ONLINE_SMOKE_ENV = "HULLBREACH_ONLINE_SMOKE"

# Environment variables whose values are secrets: never logged, printed or bundled.
SECRET_ENVS = (SERVER_KEY_ENV, TOKEN_ENV, PASSWORD_ENV)


class ToolConfig(BaseModel):
    """Settings shared by the doctor and the play commands."""

    repo_root: Path
    platform_url: str = DEFAULT_PLATFORM_URL
    port: int = Field(default=DEFAULT_PORT, ge=1, le=65535)
    builds_dir: Path

    @property
    def api(self) -> str:
        """The platform API prefix every endpoint hangs off."""
        return self.platform_url.rstrip("/") + "/api/v1"


def load_config(
    repo_root: Path,
    env: dict[str, str],
    platform_url: str | None = None,
    port: int | None = None,
) -> ToolConfig:
    """Build the ToolConfig from CLI values, then env, then defaults."""
    url = platform_url or env.get(URL_ENV) or DEFAULT_PLATFORM_URL
    config = ToolConfig(
        repo_root=repo_root,
        platform_url=url.rstrip("/"),
        port=port or DEFAULT_PORT,
        builds_dir=repo_root / "Builds",
    )
    logger.info(
        "config: root=%s platform=%s port=%s",
        repo_root,
        config.platform_url,
        config.port,
    )
    return config
