from __future__ import annotations

from dataclasses import dataclass
from math import atan2, degrees, sqrt
from typing import Any, Dict, List, Optional

from cinematic_knowledge import CinematicKnowledgeBase, ShotTemplate
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

    template_id: str
    target_role: str

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
    """
    Temporary director planner.

    This is still not the final LLM brain.
    But now it outputs template IDs instead of hardcoding every camera value here.
    Later, an LLM can generate these BeatPlans directly.
    """

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
                template_id="aerial_object_orbit",
                target_role="object",
                duration=5.0,
                transition="cut",
            ),
            BeatPlan(
                beat_id=2,
                purpose="Show the character entering through movement detail.",
                action=f"Low detail shot follows {c}'s feet as he walks toward {o}.",
                emotion="focused",
                intent="approach",
                speaker=c,
                dialogue="",
                template_id="feet_follow_detail",
                target_role="feet",
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
                template_id="medium_walk_approach",
                target_role="character",
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
                template_id="over_shoulder_reveal",
                target_role="object",
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
                template_id="face_reaction_closeup",
                target_role="head",
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
    def __init__(self) -> None:
        self.knowledge = CinematicKnowledgeBase()

    def create_camera_plan(
            self,
            beat_plan: BeatPlan,
            blocking: Optional[BlockingPlan],
            analysis: StoryAnalysis,
            scene_context: Dict[str, Any],
    ) -> CameraPlan:
        template = self.knowledge.get(beat_plan.template_id)

        c = analysis.main_character
        o = analysis.main_object

        look_at = self._resolve_target_name(beat_plan.target_role, c, o)
        follow = self._resolve_follow_name(beat_plan.target_role, c, o)

        position = template.position
        rotation = template.rotation

        if template.force_world_position:
            object_pos = self._vec(scene_context.get("default_object_position", {"x": -5.0, "y": 0.0, "z": -3.0}))
            position = Vector3(
                object_pos.x + template.position.x,
                object_pos.y + template.position.y,
                object_pos.z + template.position.z,
                )
            rotation = self._look_at_rotation(position, object_pos)

        movement_target = follow or look_at

        return self._camera_from_template(
            beat_id=beat_plan.beat_id,
            template=template,
            look_at=look_at,
            follow=follow,
            movement_target=movement_target,
            position=position,
            rotation=rotation,
        )

    def _camera_from_template(
            self,
            beat_id: int,
            template: ShotTemplate,
            look_at: Optional[str],
            follow: Optional[str],
            movement_target: Optional[str],
            position: Vector3,
            rotation: Rotation,
    ) -> CameraPlan:
        return CameraPlan(
            name=f"VCam_{beat_id}",
            shot_type=template.shot_type,
            position=position,
            rotation=rotation,
            fov=template.fov,
            look_at=look_at,
            follow=follow,
            offset=template.offset,
            movement=self.knowledge.movement_for(template, movement_target),
            force_world_position=template.force_world_position,
        )

    @staticmethod
    def _resolve_target_name(target_role: str, character: str, obj: str) -> str:
        if target_role == "object":
            return obj

        if target_role == "feet":
            return f"{character}_FEET"

        if target_role == "head":
            return f"{character}_HEAD"

        return character

    @staticmethod
    def _resolve_follow_name(target_role: str, character: str, obj: str) -> Optional[str]:
        if target_role == "object":
            return None

        if target_role == "feet":
            return f"{character}_FEET"

        if target_role == "head":
            return f"{character}_HEAD"

        return character

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
                is_anchor_target = (
                        beat.camera.look_at.endswith("_FEET") or
                        beat.camera.look_at.endswith("_HEAD") or
                        beat.camera.look_at.endswith("_BODY")
                )

                if not is_anchor_target:
                    if beat.camera.look_at not in allowed_characters and beat.camera.look_at not in allowed_objects:
                        raise ValueError(f"Invalid look_at target in beat {beat.beat_id}: {beat.camera.look_at}")

            if beat.camera.follow:
                is_anchor_target = (
                        beat.camera.follow.endswith("_FEET") or
                        beat.camera.follow.endswith("_HEAD") or
                        beat.camera.follow.endswith("_BODY")
                )

                if not is_anchor_target:
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