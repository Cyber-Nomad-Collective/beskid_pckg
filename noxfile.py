"""Nox tasks for the Beskid package manager (pckg) repository."""

from __future__ import annotations

from pathlib import Path

import nox

ROOT = Path(__file__).resolve().parent
TESTS = ROOT / "src" / "Server.Tests"


@nox.session(python=False, name="unit_tests")
def unit_tests(session: nox.Session) -> None:
    if not TESTS.is_dir():
        raise SystemExit(f"Missing test project: {TESTS}")
    with session.chdir(str(TESTS)):
        session.run(
            "dotnet",
            "test",
            "--filter",
            "FullyQualifiedName~Server.Tests.Unit",
            "--configuration",
            "Release",
            external=True,
        )
