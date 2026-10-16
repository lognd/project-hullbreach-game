"""Every README link the doctor prints, or docs/doctor.md uses, must exist."""

from __future__ import annotations

import re
from pathlib import Path

import pytest

from hbtools import catalog

GAME_ROOT = Path(__file__).resolve().parents[4]
README = GAME_ROOT / "README.md"
DOCTOR_DOC = GAME_ROOT / "docs" / "doctor.md"


def _slug(heading: str) -> str:
    # GitHub's anchor rule: lower case, drop punctuation, spaces become dashes.
    return re.sub(r"[^\w\- ]", "", heading.strip().lower()).replace(" ", "-")


def _anchors(markdown: str) -> set[str]:
    found: set[str] = set()
    fenced = False
    for line in markdown.splitlines():
        if line.startswith("```"):
            fenced = not fenced
            continue
        match = None if fenced else re.match(r"#{1,6} (.+)$", line)
        if match:
            found.add(_slug(match.group(1)))
    return found


README_ANCHORS = _anchors(README.read_text(encoding="utf-8"))
DOC_LINKS = sorted(
    value.removeprefix(catalog.README)
    for name, value in vars(catalog).items()
    if name.startswith("DOC_") and isinstance(value, str)
)


@pytest.mark.parametrize("anchor", DOC_LINKS)
def test_catalog_anchor_exists(anchor: str) -> None:
    assert anchor in README_ANCHORS


def test_every_check_links_a_real_readme_section() -> None:
    for check in catalog.CATALOG:
        anchor = check.doc.removeprefix(catalog.README)
        assert anchor in README_ANCHORS, check.id


def test_readme_links_in_the_reference_page_exist() -> None:
    links = re.findall(r"\(\.\./README\.md#([^)]+)\)", DOCTOR_DOC.read_text("utf-8"))
    assert links
    assert sorted(set(links) - README_ANCHORS) == []


def test_readme_internal_links_exist() -> None:
    links = re.findall(r"\]\(#([^)]+)\)", README.read_text(encoding="utf-8"))
    assert sorted(set(links) - README_ANCHORS) == []
