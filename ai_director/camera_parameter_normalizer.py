from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Dict, Optional

from schema import CameraMovementInstruction, Rotation, Vector3


ALLOWED_SHOT_TYPES = {
    "establishing_shot",
    "wide_shot",
    "medium_shot",
    "close_up",
    "extreme_close_up",
    "over_the_shoulder",
    "insert_shot",
    "reaction_shot",
    "tracking_shot",
}

ALLOWED_CAMERA_MOVEMENTS = {
    "static",
    "follow",
    "orbit",
    "pan",
    "tilt",
    "dolly_in",
    "dolly_out",
    "truck_left",
    "truck_right",
}

ALLOWED_TARGET_ROLES = {"character", "object", "feet", "head", "body"}


@dataclass
class NormalizedCameraPlan:
    shot_type: str
    target_role: str
    look_at_role: str
    follow_role: Optional[str]
    movement: CameraMovementInstruction
    fov: float
    offset: Vector3
    position: Vector3
    rotation: Rotation
    force_world_position: bool


class CameraParameterNormalizer:
    """
    Version 2 camera parameter safety layer.

    The LLM can generate camera parameters directly.
    This class validates and clamps them before Unity receives them.
    """

    def normalize(
            self,
            raw_camera: Optional[Dict[str, Any]],
            fallback_template: Any,
            semantic_target_role: str,
    ) -> NormalizedCameraPlan:
        raw_camera = raw_camera or {}

        shot_type = self._safe_choice(
            value=raw_camera.get("shot_type"),
            allowed=ALLOWED_SHOT_TYPES,
            fallback=getattr(fallback_template, "shot_type", "medium_shot"),
        )

        target_role = self._safe_choice(
            value=raw_camera.get("target_role"),
            allowed=ALLOWED_TARGET_ROLES,
            fallback=semantic_target_role,
        )

        look_at_role = self._safe_choice(
            value=raw_camera.get("look_at_role"),
            allowed=ALLOWED_TARGET_ROLES,
            fallback=target_role,
        )

        follow_role_raw = raw_camera.get("follow_role", target_role)

        follow_role: Optional[str]
        if follow_role_raw in {"", None, "none", "None", "null"}:
            follow_role = None
        else:
            follow_role = self._safe_choice(
                value=follow_role_raw,
                allowed=ALLOWED_TARGET_ROLES,
                fallback=target_role,
            )

        movement_type = self._safe_choice(
            value=raw_camera.get("movement"),
            allowed=ALLOWED_CAMERA_MOVEMENTS,
            fallback=getattr(fallback_template, "camera_movement", "static"),
        )

        fov = self._clamp_float(
            value=raw_camera.get("fov"),
            fallback=getattr(fallback_template, "fov", 50.0),
            min_value=18.0,
            max_value=75.0,
        )

        fallback_offset = getattr(fallback_template, "offset", None) or Vector3(0.0, 1.7, -4.0)

        offset = self._vector3(
            value=raw_camera.get("offset"),
            fallback=fallback_offset,
            min_value=-15.0,
            max_value=15.0,
        )

        force_world_position = bool(raw_camera.get("force_world_position", False))

        position = self._vector3(
            value=raw_camera.get("position"),
            fallback=getattr(fallback_template, "position", Vector3(0.0, 0.0, 0.0)),
            min_value=-100.0,
            max_value=100.0,
        )

        rotation = self._rotation(
            value=raw_camera.get("rotation"),
            fallback=getattr(fallback_template, "rotation", Rotation(0.0, 0.0, 0.0)),
        )

        speed = self._optional_clamp_float(
            value=raw_camera.get("speed"),
            fallback=getattr(fallback_template, "movement_speed", None),
            min_value=0.0,
            max_value=25.0,
        )

        radius = self._optional_clamp_float(
            value=raw_camera.get("radius"),
            fallback=getattr(fallback_template, "movement_radius", None),
            min_value=0.0,
            max_value=30.0,
        )

        if not force_world_position:
            position = Vector3(0.0, 0.0, 0.0)
            rotation = Rotation(0.0, 0.0, 0.0)

        movement = CameraMovementInstruction(
            type=movement_type,
            target=None,
            speed=speed,
            radius=radius,
        )

        return NormalizedCameraPlan(
            shot_type=shot_type,
            target_role=target_role,
            look_at_role=look_at_role,
            follow_role=follow_role,
            movement=movement,
            fov=fov,
            offset=offset,
            position=position,
            rotation=rotation,
            force_world_position=force_world_position,
        )

    @staticmethod
    def _safe_choice(value: Any, allowed: set[str], fallback: str) -> str:
        text = str(value).strip() if value is not None else ""

        if text in allowed:
            return text

        return fallback if fallback in allowed else next(iter(allowed))

    @staticmethod
    def _clamp_float(value: Any, fallback: float, min_value: float, max_value: float) -> float:
        try:
            number = float(value)
        except (TypeError, ValueError):
            number = float(fallback)

        return max(min_value, min(max_value, number))

    @staticmethod
    def _optional_clamp_float(
            value: Any,
            fallback: Optional[float],
            min_value: float,
            max_value: float,
    ) -> Optional[float]:
        if value is None and fallback is None:
            return None

        try:
            number = float(value if value is not None else fallback)
        except (TypeError, ValueError):
            return fallback

        return max(min_value, min(max_value, number))

    def _vector3(self, value: Any, fallback: Vector3, min_value: float, max_value: float) -> Vector3:
        if not isinstance(value, dict):
            return fallback

        return Vector3(
            x=self._clamp_float(value.get("x"), fallback.x, min_value, max_value),
            y=self._clamp_float(value.get("y"), fallback.y, min_value, max_value),
            z=self._clamp_float(value.get("z"), fallback.z, min_value, max_value),
        )

    def _rotation(self, value: Any, fallback: Rotation) -> Rotation:
        if not isinstance(value, dict):
            return fallback

        return Rotation(
            x=self._clamp_float(value.get("x"), fallback.x, -89.0, 89.0),
            y=self._clamp_float(value.get("y"), fallback.y, -360.0, 360.0),
            z=self._clamp_float(value.get("z"), fallback.z, -45.0, 45.0),
        )