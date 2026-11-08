import os
import sys
import time
import subprocess
import fnmatch
from pathlib import Path
from typing import Dict, Any, List, Optional, Tuple

# Sensitive file patterns that should not be read or altered by untrusted agent calls
SENSITIVE_PATTERNS = [
    ".env*",
    "*.pem",
    "*.key",
    "id_rsa*",
    "id_ed25519*",
    "*.pfx",
    "*.p12",
    "*credentials*.json",
    "*secret*.json",
    "*token*.json",
    "authorized_keys"
]

# High-risk system directories that must never be targeted
FORBIDDEN_PARENT_DIRS = [
    "c:\\windows",
    "c:\\program files",
    "c:\\program files (x86)",
    "c:\\users\\default",
    "/etc",
    "/var",
    "/usr",
    "/bin",
    "/sbin"
]

# Commands with destructive or system-altering potential
DANGEROUS_COMMAND_SUBSTRINGS = [
    "format ",
    "rmdir /s /q c:\\",
    "rm -rf /",
    "reg add",
    "reg delete",
    "net user",
    "shutdown",
    "diskpart",
    "mkfs",
    "dd if="
]


class SandboxViolationError(PermissionError):
    """Raised when an operation violates workspace sandbox boundaries."""
    pass


class WorkspaceSandbox:
    """
    Enforces path boundary containment, sensitive secret shielding, command execution 
    sanitization, and audit logging to SQLite, similar to modern agentic harnesses.
    """
    def __init__(self, workspace_path: str, policy: str = "STRICT", store: Any = None, session_id: str = "default"):
        self.workspace = Path(workspace_path).resolve()
        self.policy = policy.upper()  # STRICT, MONITORED, PERMISSIVE
        self.store = store
        self.session_id = session_id
        self._init_audit_table()

    def _init_audit_table(self):
        if not self.store:
            return
        try:
            with self.store.connect(True) as db:
                db.execute("""
                    CREATE TABLE IF NOT EXISTS audit_events (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        timestamp REAL,
                        session_id TEXT,
                        action_type TEXT,
                        target TEXT,
                        allowed INTEGER,
                        reason TEXT,
                        severity TEXT
                    )
                """)
        except Exception:
            pass

    def log_audit(self, action_type: str, target: str, allowed: bool, reason: str = "", severity: str = "INFO"):
        if not self.store:
            return
        try:
            with self.store.connect(True) as db:
                db.execute(
                    "INSERT INTO audit_events (timestamp, session_id, action_type, target, allowed, reason, severity) VALUES (?,?,?,?,?,?,?)",
                    (time.time(), self.session_id, action_type, target[:500], 1 if allowed else 0, reason, severity)
                )
        except Exception:
            pass

    def check_path_access(self, rel_or_abs_path: str, is_write: bool = False) -> Path:
        """
        Ensures target path strictly resides within the designated workspace boundary.
        Prevents path traversal attacks ('../'), symlink escapes, and access to sensitive secrets.
        """
        raw_target = (self.workspace / rel_or_abs_path).resolve()

        # Check path containment
        try:
            raw_target.relative_to(self.workspace)
        except ValueError:
            msg = f"Path escape blocked: '{rel_or_abs_path}' resolves outside workspace boundary '{self.workspace}'"
            self.log_audit("FILE_ACCESS", str(rel_or_abs_path), False, msg, "CRITICAL")
            if self.policy == "STRICT":
                raise SandboxViolationError(msg)

        # Check for system-critical paths
        target_str = str(raw_target).lower()
        for forbidden in FORBIDDEN_PARENT_DIRS:
            if target_str.startswith(forbidden):
                msg = f"Access to system-critical directory '{forbidden}' is strictly forbidden"
                self.log_audit("SYSTEM_PATH_ACCESS", str(rel_or_abs_path), False, msg, "CRITICAL")
                raise SandboxViolationError(msg)

        # Check for sensitive secret patterns
        file_name = raw_target.name.lower()
        for pat in SENSITIVE_PATTERNS:
            if fnmatch.fnmatch(file_name, pat.lower()):
                msg = f"Access to sensitive credential or secret file '{file_name}' is blocked by sandbox policy"
                self.log_audit("SECRET_ACCESS", str(rel_or_abs_path), False, msg, "HIGH")
                if self.policy in ("STRICT", "MONITORED"):
                    raise SandboxViolationError(msg)

        # Runtime metadata and repository internals isolation
        parts = {p.lower() for p in raw_target.relative_to(self.workspace).parts}
        if is_write and parts.intersection({".git", "iba_runtime", "iba_memory"}):
            msg = "Repository internals and IBA database files are protected and cannot be directly overwritten"
            self.log_audit("PROTECTED_INTERNAL_WRITE", str(rel_or_abs_path), False, msg, "HIGH")
            raise SandboxViolationError(msg)

        self.log_audit("FILE_ACCESS", str(raw_target), True, "Path authorized within sandbox boundary", "INFO")
        return raw_target

    def sanitize_command(self, cmd_or_argv: Any) -> Tuple[List[str], Dict[str, str]]:
        """
        Validates command safety, scrubs sensitive environment variables, and enforces workspace cwd.
        """
        cmd_str = " ".join(cmd_or_argv) if isinstance(cmd_or_argv, list) else str(cmd_or_argv)
        lower_cmd = cmd_str.lower()

        # Check for dangerous command substrings
        for bad in DANGEROUS_COMMAND_SUBSTRINGS:
            if bad in lower_cmd:
                msg = f"Command rejected: dangerous operation detected matching '{bad}'"
                self.log_audit("COMMAND_EXEC", cmd_str, False, msg, "CRITICAL")
                raise SandboxViolationError(msg)

        # Scrub sensitive environment variables
        scrubbed_env = dict(os.environ)
        sensitive_env_keys = ["API_KEY", "TOKEN", "SECRET", "PASSWORD", "AUTH", "CREDENTIALS", "ACCESS_KEY"]
        for k in list(scrubbed_env.keys()):
            for s_key in sensitive_env_keys:
                if s_key in k.upper():
                    scrubbed_env.pop(k, None)

        self.log_audit("COMMAND_EXEC", cmd_str, True, "Command verified and environment scrubbed", "INFO")
        argv = cmd_or_argv if isinstance(cmd_or_argv, list) else cmd_str.split()
        return argv, scrubbed_env

    def get_status(self) -> Dict[str, Any]:
        """Returns diagnostic status of the workspace sandbox for UI inspection."""
        recent_audits = []
        if self.store:
            try:
                with self.store.connect(False) as db:
                    rows = db.execute(
                        "SELECT timestamp, session_id, action_type, target, allowed, reason, severity FROM audit_events ORDER BY id DESC LIMIT 20"
                    ).fetchall()
                    for r in rows:
                        recent_audits.append({
                            "timestamp": r[0],
                            "session_id": r[1],
                            "action_type": r[2],
                            "target": r[3],
                            "allowed": bool(r[4]),
                            "reason": r[5],
                            "severity": r[6]
                        })
            except Exception:
                pass

        return {
            "status": "active",
            "policy": self.policy,
            "workspace_jail": str(self.workspace),
            "protected_patterns": SENSITIVE_PATTERNS,
            "recent_audits": recent_audits
        }
