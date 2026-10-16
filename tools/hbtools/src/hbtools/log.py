"""Logging setup: a rotating file under Logs/hbtools so --collect can bundle it."""

from __future__ import annotations

import logging
import logging.config
from pathlib import Path

LOG_FILE_NAME = "hbtools.log"


def log_dir(repo_root: Path) -> Path:
    """Where the tools write their own log (gitignored with the rest of Logs/)."""
    return repo_root / "Logs" / "hbtools"


def configure(repo_root: Path, verbose: bool = False) -> Path:
    """Send hbtools logs to a file; the console belongs to the reporter."""
    directory = log_dir(repo_root)
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / LOG_FILE_NAME
    handlers: dict[str, dict[str, object]] = {
        "file": {
            "class": "logging.handlers.RotatingFileHandler",
            "filename": str(path),
            "maxBytes": 1_000_000,
            "backupCount": 3,
            "encoding": "utf-8",
            "formatter": "plain",
        }
    }
    root_handlers = ["file"]
    if verbose:
        handlers["console"] = {
            "class": "logging.StreamHandler",
            "stream": "ext://sys.stderr",
            "formatter": "plain",
        }
        root_handlers.append("console")
    logging.config.dictConfig(
        {
            "version": 1,
            "disable_existing_loggers": False,
            "formatters": {
                "plain": {"format": "%(asctime)s %(levelname)s %(name)s: %(message)s"}
            },
            "handlers": handlers,
            "loggers": {"hbtools": {"level": "DEBUG", "handlers": root_handlers}},
        }
    )
    logging.getLogger(__name__).info("logging to %s", path)
    return path
