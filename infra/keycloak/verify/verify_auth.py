#!/usr/bin/env python3

from __future__ import annotations

import base64
import json
import os
import sys
import time
from typing import Any
from urllib import error, parse, request


EDGE_PROXY_URL = os.environ.get("EDGE_PROXY_URL", "http://edge-proxy:8080").rstrip("/")
KEYCLOAK_URL = os.environ.get("KEYCLOAK_URL", f"{EDGE_PROXY_URL}/auth").rstrip("/")
REALM = os.environ.get("KEYCLOAK_REALM", "umbral")
SHORT_LIVED_CLIENT_ID = os.environ.get("SHORT_LIVED_CLIENT_ID", "umbral-web-shortlived")
SCENARIO = os.environ.get("AUTH_SMOKE_SCENARIO", "all")

EXPECTED_API_AUDIENCES = {
    "umbral-identity-access-api",
    "umbral-mission-management-api",
    "umbral-session-operations-api",
    "umbral-scoring-monitoring-api",
}
BOOTSTRAP_PATHS = [
    "/identity-access/api/identity-access/bootstrap",
    "/mission-management/api/mission-management/bootstrap",
    "/session-operations/api/session-operations/bootstrap",
    "/scoring-monitoring/api/scoring-monitoring/bootstrap",
]
ROLE_SMOKE_PATHS = {
    "admin": "/mission-management/api/mission-management/smoke/administrator",
    "operator": "/session-operations/api/session-operations/smoke/operator",
    "participant": "/scoring-monitoring/api/scoring-monitoring/smoke/participant",
}
SEED_USERS = {
    "admin": "admin123!",
    "operator": "operator123!",
    "participant": "participant123!",
}


def log(message: str) -> None:
    print(message, flush=True)


def fail(message: str) -> None:
    raise RuntimeError(message)


def wait_for_json(url: str, timeout_seconds: int = 120) -> None:
    deadline = time.time() + timeout_seconds

    while time.time() < deadline:
        try:
            payload = request_json("GET", url)
            if payload:
                return
        except Exception:
            time.sleep(2)

    fail(f"Timed out waiting for {url}.")


def request_json(method: str, url: str, *, data: bytes | None = None, headers: dict[str, str] | None = None) -> Any:
    req = request.Request(url, data=data, method=method)
    for key, value in (headers or {}).items():
        req.add_header(key, value)

    with request.urlopen(req, timeout=30) as response:
        body = response.read().decode("utf-8")
        return json.loads(body) if body else {}


def request_status(method: str, url: str, *, bearer_token: str | None = None) -> int:
    req = request.Request(url, method=method)
    if bearer_token:
        req.add_header("Authorization", f"Bearer {bearer_token}")

    try:
        with request.urlopen(req, timeout=30) as response:
            response.read()
            return response.status
    except error.HTTPError as exc:
        exc.read()
        return exc.code


def get_password_token(client_id: str, username: str, password: str) -> str:
    token_url = f"{KEYCLOAK_URL}/realms/{REALM}/protocol/openid-connect/token"
    form = parse.urlencode(
        {
            "grant_type": "password",
            "client_id": client_id,
            "username": username,
            "password": password,
            "scope": "openid",
        }
    ).encode("utf-8")

    payload = request_json(
        "POST",
        token_url,
        data=form,
        headers={"Content-Type": "application/x-www-form-urlencoded"},
    )

    access_token = payload.get("access_token")
    if not access_token:
        fail(f"Token endpoint did not return an access token for client '{client_id}' and user '{username}'.")

    return access_token


def decode_jwt_payload(token: str) -> dict[str, Any]:
    parts = token.split(".")
    if len(parts) != 3:
        fail("JWT format is invalid.")

    payload = parts[1]
    payload += "=" * (-len(payload) % 4)
    decoded = base64.urlsafe_b64decode(payload.encode("utf-8"))
    return json.loads(decoded.decode("utf-8"))


def audience_set(payload: dict[str, Any]) -> set[str]:
    raw_audience = payload.get("aud", [])
    if isinstance(raw_audience, str):
        return {raw_audience}

    return set(raw_audience)


def role_set(payload: dict[str, Any]) -> set[str]:
    roles = payload.get("roles")
    if isinstance(roles, list):
        return {role for role in roles if isinstance(role, str) and role}

    realm_access = payload.get("realm_access")
    if isinstance(realm_access, dict):
        nested_roles = realm_access.get("roles", [])
        if isinstance(nested_roles, list):
            return {role for role in nested_roles if isinstance(role, str) and role}

    return set()


def require_subject_claim(payload: dict[str, Any], client_id: str) -> None:
    subject = payload.get("sub")
    if not isinstance(subject, str) or not subject.strip():
        fail(
            f"{client_id} token does not contain sub. "
            "Local realm is likely missing the Subject (sub) mapper for lightweight access tokens."
        )


def expect_status(actual_status: int, expected_status: int, label: str) -> None:
    if actual_status != expected_status:
        fail(f"{label}: expected HTTP {expected_status}, got HTTP {actual_status}.")

    log(f"[ok] {label}: HTTP {actual_status}")


def verify_login_and_audiences() -> None:
    for client_id in ("umbral-web", "umbral-mobile"):
        token = get_password_token(client_id, "participant", SEED_USERS["participant"])
        payload = decode_jwt_payload(token)
        require_subject_claim(payload, client_id)
        audiences = audience_set(payload)
        missing_audiences = sorted(EXPECTED_API_AUDIENCES.difference(audiences))
        if missing_audiences:
            fail(f"{client_id} token is missing audiences: {', '.join(missing_audiences)}.")

        roles = role_set(payload)
        if "Participant" not in roles:
            fail(f"{client_id} token does not contain the Participant realm role.")

        log(f"[ok] {client_id} token includes API audiences and realm role claims.")

    role_tokens = {
        "admin": get_password_token("umbral-web", "admin", SEED_USERS["admin"]),
        "operator": get_password_token("umbral-web", "operator", SEED_USERS["operator"]),
        "participant": get_password_token("umbral-web", "participant", SEED_USERS["participant"]),
    }

    for bootstrap_path in BOOTSTRAP_PATHS:
        status = request_status(
            "GET",
            f"{EDGE_PROXY_URL}{bootstrap_path}",
            bearer_token=role_tokens["participant"])
        expect_status(status, 200, f"Participant token reaches {bootstrap_path} through edge proxy")

    for role_name, smoke_path in ROLE_SMOKE_PATHS.items():
        status = request_status("GET", f"{EDGE_PROXY_URL}{smoke_path}", bearer_token=role_tokens[role_name])
        expect_status(status, 200, f"{role_name} token reaches {smoke_path} through edge proxy")


def verify_invalid_token() -> None:
    invalid_token = "invalid.local.token"
    status = request_status(
        "GET",
        f"{EDGE_PROXY_URL}{BOOTSTRAP_PATHS[0]}",
        bearer_token=invalid_token,
    )
    expect_status(status, 401, "Invalid token is rejected by API behind edge proxy")


def verify_expired_token() -> None:
    token = get_password_token(SHORT_LIVED_CLIENT_ID, "participant", SEED_USERS["participant"])
    payload = decode_jwt_payload(token)
    expires_at = int(payload.get("exp", 0))
    if expires_at <= 0:
        fail("Short-lived token does not contain a valid exp claim.")

    wait_seconds = max(expires_at - int(time.time()) + 1, 2)
    log(f"Waiting {wait_seconds}s for short-lived token to expire.")
    time.sleep(wait_seconds)

    status = request_status(
        "GET",
        f"{EDGE_PROXY_URL}{BOOTSTRAP_PATHS[0]}",
        bearer_token=token,
    )
    expect_status(status, 401, "Expired token is rejected by API behind edge proxy")


def verify_insufficient_role() -> None:
    participant_token = get_password_token("umbral-web", "participant", SEED_USERS["participant"])
    status = request_status(
        "GET",
        f"{EDGE_PROXY_URL}{ROLE_SMOKE_PATHS['admin']}",
        bearer_token=participant_token,
    )
    expect_status(status, 403, "Participant token is rejected from administrator smoke route due to insufficient role")


def main() -> int:
    log("Waiting for edge proxy and Keycloak discovery document.")
    wait_for_json(f"{KEYCLOAK_URL}/realms/{REALM}/.well-known/openid-configuration")
    wait_for_json(f"{EDGE_PROXY_URL}/health")

    scenario_order = {
        "login": verify_login_and_audiences,
        "invalid": verify_invalid_token,
        "expired": verify_expired_token,
        "insufficient-role": verify_insufficient_role,
    }

    selected = list(scenario_order) if SCENARIO == "all" else [SCENARIO]

    for scenario in selected:
        test = scenario_order.get(scenario)
        if test is None:
            fail(f"Unsupported AUTH_SMOKE_SCENARIO '{SCENARIO}'.")

        log(f"Running scenario: {scenario}")
        test()

    log("All requested authentication smoke tests passed.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:  # noqa: BLE001
        print(f"[error] {exc}", file=sys.stderr)
        raise SystemExit(1)
