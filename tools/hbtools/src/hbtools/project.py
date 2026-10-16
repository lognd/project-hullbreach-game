"""Facts read straight from the Unity project files, without starting Unity."""

from __future__ import annotations

import logging
import re
from pathlib import Path

from pydantic import BaseModel
from typani import Err, Ok, Result, propagate

from hbtools.errors import ProjectError

logger = logging.getLogger(__name__)

LOBBY_SCENE = "Assets/Scenes/LobbyScene.unity"
GAME_SCENE = "Assets/Scenes/MultiplayerGame.unity"
SIGNIN_PREFAB = "Assets/Prefabs/UI/SignInCanvas.prefab"
LOCK_FILE = "Temp/UnityLockfile"


class ProjectFacts(BaseModel):
    """What the doctor needs to know about the checked-out Unity project."""

    unity_version: str
    company: str
    product: str
    cloud_project_id: str
    insecure_http: int
    scenes: list[str]
    signin_prefab_present: bool
    signin_in_lobby: bool
    uses_lfs: bool


def find_repo_root(start: Path) -> Path | None:
    """Walk up from start to the folder holding ProjectSettings/ProjectVersion.txt."""
    for folder in [start, *start.parents]:
        if (folder / "ProjectSettings" / "ProjectVersion.txt").is_file():
            return folder
    return None


def _read(path: Path) -> Result[str, ProjectError]:
    if not path.is_file():
        return Err(ProjectError.Missing).note(str(path))
    return Result.catch(
        lambda: path.read_text(encoding="utf-8", errors="replace"),
        OSError,
        on_error=lambda _exc: ProjectError.Unreadable,
    ).note(str(path))


def _field(text: str, name: str) -> str:
    match = re.search(rf"^\s*{re.escape(name)}:[ \t]*(.*)$", text, re.MULTILINE)
    return match.group(1).strip() if match else ""


def enabled_scenes(build_settings: str) -> list[str]:
    """Enabled scene paths from EditorBuildSettings.asset, in build order."""
    found = re.findall(
        r"-\s+enabled:\s*(\d)\s*\n\s*path:\s*(\S+)", build_settings, re.MULTILINE
    )
    return [path for enabled, path in found if enabled == "1"]


@propagate
def read_project(root: Path) -> Result[ProjectFacts, ProjectError]:
    """Parse ProjectVersion, ProjectSettings and the lobby scene into facts."""
    settings_dir = root / "ProjectSettings"
    version_text = _read(settings_dir / "ProjectVersion.txt").unwrap()
    settings = _read(settings_dir / "ProjectSettings.asset").unwrap()
    build = _read(settings_dir / "EditorBuildSettings.asset").unwrap()
    version = _field(version_text, "m_EditorVersion")
    if not version:
        return Err(ProjectError.Malformed).note("ProjectVersion.txt has no version")
    prefab_meta = root / (SIGNIN_PREFAB + ".meta")
    guid = _field(_read(prefab_meta).unwrap_or(""), "guid")
    lobby = _read(root / LOBBY_SCENE).unwrap_or("")
    attributes = _read(root / ".gitattributes").unwrap_or("")
    insecure = _field(settings, "insecureHttpOption")
    facts = ProjectFacts(
        unity_version=version,
        company=_field(settings, "companyName"),
        product=_field(settings, "productName"),
        cloud_project_id=_field(settings, "cloudProjectId"),
        insecure_http=int(insecure) if insecure.isdigit() else 0,
        scenes=enabled_scenes(build),
        signin_prefab_present=(root / SIGNIN_PREFAB).is_file(),
        signin_in_lobby=bool(guid) and guid in lobby,
        uses_lfs="filter=lfs" in attributes,
    )
    logger.info(
        "project: unity %s, scenes %s, signin in lobby %s",
        facts.unity_version,
        facts.scenes,
        facts.signin_in_lobby,
    )
    return Ok(facts)


def editor_has_project_open(root: Path) -> bool:
    """Unity holds Temp/UnityLockfile while the project is open in an editor."""
    return (root / LOCK_FILE).exists()
