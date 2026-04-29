from __future__ import annotations

from dataclasses import dataclass
from typing import Dict, Optional

from schema import CameraMovementInstruction, Rotation, Vector3


@dataclass(frozen=True)
class ShotTemplate:
    template_id: str
    description: str

    shot_type: str
    camera_movement: str
    fov: float

    offset: Optional[Vector3]
    position: Vector3
    rotation: Rotation

    force_world_position: bool = False

    movement_speed: Optional[float] = None
    movement_radius: Optional[float] = None


class CinematicKnowledgeBase:
    """
    Temporary cinematic knowledge layer.

    This file keeps reusable shot knowledge in one place.
    Later, the LLM can choose these template IDs or generate similar values directly.

    Unity should not decide cinematography.
    Unity should only execute the JSON values produced by this Python layer.
    """

    def __init__(self) -> None:
        self.templates: Dict[str, ShotTemplate] = {
            "aerial_object_orbit": ShotTemplate(
                template_id="aerial_object_orbit",
                description="A wide aerial orbit used to establish an important object or location.",
                shot_type="wide_shot",
                camera_movement="orbit",
                fov=55.0,
                offset=None,
                position=Vector3(0.0, 7.0, 9.0),
                rotation=Rotation(0.0, 0.0, 0.0),
                force_world_position=True,
                movement_speed=8.0,
                movement_radius=9.0,
            ),

            "feet_follow_detail": ShotTemplate(
                template_id="feet_follow_detail",
                description="A low walking-detail shot that follows the feet/legs using a stable feet anchor.",
                shot_type="close_up",
                camera_movement="follow",
                fov=42.0,
                offset=Vector3(0.0, 0.25, -2.4),
                position=Vector3(0.0, 0.0, 0.0),
                rotation=Rotation(0.0, 0.0, 0.0),
                force_world_position=False,
                movement_speed=1.0,
                movement_radius=None,
            ),

            "medium_walk_approach": ShotTemplate(
                template_id="medium_walk_approach",
                description="A medium follow shot for a character walking toward a subject.",
                shot_type="medium_shot",
                camera_movement="follow",
                fov=55.0,
                offset=Vector3(0.0, 1.8, 4.5),
                position=Vector3(0.0, 0.0, 0.0),
                rotation=Rotation(0.0, 0.0, 0.0),
                force_world_position=False,
                movement_speed=0.8,
                movement_radius=None,
            ),

            "over_shoulder_reveal": ShotTemplate(
                template_id="over_shoulder_reveal",
                description="An over-the-shoulder reveal shot where the character frames an important object.",
                shot_type="over_the_shoulder",
                camera_movement="pan",
                fov=60.0,
                offset=Vector3(0.55, 1.85, -2.2),
                position=Vector3(0.0, 0.0, 0.0),
                rotation=Rotation(0.0, 0.0, 0.0),
                force_world_position=False,
                movement_speed=8.0,
                movement_radius=1.5,
            ),

            "face_reaction_closeup": ShotTemplate(
                template_id="face_reaction_closeup",
                description="A close emotional reaction shot using a stable head anchor.",
                shot_type="close_up",
                camera_movement="follow",
                fov=30.0,
                offset=Vector3(0.25, 0.12, -2.4),
                position=Vector3(0.0, 0.0, 0.0),
                rotation=Rotation(0.0, 0.0, 0.0),
                force_world_position=False,
                movement_speed=None,
                movement_radius=None,
            ),

            "object_insert": ShotTemplate(
                template_id="object_insert",
                description="A closer insert shot of an important object.",
                shot_type="insert_shot",
                camera_movement="static",
                fov=36.0,
                offset=Vector3(0.0, 1.0, -2.2),
                position=Vector3(0.0, 0.0, 0.0),
                rotation=Rotation(0.0, 0.0, 0.0),
                force_world_position=False,
                movement_speed=None,
                movement_radius=None,
            ),
        }

    def get(self, template_id: str) -> ShotTemplate:
        if template_id not in self.templates:
            available = ", ".join(sorted(self.templates.keys()))
            raise KeyError(f"Unknown shot template: {template_id}. Available: {available}")

        return self.templates[template_id]

    def movement_for(self, template: ShotTemplate, target: Optional[str]) -> CameraMovementInstruction:
        return CameraMovementInstruction(
            type=template.camera_movement,
            target=target,
            speed=template.movement_speed,
            radius=template.movement_radius,
        )