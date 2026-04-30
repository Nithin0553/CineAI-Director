from __future__ import annotations

from dataclasses import dataclass
from math import atan2, degrees, sqrt
from typing import Any, Dict, List, Optional

from camera_parameter_normalizer import CameraParameterNormalizer
from cinematic_knowledge import CinematicKnowledgeBase
from llm_director import LLMDirector
from prompt_builder import DirectorPromptBuilder
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
from semantic_normalizer import SemanticNormalizer


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
    raw_camera: Optional[Dict[str, Any]]


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
            emotion_arc=["neutral"],
            story_intent="llm_planned_scene",
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

        return ""


class DirectorAgent:
    def __init__(self, knowledge: CinematicKnowledgeBase, use_mock_llm: bool = True) -> None:
        self.knowledge = knowledge
        self.prompt_builder = DirectorPromptBuilder()
        self.llm_director = LLMDirector(use_mock=use_mock_llm)
        self.normalizer = SemanticNormalizer(
            available_template_ids=list(self.knowledge.templates.keys())
        )

    def create_beat_plan(
            self,
            analysis: StoryAnalysis,
            scene_context: Dict[str, Any],
            story: str,
    ) -> List[BeatPlan]:
        prompt = self.prompt_builder.build_prompt(
            story=story,
            scene_context=scene_context,
            cinematic_templates=self._templates_for_prompt(),
        )

        llm_plan = self.llm_director.generate_plan(
            prompt=prompt,
            scene_context=scene_context,
            story=story,
        )

        return self._beat_plans_from_llm_json(llm_plan, analysis)

    def _templates_for_prompt(self) -> Dict[str, Any]:
        prompt_templates: Dict[str, Any] = {}

        for template_id, template in self.knowledge.templates.items():
            prompt_templates[template_id] = {
                "description": template.description,
                "shot_type": template.shot_type,
                "camera_movement": template.camera_movement,
                "fov": template.fov,
                "offset": template.offset.to_dict() if template.offset else None,
                "force_world_position": template.force_world_position,
                "movement_speed": template.movement_speed,
                "movement_radius": template.movement_radius,
            }

        return prompt_templates

    def _beat_plans_from_llm_json(
            self,
            data: Dict[str, Any],
            analysis: StoryAnalysis,
    ) -> List[BeatPlan]:
        raw_beats = data.get("beats", [])

        normalized_beats = self.normalizer.normalize(
            raw_beats=raw_beats,
            main_character=analysis.main_character,
            main_object=analysis.main_object,
            fallback_dialogue=analysis.dialogue,
        )

        beat_plans: List[BeatPlan] = []

        for index, raw in enumerate(normalized_beats, start=1):
            beat_plans.append(
                BeatPlan(
                    beat_id=int(raw.get("beat_id", index)),
                    purpose=str(raw.get("purpose", "Cinematic beat.")),
                    action=str(raw.get("action", "")),
                    emotion=str(raw.get("emotion", "neutral")),
                    intent=str(raw.get("intent", "observe")),
                    speaker=str(raw.get("speaker", "")),
                    dialogue=str(raw.get("dialogue", "")),
                    template_id=str(raw.get("template_id", "medium_walk_approach")),
                    target_role=str(raw.get("target_role", "character")),
                    duration=float(raw.get("duration", 4.0)),
                    transition=str(raw.get("transition", "cut")),
                    raw_camera=raw.get("camera") if isinstance(raw.get("camera"), dict) else None,
                )
            )

        return beat_plans


class BlockingAgent:
    """
    Dynamic scene-aware blocking.

    It no longer uses a fixed lane. It computes the character path relative to
    the target object position, so it can work for other scenes/games too.
    """

    def create_blocking(
            self,
            beat_plan: BeatPlan,
            analysis: StoryAnalysis,
            scene_context: Dict[str, Any],
    ) -> Optional[BlockingPlan]:
        character_name = analysis.main_character

        if beat_plan.speaker != character_name:
            return None

        object_pos = self._vec(
            scene_context.get("default_object_position", {"x": -5.0, "y": 0.0, "z": -3.0})
        )

        scene_start = self._vec(
            scene_context.get("default_character_start", {"x": -5.0, "y": 0.0, "z": 12.0})
        )

        stop_distance = float(scene_context.get("approach_stop_distance", 2.0))
        middle_distance = float(scene_context.get("approach_middle_distance", 6.0))
        start_distance = float(scene_context.get("approach_start_distance", 12.0))

        approach_dir = self._horizontal_direction_from_object_to_start(
            object_pos=object_pos,
            scene_start=scene_start,
        )

        approach_start = self._point_from_object(object_pos, approach_dir, start_distance, scene_start.y)
        approach_middle = self._point_from_object(object_pos, approach_dir, middle_distance, scene_start.y)
        approach_end = self._point_from_object(object_pos, approach_dir, stop_distance, scene_start.y)

        facing_y = self._yaw_toward(
            from_pos=approach_end,
            to_pos=object_pos,
        )

        if beat_plan.intent == "approach" and beat_plan.target_role == "feet":
            return BlockingPlan(
                character_name=character_name,
                animation="Walk",
                start_position=approach_start,
                end_position=approach_middle,
                facing_y=facing_y,
                move_speed=1.0,
            )

        if beat_plan.intent == "approach":
            return BlockingPlan(
                character_name=character_name,
                animation="Walk",
                start_position=approach_middle,
                end_position=approach_end,
                facing_y=facing_y,
                move_speed=0.8,
            )

        if beat_plan.intent in {"reveal", "observe", "insert"}:
            return BlockingPlan(
                character_name=character_name,
                animation="Idle",
                start_position=approach_end,
                end_position=None,
                facing_y=facing_y,
                move_speed=None,
            )

        if beat_plan.intent in {"question", "react", "dialogue"}:
            return BlockingPlan(
                character_name=character_name,
                animation="Reaction",
                start_position=approach_end,
                end_position=None,
                facing_y=facing_y,
                move_speed=None,
            )

        return BlockingPlan(
            character_name=character_name,
            animation="Idle",
            start_position=approach_end,
            end_position=None,
            facing_y=facing_y,
            move_speed=None,
        )

    @staticmethod
    def _vec(data: Dict[str, Any]) -> Vector3:
        return Vector3(float(data["x"]), float(data["y"]), float(data["z"]))

    @staticmethod
    def _horizontal_direction_from_object_to_start(object_pos: Vector3, scene_start: Vector3) -> Vector3:
        dx = scene_start.x - object_pos.x
        dz = scene_start.z - object_pos.z
        length = sqrt(dx * dx + dz * dz)

        if length < 0.001:
            return Vector3(0.0, 0.0, 1.0)

        return Vector3(dx / length, 0.0, dz / length)

    @staticmethod
    def _point_from_object(object_pos: Vector3, direction: Vector3, distance: float, y: float) -> Vector3:
        return Vector3(
            object_pos.x + direction.x * distance,
            y,
            object_pos.z + direction.z * distance,
            )

    @staticmethod
    def _yaw_toward(from_pos: Vector3, to_pos: Vector3) -> float:
        dx = to_pos.x - from_pos.x
        dz = to_pos.z - from_pos.z

        if abs(dx) < 0.001 and abs(dz) < 0.001:
            return 0.0

        return degrees(atan2(dx, dz))


class CinematographerAgent:
    def __init__(self, knowledge: CinematicKnowledgeBase) -> None:
        self.knowledge = knowledge
        self.camera_normalizer = CameraParameterNormalizer()

    def create_camera_plan(
            self,
            beat_plan: BeatPlan,
            blocking: Optional[BlockingPlan],
            analysis: StoryAnalysis,
            scene_context: Dict[str, Any],
    ) -> CameraPlan:
        fallback_template = self.knowledge.get(beat_plan.template_id)

        camera_params = self.camera_normalizer.normalize(
            raw_camera=beat_plan.raw_camera,
            fallback_template=fallback_template,
            semantic_target_role=beat_plan.target_role,
        )

        c = analysis.main_character
        o = analysis.main_object

        look_at = self._resolve_role(camera_params.look_at_role, c, o)
        follow = self._resolve_role(camera_params.follow_role, c, o) if camera_params.follow_role else None

        camera_params.movement.target = follow or look_at

        return CameraPlan(
            name=f"VCam_{beat_plan.beat_id}",
            shot_type=camera_params.shot_type,
            position=camera_params.position,
            rotation=camera_params.rotation,
            fov=camera_params.fov,
            look_at=look_at,
            follow=follow,
            offset=camera_params.offset,
            movement=camera_params.movement,
            force_world_position=camera_params.force_world_position,
        )

    @staticmethod
    def _resolve_role(role: Optional[str], character: str, obj: str) -> Optional[str]:
        if role is None:
            return None

        if role == "object":
            return obj

        if role == "feet":
            return f"{character}_FEET"

        if role == "head":
            return f"{character}_HEAD"

        if role == "body":
            return f"{character}_BODY"

        if role == "character":
            return character

        return character


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
                        beat.camera.look_at.endswith("_FEET")
                        or beat.camera.look_at.endswith("_HEAD")
                        or beat.camera.look_at.endswith("_BODY")
                )

                if not is_anchor_target:
                    if beat.camera.look_at not in allowed_characters and beat.camera.look_at not in allowed_objects:
                        raise ValueError(f"Invalid look_at target in beat {beat.beat_id}: {beat.camera.look_at}")

            if beat.camera.follow:
                is_anchor_target = (
                        beat.camera.follow.endswith("_FEET")
                        or beat.camera.follow.endswith("_HEAD")
                        or beat.camera.follow.endswith("_BODY")
                )

                if not is_anchor_target:
                    if beat.camera.follow not in allowed_characters and beat.camera.follow not in allowed_objects:
                        raise ValueError(f"Invalid follow target in beat {beat.beat_id}: {beat.camera.follow}")

        return script


class MultiAgentDirectorBrain:
    def __init__(self, use_mock_llm: bool = True) -> None:
        self.knowledge = CinematicKnowledgeBase()

        self.story_agent = StoryUnderstandingAgent()
        self.director_agent = DirectorAgent(
            knowledge=self.knowledge,
            use_mock_llm=use_mock_llm,
        )
        self.blocking_agent = BlockingAgent()
        self.cinematographer_agent = CinematographerAgent(self.knowledge)
        self.editor_agent = EditorAgent()
        self.validator_agent = UnitySafetyValidatorAgent()

    def generate(self, scene_context: Dict[str, Any], story: str) -> UniversalBeatScript:
        analysis = self.story_agent.analyze(scene_context, story)

        beat_plans = self.director_agent.create_beat_plan(
            analysis=analysis,
            scene_context=scene_context,
            story=story,
        )

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