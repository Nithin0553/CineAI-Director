from __future__ import annotations

from dataclasses import dataclass, field, asdict
from typing import Any, Dict, List, Optional


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

ALLOWED_TRANSITIONS = {
    "cut",
    "fade",
    "dissolve",
    "match_cut",
}

ALLOWED_EMOTIONS = {
    "neutral",
    "mysterious",
    "concerned",
    "focused",
    "confused",
    "shocked",
    "sad",
    "happy",
    "angry",
    "fearful",
    "determined",
}


@dataclass
class Vector3:
    x: float
    y: float
    z: float

    def to_dict(self) -> Dict[str, float]:
        return asdict(self)


@dataclass
class Rotation:
    x: float
    y: float
    z: float

    def to_dict(self) -> Dict[str, float]:
        return asdict(self)


@dataclass
class CharacterInstruction:
    name: str
    animation: str = "Idle"
    start_position: Optional[Vector3] = None
    end_position: Optional[Vector3] = None
    facing_y: Optional[float] = None
    move_speed: Optional[float] = None

    def to_dict(self) -> Dict[str, Any]:
        return {
            "name": self.name,
            "animation": self.animation,
            "start_position": self.start_position.to_dict() if self.start_position else None,
            "end_position": self.end_position.to_dict() if self.end_position else None,
            "facing_y": self.facing_y,
            "move_speed": self.move_speed,
        }


@dataclass
class CameraMovementInstruction:
    type: str = "static"
    target: Optional[str] = None
    speed: Optional[float] = None
    radius: Optional[float] = None

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)


@dataclass
class CameraInstruction:
    name: str
    shot_type: str

    # These are kept in the universal schema for future world-space support.
    # In the current Unity export, character/object shots use target-relative offsets.
    position: Vector3
    rotation: Rotation

    fov: float
    look_at: Optional[str] = None
    follow: Optional[str] = None

    # Target-relative camera offset.
    # This is the most universal form for Unity because it works across scenes.
    offset: Optional[Vector3] = None

    movement: CameraMovementInstruction = field(default_factory=CameraMovementInstruction)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "name": self.name,
            "shot_type": self.shot_type,
            "position": self.position.to_dict(),
            "rotation": self.rotation.to_dict(),
            "fov": self.fov,
            "look_at": self.look_at,
            "follow": self.follow,
            "offset": self.offset.to_dict() if self.offset else None,
            "movement": self.movement.to_dict(),
        }


@dataclass
class TransitionInstruction:
    type: str = "cut"
    duration: float = 0.0

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)


@dataclass
class Beat:
    beat_id: int
    purpose: str
    speaker: str
    dialogue: str
    action: str
    emotion: str
    intent: str
    duration: float
    character: Optional[CharacterInstruction]
    camera: CameraInstruction
    transition: TransitionInstruction

    def to_dict(self) -> Dict[str, Any]:
        return {
            "beat_id": self.beat_id,
            "purpose": self.purpose,
            "speaker": self.speaker,
            "dialogue": self.dialogue,
            "action": self.action,
            "emotion": self.emotion,
            "intent": self.intent,
            "duration": self.duration,
            "character": self.character.to_dict() if self.character else None,
            "camera": self.camera.to_dict(),
            "transition": self.transition.to_dict(),
        }


@dataclass
class UniversalBeatScript:
    project: str
    scene_id: str
    scene_title: str
    characters: List[str]
    objects: List[str]
    locations: List[str]
    beats: List[Beat]

    def to_dict(self) -> Dict[str, Any]:
        return {
            "project": self.project,
            "scene_id": self.scene_id,
            "scene_title": self.scene_title,
            "characters": self.characters,
            "objects": self.objects,
            "locations": self.locations,
            "beats": [beat.to_dict() for beat in self.beats],
        }


def unity_legacy_beat_script(universal_script: UniversalBeatScript) -> Dict[str, Any]:
    """
    Converts Universal Beat Script into the current Unity Beat.cs-compatible format.

    Important architecture:
    - Environment/aerial shots may use exact world camera position.
    - Character/object shots use exact target-relative camera offsets.
    - LLM/multi-agent brain decides the offset, FOV, movement, target, animation, and timing.
    - Unity only resolves targets and applies those values.
    """

    legacy_beats: List[Dict[str, Any]] = []

    for beat in universal_script.beats:
        cam = beat.camera
        char = beat.character
        movement = cam.movement

        speaker_name = char.name if char else beat.speaker
        focus_target = cam.look_at or cam.follow or (char.name if char else "")

        is_environment_or_aerial = char is None and bool(cam.look_at)

        offset = cam.offset if cam.offset else Vector3(0.0, 1.7, -4.0)

        use_exact_position = is_environment_or_aerial
        use_exact_offset = not is_environment_or_aerial

        legacy: Dict[str, Any] = {
            "scene_id": 1,
            "beat_id": beat.beat_id,

            "speaker": speaker_name,
            "dialogue": beat.dialogue,
            "action": beat.action,
            "emotion": beat.emotion,
            "intent": beat.intent,

            "shot_type": cam.shot_type,
            "camera_angle": "eye_level",
            "camera_movement": movement.type,
            "focus_target": focus_target,
            "secondary_target": "",

            "duration": beat.duration,
            "transition": beat.transition.type,

            # Only aerial/environment shots use world position.
            "use_exact_camera_position": use_exact_position,
            "camera_position_x": cam.position.x,
            "camera_position_y": cam.position.y,
            "camera_position_z": cam.position.z,

            # Disable exact rotation for target-relative shots.
            # Let Unity/Cinemachine aim at the resolved target.
            "use_exact_camera_rotation": use_exact_position,
            "camera_rotation_x": cam.rotation.x,
            "camera_rotation_y": cam.rotation.y,
            "camera_rotation_z": cam.rotation.z,

            "camera_follow_target": cam.follow or "",
            "camera_look_at_target": cam.look_at or focus_target,

            # Main universal camera control for character/object shots.
            "use_exact_camera_offset": use_exact_offset,
            "camera_offset_x": offset.x,
            "camera_offset_y": offset.y,
            "camera_offset_z": offset.z,

            "fov_override": cam.fov,

            "orbit_speed_override": movement.speed if movement.type == "orbit" and movement.speed else 0.0,
            "dolly_speed_override": movement.speed if movement.type in {"dolly_in", "dolly_out"} and movement.speed else 0.0,
            "pan_speed_override": movement.speed if movement.type == "pan" and movement.speed else 0.0,

            "animation_name": char.animation if char else "",
            "move_speed": char.move_speed if char and char.move_speed else 0.0,

            "use_char_start_position": False,
            "char_start_x": 0.0,
            "char_start_y": 0.0,
            "char_start_z": 0.0,

            "use_char_end_position": False,
            "char_end_x": 0.0,
            "char_end_y": 0.0,
            "char_end_z": 0.0,

            "use_char_facing": False,
            "char_facing_y": 0.0,
        }

        if char and char.start_position:
            legacy["use_char_start_position"] = True
            legacy["char_start_x"] = char.start_position.x
            legacy["char_start_y"] = char.start_position.y
            legacy["char_start_z"] = char.start_position.z

        if char and char.end_position:
            legacy["use_char_end_position"] = True
            legacy["char_end_x"] = char.end_position.x
            legacy["char_end_y"] = char.end_position.y
            legacy["char_end_z"] = char.end_position.z

        if char and char.facing_y is not None:
            legacy["use_char_facing"] = True
            legacy["char_facing_y"] = char.facing_y

        legacy_beats.append(legacy)

    return {"beats": legacy_beats}