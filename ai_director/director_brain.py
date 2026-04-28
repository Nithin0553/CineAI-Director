from __future__ import annotations

import json
from typing import Any, Dict, List

from schema import (
    Beat,
    CameraInstruction,
    CameraMovementInstruction,
    CharacterInstruction,
    Rotation,
    TransitionInstruction,
    UniversalBeatScript,
    Vector3,
)


class LocalDirectorBrain:
    """
    First prototype of the AI Director Brain.

    This version is intentionally local and deterministic so the project works
    without API keys. Later, this class can be replaced with an LLM-backed brain.
    """

    def generate(self, scene_context: Dict[str, Any], story: str) -> UniversalBeatScript:
        characters: List[str] = scene_context.get("characters", [])
        objects: List[str] = scene_context.get("objects", [])
        locations: List[str] = scene_context.get("locations", [])

        main_character = characters[0] if characters else "CHARACTER"
        main_object = objects[0] if objects else "OBJECT"

        start = scene_context.get("default_character_start", {"x": -5.0, "y": 0.0, "z": 8.0})
        middle = scene_context.get("default_character_middle", {"x": -5.0, "y": 0.0, "z": 3.5})
        end = scene_context.get("default_character_end", {"x": -5.0, "y": 0.0, "z": -1.0})
        object_pos = scene_context.get("default_object_position", {"x": -5.0, "y": 0.0, "z": -3.0})

        beats = [
            Beat(
                beat_id=1,
                purpose="Establish the mysterious object and location.",
                speaker="",
                dialogue="",
                action=f"Aerial establishing shot reveals {main_object} in the scene.",
                emotion="mysterious",
                intent="establish",
                duration=5.0,
                character=None,
                camera=CameraInstruction(
                    name="VCam_1",
                    shot_type="wide_shot",
                    position=Vector3(object_pos["x"], object_pos["y"] + 14.0, object_pos["z"] + 10.0),
                    rotation=Rotation(60.0, 180.0, 0.0),
                    fov=55.0,
                    look_at=main_object,
                    follow=None,
                    movement=CameraMovementInstruction(
                        type="orbit",
                        target=main_object,
                        speed=12.0,
                        radius=10.0,
                    ),
                ),
                transition=TransitionInstruction(type="cut", duration=0.0),
            ),
            Beat(
                beat_id=2,
                purpose="Show the character entering the scene.",
                speaker=main_character,
                dialogue="",
                action=f"{main_character} walks toward {main_object}.",
                emotion="focused",
                intent="approach",
                duration=5.0,
                character=CharacterInstruction(
                    name=main_character,
                    animation="Walk",
                    start_position=Vector3(start["x"], start["y"], start["z"]),
                    end_position=Vector3(middle["x"], middle["y"], middle["z"]),
                    facing_y=180.0,
                    move_speed=1.0,
                ),
                camera=CameraInstruction(
                    name="VCam_2",
                    shot_type="medium_shot",
                    position=Vector3(start["x"], start["y"] + 1.8, start["z"] - 4.0),
                    rotation=Rotation(8.0, 0.0, 0.0),
                    fov=50.0,
                    look_at=main_character,
                    follow=main_character,
                    movement=CameraMovementInstruction(
                        type="follow",
                        target=main_character,
                        speed=1.0,
                        radius=None,
                    ),
                ),
                transition=TransitionInstruction(type="cut", duration=0.0),
            ),
            Beat(
                beat_id=3,
                purpose="Build anticipation as the character gets closer.",
                speaker=main_character,
                dialogue="",
                action=f"{main_character} slows down as the strange {main_object} becomes visible.",
                emotion="concerned",
                intent="build_tension",
                duration=5.0,
                character=CharacterInstruction(
                    name=main_character,
                    animation="Walk",
                    start_position=Vector3(middle["x"], middle["y"], middle["z"]),
                    end_position=Vector3(end["x"], end["y"], end["z"]),
                    facing_y=180.0,
                    move_speed=0.8,
                ),
                camera=CameraInstruction(
                    name="VCam_3",
                    shot_type="wide_shot",
                    position=Vector3(end["x"] + 4.0, end["y"] + 2.2, end["z"] + 5.5),
                    rotation=Rotation(10.0, 210.0, 0.0),
                    fov=60.0,
                    look_at=main_character,
                    follow=None,
                    movement=CameraMovementInstruction(
                        type="static",
                        target=main_character,
                        speed=None,
                        radius=None,
                    ),
                ),
                transition=TransitionInstruction(type="cut", duration=0.0),
            ),
            Beat(
                beat_id=4,
                purpose="Reveal the object from the character perspective.",
                speaker=main_character,
                dialogue="",
                action=f"Over-the-shoulder reveal of {main_object} in front of {main_character}.",
                emotion="mysterious",
                intent="reveal",
                duration=4.0,
                character=CharacterInstruction(
                    name=main_character,
                    animation="Idle",
                    start_position=Vector3(end["x"], end["y"], end["z"]),
                    end_position=None,
                    facing_y=180.0,
                    move_speed=None,
                ),
                camera=CameraInstruction(
                    name="VCam_4",
                    shot_type="over_the_shoulder",
                    position=Vector3(end["x"], end["y"] + 1.8, end["z"] + 1.2),
                    rotation=Rotation(8.0, 180.0, 0.0),
                    fov=52.0,
                    look_at=main_object,
                    follow=main_character,
                    movement=CameraMovementInstruction(
                        type="pan",
                        target=main_object,
                        speed=8.0,
                        radius=1.5,
                    ),
                ),
                transition=TransitionInstruction(type="cut", duration=0.0),
            ),
            Beat(
                beat_id=5,
                purpose="Show emotional reaction and inner thought.",
                speaker=main_character,
                dialogue=self._extract_dialogue_or_default(story),
                action=f"Close-up on {main_character}'s face as he reacts with confusion.",
                emotion="confused",
                intent="question",
                duration=5.0,
                character=CharacterInstruction(
                    name=main_character,
                    animation="Reaction",
                    start_position=Vector3(end["x"], end["y"], end["z"]),
                    end_position=None,
                    facing_y=180.0,
                    move_speed=None,
                ),
                camera=CameraInstruction(
                    name="VCam_5",
                    shot_type="close_up",
                    position=Vector3(end["x"], end["y"] + 1.7, end["z"] - 2.0),
                    rotation=Rotation(5.0, 0.0, 0.0),
                    fov=35.0,
                    look_at=main_character,
                    follow=None,
                    movement=CameraMovementInstruction(
                        type="dolly_in",
                        target=main_character,
                        speed=0.15,
                        radius=None,
                    ),
                ),
                transition=TransitionInstruction(type="fade", duration=1.0),
            ),
        ]

        return UniversalBeatScript(
            project="CineAI Director",
            scene_id="scene_001",
            scene_title="Generated Cutscene",
            characters=characters,
            objects=objects,
            locations=locations,
            beats=beats,
        )

    @staticmethod
    def _extract_dialogue_or_default(story: str) -> str:
        if '"' in story:
            parts = story.split('"')
            if len(parts) >= 3:
                return parts[1].strip()

        if ":" in story:
            possible_line = story.split(":")[-1].strip()
            if len(possible_line) > 3:
                return possible_line

        return "Strange, where did this come from?"


def universal_script_from_dict(data: Dict[str, Any]) -> UniversalBeatScript:
    """
    Placeholder for future LLM JSON → dataclass conversion.
    For now, local generation directly creates dataclasses.
    """
    raise NotImplementedError("LLM JSON parsing will be added in the next phase.")