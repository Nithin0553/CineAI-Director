from __future__ import annotations

from dataclasses import dataclass
from math import atan2, degrees, sqrt
from typing import Any, Dict, List, Optional

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


@dataclass
class StoryAnalysis:
    main_character: str
    main_object: str
    location: str
    dialogue: str
    emotion_arc: List[str]
    story_intent: str


@dataclass
class BeatPlan:
    beat_id: int
    purpose: str
    action: str
    emotion: str
    intent: str
    speaker: str
    dialogue: str
    shot_type: str
    camera_movement: str
    duration: float
    transition: str


@dataclass
class BlockingPlan:
    character_name: str
    animation: str
    start_position: Optional[Vector3]
    end_position: Optional[Vector3]
    facing_y: Optional[float]
    move_speed: Optional[float]


@dataclass
class CameraPlan:
    name: str
    shot_type: str
    position: Vector3
    rotation: Rotation
    fov: float
    look_at: Optional[str]
    follow: Optional[str]
    offset: Optional[Vector3]
    movement: CameraMovementInstruction
    force_world_position: bool = False


class StoryUnderstandingAgent:
    def analyze(self, scene_context: Dict[str, Any], story: str) -> StoryAnalysis:
        characters = scene_context.get("characters", [])
        objects = scene_context.get("objects", [])
        locations = scene_context.get("locations", [])

        main_character = characters[0] if characters else "CHARACTER"
        main_object = objects[0] if objects else "OBJECT"
        location = locations[0] if locations else "LOCATION"

        dialogue = self._extract_dialogue_or_default(story)

        return StoryAnalysis(
            main_character=main_character,
            main_object=main_object,
            location=location,
            dialogue=dialogue,
            emotion_arc=["mysterious", "focused", "concerned", "mysterious", "confused"],
            story_intent="approach_reveal_reaction",
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


class DirectorAgent:
    def create_beat_plan(self, analysis: StoryAnalysis) -> List[BeatPlan]:
        c = analysis.main_character
        o = analysis.main_object

        return [
            BeatPlan(
                beat_id=1,
                purpose="Establish the location and mysterious object.",
                action=f"Aerial establishing shot reveals {o} in the scene.",
                emotion="mysterious",
                intent="establish",
                speaker="",
                dialogue="",
                shot_type="wide_shot",
                camera_movement="orbit",
                duration=5.0,
                transition="cut",
            ),
            BeatPlan(
                beat_id=2,
                purpose="Show the character entering through movement detail.",
                action=f"Low close shot follows {c}'s feet as he walks toward {o}.",
                emotion="focused",
                intent="approach",
                speaker=c,
                dialogue="",
                shot_type="close_up",
                camera_movement="follow",
                duration=5.0,
                transition="cut",
            ),
            BeatPlan(
                beat_id=3,
                purpose="Show the character approaching the mysterious object.",
                action=f"{c} walks toward the camera and slows near {o}.",
                emotion="concerned",
                intent="approach",
                speaker=c,
                dialogue="",
                shot_type="medium_shot",
                camera_movement="follow",
                duration=6.0,
                transition="cut",
            ),
            BeatPlan(
                beat_id=4,
                purpose="Reveal the object from behind the character.",
                action=f"Over-the-shoulder reveal of {o} in front of {c}.",
                emotion="mysterious",
                intent="reveal",
                speaker=c,
                dialogue="",
                shot_type="over_the_shoulder",
                camera_movement="pan",
                duration=5.0,
                transition="cut",
            ),
            BeatPlan(
                beat_id=5,
                purpose="Show emotional reaction and inner thought.",
                action=f"Close-up on {c}'s face as he reacts with confusion.",
                emotion="confused",
                intent="question",
                speaker=c,
                dialogue=analysis.dialogue,
                shot_type="close_up",
                camera_movement="follow",
                duration=5.0,
                transition="fade",
            ),
        ]


class BlockingAgent:
    def create_blocking(
            self,
            beat_plan: BeatPlan,
            analysis: StoryAnalysis,
            scene_context: Dict[str, Any],
    ) -> Optional[BlockingPlan]:
        c = analysis.main_character

        start = self._vec(scene_context.get("default_character_start", {"x": -5.0, "y": 0.0, "z": 12.0}))
        middle = self._vec(scene_context.get("default_character_middle", {"x": -5.0, "y": 0.0, "z": 4.0}))
        end = self._vec(scene_context.get("default_character_end", {"x": -5.0, "y": 0.0, "z": -1.0}))

        if beat_plan.beat_id == 1:
            return None

        if beat_plan.beat_id == 2:
            return BlockingPlan(
                character_name=c,
                animation="Walk",
                start_position=start,
                end_position=middle,
                facing_y=180.0,
                move_speed=1.0,
            )

        if beat_plan.beat_id == 3:
            return BlockingPlan(
                character_name=c,
                animation="Walk",
                start_position=middle,
                end_position=end,
                facing_y=180.0,
                move_speed=0.8,
            )

        if beat_plan.beat_id == 4:
            return BlockingPlan(
                character_name=c,
                animation="Idle",
                start_position=end,
                end_position=None,
                facing_y=180.0,
                move_speed=None,
            )

        if beat_plan.beat_id == 5:
            return BlockingPlan(
                character_name=c,
                animation="Reaction",
                start_position=end,
                end_position=None,
                facing_y=180.0,
                move_speed=None,
            )

        return None

    @staticmethod
    def _vec(data: Dict[str, Any]) -> Vector3:
        return Vector3(float(data["x"]), float(data["y"]), float(data["z"]))


class CinematographerAgent:
    def create_camera_plan(
            self,
            beat_plan: BeatPlan,
            blocking: Optional[BlockingPlan],
            analysis: StoryAnalysis,
            scene_context: Dict[str, Any],
    ) -> CameraPlan:
        c = analysis.main_character
        o = analysis.main_object

        object_pos = self._vec(scene_context.get("default_object_position", {"x": -5.0, "y": 0.0, "z": -3.0}))

        if beat_plan.beat_id == 1:
            cam_pos = Vector3(object_pos.x, object_pos.y + 7.0, object_pos.z + 9.0)
            rotation = self._look_at_rotation(cam_pos, object_pos)

            return CameraPlan(
                name="VCam_1",
                shot_type="wide_shot",
                position=cam_pos,
                rotation=rotation,
                fov=55.0,
                look_at=o,
                follow=None,
                offset=None,
                movement=CameraMovementInstruction(
                    type="orbit",
                    target=o,
                    speed=8.0,
                    radius=9.0,
                ),
                force_world_position=True,
            )

        if beat_plan.beat_id == 2:
            return CameraPlan(
                name="VCam_2",
                shot_type="close_up",
                position=Vector3(0.0, 0.0, 0.0),
                rotation=Rotation(0.0, 0.0, 0.0),
                fov=45.0,
                look_at=f"{c}_FEET",
                follow=c,
                offset=Vector3(0.25, 0.45, -2.0),
                movement=CameraMovementInstruction(
                    type="follow",
                    target=c,
                    speed=1.0,
                    radius=None,
                ),
                force_world_position=False,
            )

        if beat_plan.beat_id == 3:
            return CameraPlan(
                name="VCam_3",
                shot_type="medium_shot",
                position=Vector3(0.0, 0.0, 0.0),
                rotation=Rotation(0.0, 0.0, 0.0),
                fov=55.0,
                look_at=c,
                follow=c,
                offset=Vector3(0.0, 1.8, 4.5),
                movement=CameraMovementInstruction(
                    type="follow",
                    target=c,
                    speed=0.8,
                    radius=None,
                ),
                force_world_position=False,
            )

        if beat_plan.beat_id == 4:
            return CameraPlan(
                name="VCam_4",
                shot_type="over_the_shoulder",
                position=Vector3(0.0, 0.0, 0.0),
                rotation=Rotation(0.0, 0.0, 0.0),
                fov=60.0,
                look_at=o,
                follow=c,
                offset=Vector3(0.55, 1.85, -2.2),
                movement=CameraMovementInstruction(
                    type="pan",
                    target=o,
                    speed=8.0,
                    radius=1.5,
                ),
                force_world_position=False,
            )

        if beat_plan.beat_id == 5:
            head_target = f"{c}_HEAD"

            return CameraPlan(
                name="VCam_5",
                shot_type="close_up",
                position=Vector3(0.0, 0.0, 0.0),
                rotation=Rotation(0.0, 0.0, 0.0),
                fov=28.0,
                look_at=head_target,
                follow=head_target,
                offset=Vector3(0.0, 0.15, -1.2),
                movement=CameraMovementInstruction(
                    type="follow",
                    target=head_target,
                    speed=None,
                    radius=None,
                ),
                force_world_position=False,
            )

        return CameraPlan(
            name=f"VCam_{beat_plan.beat_id}",
            shot_type=beat_plan.shot_type,
            position=Vector3(0.0, 0.0, 0.0),
            rotation=Rotation(0.0, 0.0, 0.0),
            fov=50.0,
            look_at=c,
            follow=c,
            offset=Vector3(0.0, 1.7, -4.0),
            movement=CameraMovementInstruction(
                type=beat_plan.camera_movement,
                target=c,
                speed=None,
                radius=None,
            ),
            force_world_position=False,
        )

    @staticmethod
    def _vec(data: Dict[str, Any]) -> Vector3:
        return Vector3(float(data["x"]), float(data["y"]), float(data["z"]))

    @staticmethod
    def _look_at_rotation(camera_pos: Vector3, target_pos: Vector3) -> Rotation:
        dx = target_pos.x - camera_pos.x
        dy = target_pos.y - camera_pos.y
        dz = target_pos.z - camera_pos.z

        horizontal_distance = sqrt(dx * dx + dz * dz)

        pitch = -degrees(atan2(dy, horizontal_distance))
        yaw = degrees(atan2(dx, dz))

        return Rotation(
            x=round(pitch, 2),
            y=round(yaw, 2),
            z=0.0,
        )


class EditorAgent:
    def refine(self, beat_plan: BeatPlan) -> TransitionInstruction:
        if beat_plan.transition == "fade":
            return TransitionInstruction(type="fade", duration=1.0)

        return TransitionInstruction(type=beat_plan.transition, duration=0.0)


class UnitySafetyValidatorAgent:
    def validate_names(
            self,
            script: UniversalBeatScript,
            scene_context: Dict[str, Any],
    ) -> UniversalBeatScript:
        allowed_characters = set(scene_context.get("characters", []))
        allowed_objects = set(scene_context.get("objects", []))

        for beat in script.beats:
            if beat.character and beat.character.name not in allowed_characters:
                raise ValueError(f"Invalid character name in beat {beat.beat_id}: {beat.character.name}")

            if beat.speaker and beat.speaker not in allowed_characters:
                raise ValueError(f"Invalid speaker name in beat {beat.beat_id}: {beat.speaker}")

            if beat.camera.look_at:
                is_bone_target = (
                        beat.camera.look_at.endswith("_FEET") or
                        beat.camera.look_at.endswith("_HEAD")
                )

                if not is_bone_target:
                    if beat.camera.look_at not in allowed_characters and beat.camera.look_at not in allowed_objects:
                        raise ValueError(f"Invalid look_at target in beat {beat.beat_id}: {beat.camera.look_at}")

            if beat.camera.follow:
                is_bone_target = (
                        beat.camera.follow.endswith("_FEET") or
                        beat.camera.follow.endswith("_HEAD")
                )

                if not is_bone_target:
                    if beat.camera.follow not in allowed_characters and beat.camera.follow not in allowed_objects:
                        raise ValueError(f"Invalid follow target in beat {beat.beat_id}: {beat.camera.follow}")

        return script


class MultiAgentDirectorBrain:
    def __init__(self) -> None:
        self.story_agent = StoryUnderstandingAgent()
        self.director_agent = DirectorAgent()
        self.blocking_agent = BlockingAgent()
        self.cinematographer_agent = CinematographerAgent()
        self.editor_agent = EditorAgent()
        self.validator_agent = UnitySafetyValidatorAgent()

    def generate(self, scene_context: Dict[str, Any], story: str) -> UniversalBeatScript:
        analysis = self.story_agent.analyze(scene_context, story)
        beat_plans = self.director_agent.create_beat_plan(analysis)

        beats: List[Beat] = []

        for plan in beat_plans:
            blocking = self.blocking_agent.create_blocking(plan, analysis, scene_context)
            camera = self.cinematographer_agent.create_camera_plan(plan, blocking, analysis, scene_context)
            transition = self.editor_agent.refine(plan)

            character_instruction = None

            if blocking:
                character_instruction = CharacterInstruction(
                    name=blocking.character_name,
                    animation=blocking.animation,
                    start_position=blocking.start_position,
                    end_position=blocking.end_position,
                    facing_y=blocking.facing_y,
                    move_speed=blocking.move_speed,
                )

            beat = Beat(
                beat_id=plan.beat_id,
                purpose=plan.purpose,
                speaker=plan.speaker,
                dialogue=plan.dialogue,
                action=plan.action,
                emotion=plan.emotion,
                intent=plan.intent,
                duration=plan.duration,
                character=character_instruction,
                camera=CameraInstruction(
                    name=camera.name,
                    shot_type=camera.shot_type,
                    position=camera.position,
                    rotation=camera.rotation,
                    fov=camera.fov,
                    look_at=camera.look_at,
                    follow=camera.follow,
                    offset=camera.offset,
                    movement=camera.movement,
                    force_world_position=camera.force_world_position,
                ),
                transition=transition,
            )

            beats.append(beat)

        script = UniversalBeatScript(
            project="CineAI Director",
            scene_id="scene_001",
            scene_title="Generated Cutscene",
            characters=scene_context.get("characters", []),
            objects=scene_context.get("objects", []),
            locations=scene_context.get("locations", []),
            beats=beats,
        )

        return self.validator_agent.validate_names(script, scene_context)


class LocalDirectorBrain(MultiAgentDirectorBrain):
    pass


def universal_script_from_dict(data: Dict[str, Any]) -> UniversalBeatScript:
    raise NotImplementedError("LLM JSON parsing will be added in the next phase.")