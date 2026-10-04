#!/usr/bin/env python3
"""Record nontrivial product edits, then request one Documancer follow-up.

afterFileEdit: python .cursor/hooks/ensure-docs.py record
stop:          python .cursor/hooks/ensure-docs.py
"""

from __future__ import annotations

import json
import os
import sys
import tempfile
from pathlib import Path

TECHNICAL_JSON = ".userdocs/technical.json"
USER_JSON = ".userdocs/user-guide.json"
SKILL = ".cursor/skills/documancer/SKILL.md"
MAX_LISTED_PATHS = 20
MAX_SCAN_CHARS = 500_000

USER_PREFIX = "src/JeffyGPT/JeffyGPT.Web/"
USER_EXTENSIONS = {".ts", ".tsx", ".css", ".html", ".svg"}
SKIP_DIRECTORIES = {
    "bin",
    "obj",
    "node_modules",
    "coverage",
    "artifacts",
    "dist",
}
SKIP_PREFIXES = (
    ".roots/",
    ".userdocs/",
    ".cursor/",
    ".git/",
    ".github/",
    "scripts/",
)
TEST_SUFFIXES = (".test.ts", ".test.tsx", ".spec.ts", ".spec.tsx")


def emit(payload: dict) -> None:
    sys.stdout.write(json.dumps(payload) + "\n")
    sys.stdout.flush()


def load_payload() -> dict:
    raw = sys.stdin.read()
    if not raw.strip():
        return {}
    try:
        payload = json.loads(raw)
    except json.JSONDecodeError:
        return {}
    return payload if isinstance(payload, dict) else {}


def state_path(conversation_id: str) -> Path:
    override = os.environ.get("JEFFYGPT_DOC_HOOK_DIR")
    if override:
        directory = Path(override)
    else:
        base = os.environ.get("LOCALAPPDATA") or tempfile.gettempdir()
        directory = Path(base) / "JeffyGPT" / "doc-hook"
    safe = "".join(ch if ch.isalnum() or ch in "-_" else "_" for ch in conversation_id)[:80]
    return directory / f"{safe or 'default'}.log"


def repo_relative(file_path: str, workspace_roots: list) -> str:
    normalized = os.path.normpath(file_path)
    roots = [os.path.normpath(root) for root in workspace_roots if root]
    roots.sort(key=len, reverse=True)
    folded = normalized.casefold()
    for root in roots:
        root_folded = root.casefold()
        if folded == root_folded:
            return ""
        prefix = root_folded + os.sep
        if folded.startswith(prefix):
            return os.path.relpath(normalized, root).replace("\\", "/")
    return file_path.replace("\\", "/")


def is_test_path(relative_path: str) -> bool:
    name = relative_path.rsplit("/", 1)[-1]
    if name.endswith(TEST_SUFFIXES) or name.endswith("Tests.cs"):
        return True
    folded = relative_path.casefold()
    return "/tests/" in f"/{folded}" or ".tests/" in folded


def is_ignored_path(relative_path: str) -> bool:
    if not relative_path or relative_path.startswith(SKIP_PREFIXES):
        return True
    if any(part in SKIP_DIRECTORIES for part in relative_path.split("/")):
        return True
    return is_test_path(relative_path)


def documentation_audience(relative_path: str) -> str | None:
    """Return 'user', 'technical', or None for paths that should not trigger docs."""
    if is_ignored_path(relative_path):
        return None
    if relative_path.startswith(USER_PREFIX):
        rest = relative_path[len(USER_PREFIX) :]
        extension = os.path.splitext(rest)[1].lower()
        if rest.startswith("src/") and extension in USER_EXTENSIONS:
            return "user"
        if rest == "index.html" or rest.startswith("public/"):
            return "user"
        return None
    if not relative_path.startswith("src/"):
        return None
    name = relative_path.rsplit("/", 1)[-1]
    extension = os.path.splitext(name)[1].lower()
    if extension == ".cs":
        return "technical"
    if extension == ".csproj":
        return "technical"
    folded = name.casefold()
    if folded.startswith("appsettings") and folded.endswith(".json"):
        return "technical"
    if folded == "launchsettings.json":
        return "technical"
    return None


def _scan_code(text: str) -> str:
    """Drop whitespace and comments while keeping quoted text."""
    out: list[str] = []
    index = 0
    length = len(text)
    while index < length:
        char = text[index]
        if char in " \t\r\n":
            index += 1
            continue
        if char in "\"'":
            quote = char
            out.append(char)
            index += 1
            while index < length:
                current = text[index]
                out.append(current)
                index += 1
                if current == "\\":
                    if index < length:
                        out.append(text[index])
                        index += 1
                    continue
                if current == quote:
                    break
            continue
        if char == "/" and index + 1 < length and text[index + 1] == "/":
            index += 2
            while index < length and text[index] not in "\r\n":
                index += 1
            continue
        if char == "/" and index + 1 < length and text[index + 1] == "*":
            index += 2
            while index + 1 < length and not (text[index] == "*" and text[index + 1] == "/"):
                index += 1
            index = min(length, index + 2)
            continue
        out.append(char)
        index += 1
    return "".join(out)


def changes_behavior(old: str, new: str, relative_path: str) -> bool:
    if len(old) > MAX_SCAN_CHARS or len(new) > MAX_SCAN_CHARS:
        return True
    if "".join(old.split()) == "".join(new.split()):
        return False
    extension = os.path.splitext(relative_path)[1].lower()
    if extension not in {".cs", ".ts", ".tsx", ".css"}:
        return True
    return _scan_code(old) != _scan_code(new)


def edit_changes_behavior(payload: dict, relative_path: str) -> bool:
    edits = payload.get("edits")
    if not isinstance(edits, list) or not edits:
        return True
    for edit in edits:
        if not isinstance(edit, dict):
            return True
        old = edit.get("old_string", "")
        new = edit.get("new_string", "")
        if not isinstance(old, str) or not isinstance(new, str):
            return True
        if changes_behavior(old, new, relative_path):
            return True
    return False


def append_path(conversation_id: str, relative_path: str) -> None:
    path = state_path(conversation_id)
    path.parent.mkdir(parents=True, exist_ok=True)
    line = (relative_path + "\n").encode("utf-8")
    fd = os.open(path, os.O_APPEND | os.O_CREAT | os.O_WRONLY)
    try:
        os.write(fd, line)
    finally:
        os.close(fd)


def consume_paths(conversation_id: str) -> list[str]:
    path = state_path(conversation_id)
    if not path.exists():
        return []
    try:
        text = path.read_text(encoding="utf-8")
    except OSError:
        return []
    try:
        path.unlink()
    except OSError:
        pass
    return [line.strip() for line in text.splitlines() if line.strip()]


def record(payload: dict) -> None:
    file_path = payload.get("file_path")
    if not isinstance(file_path, str) or not file_path:
        return
    roots = payload.get("workspace_roots")
    if not isinstance(roots, list):
        roots = [os.getcwd()]
    relative_path = repo_relative(file_path, roots)
    if documentation_audience(relative_path) is None:
        return
    if not edit_changes_behavior(payload, relative_path):
        return
    conversation_id = payload.get("conversation_id")
    if not isinstance(conversation_id, str) or not conversation_id:
        conversation_id = "default"
    append_path(conversation_id, relative_path)


def followup_message(paths: list[str]) -> str:
    audiences = {documentation_audience(path) for path in paths}
    audiences.discard(None)
    unique_paths = sorted(set(paths))
    listed = unique_paths[:MAX_LISTED_PATHS]
    lines = "\n".join(f"- {path}" for path in listed)
    remaining = len(unique_paths) - len(listed)
    if remaining:
        lines += f"\n- +{remaining} more"

    if audiences == {"technical"}:
        target = (
            f"Update the technical reference at {TECHNICAL_JSON} and render technical.html beside it. "
            "Read .cursor/skills/documancer/references/technical.md. "
            "Document the code behavior these files changed."
        )
    elif audiences == {"user"}:
        target = (
            f"Update the user guide at {USER_JSON} and render user-guide.html beside it. "
            "Read .cursor/skills/documancer/references/user-guide.md. "
            "Document the user-visible behavior these files changed."
        )
    else:
        target = (
            f"Update {TECHNICAL_JSON} and render technical.html beside it. "
            f"Update {USER_JSON} and render user-guide.html beside it. "
            "Read .cursor/skills/documancer/references/technical.md and references/user-guide.md. "
            "Do not add UAT."
        )
    return (
        "Product behavior may have changed. "
        f"Read {SKILL}. "
        "If these edits do not change behavior, reply in one sentence and stop. "
        f"{target} "
        "Edit the existing JSON in place and re-render the HTML next to it. "
        "Leave other markdown under .roots/docs unchanged. "
        "Do not launch documancer subagents unless the change spans multiple subsystems.\n"
        f"Changed files:\n{lines}"
    )


def stop(payload: dict) -> dict:
    conversation_id = payload.get("conversation_id")
    if not isinstance(conversation_id, str) or not conversation_id:
        conversation_id = "default"
    paths = consume_paths(conversation_id)
    status = payload.get("status")
    loop_count = payload.get("loop_count", 0)
    if status != "completed" or not isinstance(loop_count, int) or loop_count > 0:
        return {}
    relevant = [path for path in paths if documentation_audience(path) is not None]
    if not relevant:
        return {}
    return {"followup_message": followup_message(relevant)}


def self_check() -> int:
    failures: list[str] = []

    def expect(condition: bool, message: str) -> None:
        if not condition:
            failures.append(message)

    expect(documentation_audience("src/JeffyGPT/JeffyGPT.Api/Program.cs") == "technical", "api cs")
    expect(
        documentation_audience("src/JeffyGPT/JeffyGPT.Web/src/App.tsx") == "user",
        "web tsx",
    )
    expect(documentation_audience("src/JeffyGPT/JeffyGPT.Web/src/api/chatClient.test.ts") is None, "web test")
    expect(documentation_audience("tests/JeffyGPT/JeffyGPT.Tests/ChatTests.cs") is None, "test project")
    expect(documentation_audience("src/JeffyGPT/JeffyGPT.Api/bin/Debug/App.dll") is None, "bin")
    expect(documentation_audience(".roots/docs/development.md") is None, "markdown docs")
    expect(documentation_audience("src/JeffyGPT/JeffyGPT.Api/appsettings.json") == "technical", "appsettings")
    expect(documentation_audience("src/JeffyGPT/JeffyGPT.Web/scripts/crap-score.mjs") is None, "web script")
    expect(
        not changes_behavior("public void Run()\n{\n}\n", "public void Run() {\n}\n", "Program.cs"),
        "whitespace",
    )
    expect(
        not changes_behavior("int x = 1; // note\n", "int x = 1;\n", "Program.cs"),
        "comment",
    )
    expect(changes_behavior("int x = 1;\n", "int x = 2;\n", "Program.cs"), "value change")
    expect(
        changes_behavior('var url = "https://a";\n', 'var url = "https://b";\n', "Client.cs"),
        "url string",
    )

    with tempfile.TemporaryDirectory() as directory:
        os.environ["JEFFYGPT_DOC_HOOK_DIR"] = directory
        record(
            {
                "conversation_id": "conv-1",
                "workspace_roots": ["C:/repos/JeffyGPT"],
                "file_path": "C:/repos/JeffyGPT/src/JeffyGPT/JeffyGPT.Api/Program.cs",
                "edits": [{"old_string": "return 1;", "new_string": "return 2;"}],
            }
        )
        record(
            {
                "conversation_id": "conv-1",
                "workspace_roots": ["C:/repos/JeffyGPT"],
                "file_path": "C:/repos/JeffyGPT/src/JeffyGPT/JeffyGPT.Web/src/App.tsx",
                "edits": [{"old_string": "Send", "new_string": "Submit"}],
            }
        )
        record(
            {
                "conversation_id": "conv-1",
                "workspace_roots": ["C:/repos/JeffyGPT"],
                "file_path": "C:/repos/JeffyGPT/.roots/docs/development.md",
                "edits": [{"old_string": "a", "new_string": "b"}],
            }
        )
        completed = stop({"conversation_id": "conv-1", "status": "completed", "loop_count": 0})
        message = completed.get("followup_message", "")
        expect("technical.json" in message and "user-guide.json" in message, "both audiences")
        expect("development.md" not in message, "docs path excluded")
        expect(stop({"conversation_id": "conv-1", "status": "completed", "loop_count": 0}) == {}, "log consumed")

        record(
            {
                "conversation_id": "conv-2",
                "file_path": "src/JeffyGPT/JeffyGPT.Web/src/App.tsx",
                "edits": [{"old_string": "Send", "new_string": "Submit"}],
            }
        )
        user_only = stop({"conversation_id": "conv-2", "status": "completed", "loop_count": 0})
        user_message = user_only.get("followup_message", "")
        expect("user-guide.json" in user_message and "technical.json" not in user_message, "user only")
        expect(stop({"conversation_id": "conv-2", "status": "aborted", "loop_count": 0}) == {}, "abort")
        expect(
            stop({"conversation_id": "unused", "status": "completed", "loop_count": 1}) == {},
            "loop limit",
        )

    if failures:
        sys.stderr.write("\n".join(failures) + "\n")
        return 1
    sys.stdout.write("ok\n")
    return 0


def main() -> int:
    command = sys.argv[1] if len(sys.argv) > 1 else "stop"
    if command == "self-check":
        return self_check()
    try:
        payload = load_payload()
        if command == "record":
            record(payload)
            emit({})
        else:
            emit(stop(payload))
    except Exception:
        emit({})
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
