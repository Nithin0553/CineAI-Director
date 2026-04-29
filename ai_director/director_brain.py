from __future__ import annotations

from dataclasses import dataclass
from math import atan2, degrees, sqrt
from typing import Any, Dict, List, Optional

from cinematic_knowledge import CinematicKnowledgeBase, ShotTemplate
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

        return "Strange, where did this come from?"


class DirectorAgent:
    """
    Director planner.

    Current behavior:
    - Builds an LLM prompt.
    - Uses LLMDirector.
    - LLMDirector currently has a mock fallback, so the project runs without API keys.
    - Later, set use_mock=False and connect _call_llm().
    """

    def __init__(self, knowledge: CinematicKnowledgeBase, use_mock_llm: bool = True) -> None:
        self.knowledge = knowledge
        self.prompt_builder = DirectorPromptBuilder()
        self.llm_director = LLMDirector(use_mock=use_mock_llm)

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
            }

        return prompt_templates

    def _beat_plans_from_llm_json(
            self,
            data: Dict[str, Any],
            analysis: StoryAnalysis,
    ) -> List[BeatPlan]:
        raw_beats = data.get("beats", [])

        if not raw_beats:
            raise ValueError("LLM plan did not contain any beats.")

        beat_plans: List[BeatPlan] = []

        for index, raw in enumerate(raw_beats, start=1):
            template_id = str(raw.get("template_id", "")).strip()

            if template_id not in self.knowledge.templates:
                template_id = "medium_walk_approach"

            target_role = str(raw.get("target_role", "character")).strip()
            if target_role not in {"character", "object", "feet", "head", "body"}:
                target_role = "character"

            speaker = str(raw.get("speaker", "")).strip()

            if speaker and speaker not in [analysis.main_character]:
                speaker = analysis.main_character

            beat_plans.append(
                BeatPlan(
                    beat_id=int(raw.get("beat_id", index)),
                    purpose=str(raw.get("purpose", "Cinematic beat.")),
                    action=str(raw.get("action", "")),
                    emotion=str(raw.get("emotion", "neutral")),
                    intent=str(raw.get("intent", "observe")),
                    speaker=speaker,
                    dialogue=str(raw.get("dialogue", "")),
                    template_id=template_id,
                    target_role=target_role,
                    duration=float(raw.get("duration", 4.0)),
                    transition=str(raw.get("transition", "cut")),
                )
            )

        return beat_plans


class BlockingAgent:
    """
    Temporary blocking planner.

    This still uses simple scene_context positions.
    Later, this should also come from LLM output or a scene-aware planner.
    """

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

        if beat_plan.speaker != c:
            return None

        if beat_plan.intent == "approach" and beat_plan.target_role == "feet":
            return BlockingPlan(
                character_name=c,
                animation="Walk",
                start_position=start,
                end_position=middle,
                facing_y=180.0,
                move_speed=1.0,
            )

        if beat_plan.intent == "approach":
            return BlockingPlan(
                character_name=c,
                animation="Walk",
                start_position=middle,
                end_position=end,
                facing_y=180.0,
                move_speed=0.8,
            )

        if beat_plan.intent in {"reveal", "observe"}:
            return BlockingPlan(
                character_name=c,
                animation="Idle",
                start_position=end,
                end_position=None,
                facing_y=180.0,
                move_speed=None,
            )

        if beat_plan.intent in {"question", "react", "dialogue"}:
            return BlockingPlan(
                character_name=c,
                animation="Reaction",
                start_position=end,
                end_position=None,
                facing_y=180.0,
                move_speed=None,
            )

        return BlockingPlan(
            character_name=c,
            animation="Idle",
            start_position=end,
            end_position=None,
            facing_y=180.0,
            move_speed=None,
        )

    @staticmethod
    def _vec(data: Dict[str, Any]) -> Vector3:
        return Vector3(float(data["x"]), float(data["y"]), float(data["z"]))


class CinematographerAgent:
    def __init__(self, knowledge: CinematicKnowledgeBase) -> None:
        self.knowledge = knowledge

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

        if target_role == "body":
            return f"{character}_BODY"

        return character

    @staticmethod
    def _resolve_follow_name(target_role: str, character: str, obj: str) -> Optional[str]:
        if target_role == "object":
            return None

        if target_role == "feet":
            return f"{character}_FEET"

        if target_role == "head":
            return f"{character}_HEAD"

        if target_role == "body":
            return f"{character}_BODY"

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