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
    raw_character: Optional[Dict[str, Any]]


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


class SceneContextHelper:
    def __init__(self, scene_context: Dict[str, Any]) -> None:
        self.scene_context = scene_context

    def character_names(self) -> List[str]:
        names = self.scene_context.get("character_names")
        if isinstance(names, list) and names:
            return [str(n) for n in names]

        old = self.scene_context.get("characters", [])
        if old and isinstance(old[0], str):
            return [str(n) for n in old]

        if old and isinstance(old[0], dict):
            return [str(c.get("name", "")) for c in old if c.get("name")]

        return []

    def object_names(self) -> List[str]:
        names = self.scene_context.get("object_names")
        if isinstance(names, list) and names:
            return [str(n) for n in names]

        old = self.scene_context.get("objects", [])
        if old and isinstance(old[0], str):
            return [str(n) for n in old]

        if old and isinstance(old[0], dict):
            return [str(o.get("name", "")) for o in old if o.get("name")]

        return []

    def locations(self) -> List[str]:
        locs = self.scene_context.get("locations", [])
        return [str(l) for l in locs] if isinstance(locs, list) else []

    def get_entity(self, name: str) -> Optional[Dict[str, Any]]:
        for group in ("characters", "objects", "environment_surfaces"):
            items = self.scene_context.get(group, [])
            if not isinstance(items, list):
                continue

            for item in items:
                if isinstance(item, dict) and self._same_name(str(item.get("name", "")), name):
                    return item

        return None

    def character_ground_position(self, name: str) -> Optional[Vector3]:
        entity = self.get_entity(name)
        if not entity:
            return self._old_default_character_start()

        for key in ("estimated_feet_position", "ground_position", "bottom_center", "visual_position", "bounds_center", "transform_position"):
            vec = self._vec_from_entity(entity, key)
            if vec:
                return vec

        return self._old_default_character_start()

    def object_visual_position(self, name: str) -> Optional[Vector3]:
        entity = self.get_entity(name)
        if not entity:
            return self._old_default_object_position()

        for key in ("visual_position", "bounds_center", "ground_position", "bottom_center", "transform_position"):
            vec = self._vec_from_entity(entity, key)
            if vec:
                return vec

        return self._old_default_object_position()

    def object_ground_position(self, name: str) -> Optional[Vector3]:
        entity = self.get_entity(name)
        if entity:
            for key in ("ground_position", "bottom_center", "visual_position", "bounds_center", "transform_position"):
                vec = self._vec_from_entity(entity, key)
                if vec:
                    return vec

        return self._old_default_object_position()

    def nearest_ground_to(self, point: Vector3) -> Optional[Vector3]:
        samples = self.scene_context.get("ground_samples", [])
        best = None
        best_dist = float("inf")

        if isinstance(samples, list):
            for sample in samples:
                if not isinstance(sample, dict):
                    continue

                vec = self._vec(sample.get("world_position"))
                if not vec:
                    continue

                d = self._horizontal_distance(point, vec)
                if d < best_dist:
                    best_dist = d
                    best = vec

        return best

    def _old_default_character_start(self) -> Optional[Vector3]:
        return self._vec(self.scene_context.get("default_character_start"))

    def _old_default_object_position(self) -> Optional[Vector3]:
        return self._vec(self.scene_context.get("default_object_position"))

    @staticmethod
    def _same_name(a: str, b: str) -> bool:
        return SceneContextHelper._norm(a) == SceneContextHelper._norm(b)

    @staticmethod
    def _norm(value: str) -> str:
        return value.strip().lower().replace("_", "").replace(" ", "").replace("-", "")

    @staticmethod
    def _vec_from_entity(entity: Dict[str, Any], key: str) -> Optional[Vector3]:
        return SceneContextHelper._vec(entity.get(key))

    @staticmethod
    def _vec(data: Any) -> Optional[Vector3]:
        if not isinstance(data, dict):
            return None

        if not all(k in data for k in ("x", "y", "z")):
            return None

        return Vector3(float(data["x"]), float(data["y"]), float(data["z"]))

    @staticmethod
    def _horizontal_distance(a: Vector3, b: Vector3) -> float:
        dx = a.x - b.x
        dz = a.z - b.z
        return sqrt(dx * dx + dz * dz)


class StoryUnderstandingAgent:
    def analyze(self, scene_context: Dict[str, Any], story: str) -> StoryAnalysis:
        helper = SceneContextHelper(scene_context)

        characters = helper.character_names()
        objects = helper.object_names()
        locations = helper.locations()

        main_character = characters[0] if characters else "CHARACTER"
        main_object = objects[0] if objects else "OBJECT"
        location = locations[0] if locations else scene_context.get("scene_name", "LOCATION")
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
                    raw_character=raw.get("character") if isinstance(raw.get("character"), dict) else None,
                )
            )

        return beat_plans


class BlockingAgent:
    def create_blocking(
            self,
            beat_plan: BeatPlan,
            analysis: StoryAnalysis,
            scene_context: Dict[str, Any],
    ) -> Optional[BlockingPlan]:
        if beat_plan.speaker != analysis.main_character:
            return None

        if beat_plan.raw_character:
            parsed = self._blocking_from_llm_character(beat_plan.raw_character, analysis)
            if parsed:
                return parsed

        helper = SceneContextHelper(scene_context)

        character_start = helper.character_ground_position(analysis.main_character)
        object_position = helper.object_ground_position(analysis.main_object)

        if character_start is None or object_position is None:
            return None

        direction = self._horizontal_direction_from_object_to_start(
            object_pos=object_position,
            scene_start=character_start,
        )

        scene_distance = self._horizontal_distance(character_start, object_position)

        if scene_distance <= MathfLike.epsilon():
            start_position = character_start
            end_position = character_start
        else:
            start_position = character_start
            end_position = self._interpolate_horizontal(
                start=character_start,
                end=object_position,
                t=self._intent_progress(beat_plan.intent),
            )

            nearest_ground = helper.nearest_ground_to(end_position)
            if nearest_ground:
                end_position.y = nearest_ground.y

        facing_y = self._yaw_toward(
            from_pos=end_position,
            to_pos=object_position,
        )

        animation = self._animation_for_intent(beat_plan.intent, beat_plan.action)
        should_move = beat_plan.intent == "approach"

        return BlockingPlan(
            character_name=analysis.main_character,
            animation=animation,
            start_position=start_position,
            end_position=end_position if should_move else None,
            facing_y=facing_y,
            move_speed=None,
        )

    def _blocking_from_llm_character(
            self,
            raw_character: Dict[str, Any],
            analysis: StoryAnalysis,
    ) -> Optional[BlockingPlan]:
        name = str(raw_character.get("name") or analysis.main_character)
        animation = str(raw_character.get("animation") or "Idle")

        start = self._vec(raw_character.get("start_position"))
        end = self._vec(raw_character.get("end_position"))

        facing = raw_character.get("facing_y")
        facing_y = float(facing) if facing is not None else None

        speed = raw_character.get("move_speed")
        move_speed = float(speed) if speed is not None else None

        return BlockingPlan(
            character_name=name,
            animation=animation,
            start_position=start,
            end_position=end,
            facing_y=facing_y,
            move_speed=move_speed,
        )

    @staticmethod
    def _vec(data: Any) -> Optional[Vector3]:
        if not isinstance(data, dict):
            return None

        if not all(k in data for k in ("x", "y", "z")):
            return None

        return Vector3(float(data["x"]), float(data["y"]), float(data["z"]))

    @staticmethod
    def _animation_for_intent(intent: str, action: str) -> str:
        text = f"{intent} {action}".lower()

        if "walk" in text or "approach" in text or "move" in text:
            return "Walking"

        if "run" in text:
            return "Running"

        if "react" in text or "confused" in text or "shock" in text:
            return "Reaction"

        return "Idle"

    @staticmethod
    def _intent_progress(intent: str) -> float:
        if intent == "approach":
            return 0.7

        if intent in {"reveal", "observe", "insert"}:
            return 0.85

        if intent in {"react", "question", "dialogue"}:
            return 0.85

        return 0.5

    @staticmethod
    def _horizontal_direction_from_object_to_start(object_pos: Vector3, scene_start: Vector3) -> Vector3:
        dx = scene_start.x - object_pos.x
        dz = scene_start.z - object_pos.z
        length = sqrt(dx * dx + dz * dz)

        if length < MathfLike.epsilon():
            return Vector3(0.0, 0.0, 1.0)

        return Vector3(dx / length, 0.0, dz / length)

    @staticmethod
    def _interpolate_horizontal(start: Vector3, end: Vector3, t: float) -> Vector3:
        return Vector3(
            start.x + (end.x - start.x) * t,
            start.y,
            start.z + (end.z - start.z) * t,
            )

    @staticmethod
    def _horizontal_distance(a: Vector3, b: Vector3) -> float:
        dx = a.x - b.x
        dz = a.z - b.z
        return sqrt(dx * dx + dz * dz)

    @staticmethod
    def _yaw_toward(from_pos: Vector3, to_pos: Vector3) -> float:
        dx = to_pos.x - from_pos.x
        dz = to_pos.z - from_pos.z

        if abs(dx) < MathfLike.epsilon() and abs(dz) < MathfLike.epsilon():
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
        helper = SceneContextHelper(scene_context)
        allowed_characters = set(helper.character_names())
        allowed_objects = set(helper.object_names())

        for beat in script.beats:
            if beat.character and beat.character.name not in allowed_characters:
                raise ValueError(f"Invalid character name in beat {beat.beat_id}: {beat.character.name}")

            if beat.speaker and beat.speaker not in allowed_characters:
                raise ValueError(f"Invalid speaker name in beat {beat.beat_id}: {beat.speaker}")

            for target in (beat.camera.look_at, beat.camera.follow):
                if not target:
                    continue

                is_anchor_target = (
                        target.endswith("_FEET")
                        or target.endswith("_HEAD")
                        or target.endswith("_BODY")
                )

                if not is_anchor_target and target not in allowed_characters and target not in allowed_objects:
                    raise ValueError(f"Invalid camera target in beat {beat.beat_id}: {target}")

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

        helper = SceneContextHelper(scene_context)

        script = UniversalBeatScript(
            project="CineAI Director",
            scene_id="scene_001",
            scene_title="Generated Cutscene",
            characters=helper.character_names(),
            objects=helper.object_names(),
            locations=helper.locations(),
            beats=beats,
        )

        return self.validator_agent.validate_names(script, scene_context)


class LocalDirectorBrain(MultiAgentDirectorBrain):
    pass


class MathfLike:
    @staticmethod
    def epsilon() -> float:
        return 1e-6


def universal_script_from_dict(data: Dict[str, Any]) -> UniversalBeatScript:
    raise NotImplementedError("LLM JSON parsing will be added in the next phase.")