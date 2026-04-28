from __future__ import annotations

from typing import Any, Dict, List

from schema import (
    ALLOWED_CAMERA_MOVEMENTS,
    ALLOWED_EMOTIONS,
    ALLOWED_SHOT_TYPES,
    ALLOWED_TRANSITIONS,
)


class BeatScriptValidationError(Exception):
    pass


def _require(condition: bool, message: str, errors: List[str]) -> None:
    if not condition:
        errors.append(message)


def validate_universal_script(data: Dict[str, Any]) -> None:
    errors: List[str] = []

    _require(isinstance(data, dict), "Root must be a JSON object.", errors)

    if not isinstance(data, dict):
        raise BeatScriptValidationError("\n".join(errors))

    _require(data.get("project") == "CineAI Director", "project must be 'CineAI Director'.", errors)
    _require(bool(data.get("scene_id")), "scene_id is required.", errors)
    _require(bool(data.get("scene_title")), "scene_title is required.", errors)
    _require(isinstance(data.get("characters"), list), "characters must be a list.", errors)
    _require(isinstance(data.get("objects"), list), "objects must be a list.", errors)
    _require(isinstance(data.get("locations"), list), "locations must be a list.", errors)
    _require(isinstance(data.get("beats"), list) and len(data.get("beats", [])) > 0, "beats must be a non-empty list.", errors)

    for index, beat in enumerate(data.get("beats", []), start=1):
        prefix = f"beats[{index}]"

        _require(isinstance(beat.get("beat_id"), int), f"{prefix}.beat_id must be an integer.", errors)
        _require(bool(beat.get("purpose")), f"{prefix}.purpose is required.", errors)
        _require(isinstance(beat.get("duration"), (int, float)) and beat.get("duration") > 0, f"{prefix}.duration must be > 0.", errors)

        emotion = beat.get("emotion", "neutral")
        _require(emotion in ALLOWED_EMOTIONS, f"{prefix}.emotion '{emotion}' is not allowed.", errors)

        camera = beat.get("camera")
        _require(isinstance(camera, dict), f"{prefix}.camera must be an object.", errors)

        if isinstance(camera, dict):
            shot_type = camera.get("shot_type")
            _require(shot_type in ALLOWED_SHOT_TYPES, f"{prefix}.camera.shot_type '{shot_type}' is not allowed.", errors)

            _require(isinstance(camera.get("position"), dict), f"{prefix}.camera.position is required.", errors)
            _require(isinstance(camera.get("rotation"), dict), f"{prefix}.camera.rotation is required.", errors)
            _require(isinstance(camera.get("fov"), (int, float)), f"{prefix}.camera.fov must be a number.", errors)

            movement = camera.get("movement", {})
            _require(isinstance(movement, dict), f"{prefix}.camera.movement must be an object.", errors)

            if isinstance(movement, dict):
                movement_type = movement.get("type", "static")
                _require(
                    movement_type in ALLOWED_CAMERA_MOVEMENTS,
                    f"{prefix}.camera.movement.type '{movement_type}' is not allowed.",
                    errors,
                    )

        transition = beat.get("transition")
        _require(isinstance(transition, dict), f"{prefix}.transition must be an object.", errors)

        if isinstance(transition, dict):
            transition_type = transition.get("type", "cut")
            _require(
                transition_type in ALLOWED_TRANSITIONS,
                f"{prefix}.transition.type '{transition_type}' is not allowed.",
                errors,
                )

    if errors:
        raise BeatScriptValidationError("\n".join(errors))