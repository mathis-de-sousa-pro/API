"""Simple smoke test ensuring the Liked Songs virtual playlist is the first item."""

from __future__ import annotations

import os
import sys
from typing import Any, Iterable

import requests


LIKED_PLAYLIST_ID = "liked-saved-tracks"
LIKED_PLAYLIST_NAME = "Liked Songs"
BASE_URL = ""
SESSION_ID = ""


def _env_bool(name: str, default: bool = True) -> bool:
    raw = os.environ.get(name)
    if raw is None:
        return default
    return raw.strip().lower() not in {"0", "false", "no", "n"}


def _get_value(item: dict[str, Any], *keys: str) -> Any:
    for key in keys:
        if key in item:
            return item[key]
    return None


def _coerce_int(value: Any) -> int | None:
    if isinstance(value, bool):
        return None
    if isinstance(value, int):
        return value
    if isinstance(value, (float,)) and value.is_integer():
        return int(value)
    if isinstance(value, str) and value.strip().isdigit():
        return int(value.strip())
    return None


def main() -> None:
    base_url = BASE_URL
    session_id = SESSION_ID

    if not base_url:
        print("❌ Missing SWIPEZ_BASE_URL environment variable.", file=sys.stderr)
        sys.exit(2)

    if not session_id:
        print("❌ Missing SWIPEZ_SESSION_ID environment variable.", file=sys.stderr)
        sys.exit(2)

    verify_tls = _env_bool("SWIPEZ_VERIFY_TLS", True)

    url = f"{base_url.rstrip('/')}/api/spotify/playlists"
    headers = {"X-Session-Id": session_id}

    print(f"➡️  GET {url}")
    response = requests.get(url, headers=headers, timeout=30, verify=verify_tls)

    if response.status_code >= 400:
        print(f"❌ Request failed: HTTP {response.status_code} — {response.text}", file=sys.stderr)
        sys.exit(1)

    payload = response.json()
    items = payload.get("items") or payload.get("Items") or []

    if not items:
        print("❌ Response did not contain any playlist items.", file=sys.stderr)
        sys.exit(1)

    first = items[0]
    playlist_id = _get_value(first, "playlistId", "PlaylistId")
    playlist_name = _get_value(first, "name", "Name")
    track_count_raw = _get_value(first, "trackCount", "TrackCount")
    owner = _get_value(first, "owner", "Owner")
    image_url = _get_value(first, "imageUrl", "ImageUrl")

    errors: list[str] = []
    if playlist_id != LIKED_PLAYLIST_ID:
        errors.append(f"Expected first playlist id '{LIKED_PLAYLIST_ID}', got {playlist_id!r}.")
    if playlist_name != LIKED_PLAYLIST_NAME:
        errors.append(f"Expected first playlist name '{LIKED_PLAYLIST_NAME}', got {playlist_name!r}.")

    track_count = _coerce_int(track_count_raw)
    if track_count is None or track_count < 0:
        errors.append(f"Invalid track count for liked playlist: {track_count_raw!r}.")

    if owner in (None, ""):
        errors.append("Liked playlist owner is missing or empty.")

    if image_url not in (None, ""):
        errors.append("Liked playlist should not expose an image URL.")

    other_ids: Iterable[Any] = [
        _get_value(item, "playlistId", "PlaylistId") for item in items[1:]
    ]
    if LIKED_PLAYLIST_ID in other_ids:
        errors.append("Liked playlist id appears more than once in the response.")

    if errors:
        print("❌ Validation failed:")
        for err in errors:
            print(f"   - {err}")
        sys.exit(1)

    print(
        "✅ Liked Songs pseudo-playlist detected as first item with",
        f"{track_count} saved tracks (owner={owner}).",
    )


if __name__ == "__main__":
    main()
