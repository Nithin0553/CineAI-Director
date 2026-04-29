from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Dict, List


VALID_TARGET_ROLES = {"character", "object", "feet", "head", "body"}

VALID_INTENTS = {
    "establish",
    "approach",
    "reveal",
    "react",
    "question",
    "dialogue",
    "observe",
    "insert",
    "exit",
}

INTENT_TO_TEMPLATE = {
    "establish_scene": "aerial_object_orbit",
    "movement_detail": "feet_follow_detail",
    "character_movement": "medium_walk_approach",
    "object_reveal": "over_shoulder_reveal",
    "object_insert": "object_insert",
    "character_reaction": "face_reaction_closeup",
    "dialogue_or_thought": "face_reaction_closeup",
}


@dataclass
class NormalizedBeat:
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

    def to_dict(self) -> Dict[str, Any]:
        return {
            "beat_id": self.beat_id,
            "purpose": self.purpose,
            "action": self.action,
            "emotion": self.emotion,
            "intent": self.intent,
            "speaker": self.speaker,
            "dialogue": self.dialogue,
            "template_id": self.template_id,
            "target_role": self.target_role,
            "duration": self.duration,
            "transition": self.transition,
        }


class SemanticNormalizer:
    """
    Universal LLM-output normalizer.

    This is not tied to one game, one scene, or one story.
    It converts weak LLM output into generic cinematic categories.
    """

    def __init__(self, available_template_ids: List[str]) -> None:
        self.available_template_ids = set(available_template_ids)

    def normalize(
            self,
            raw_beats: List[Dict[str, Any]],
            main_character: str,
            main_object: str,
            fallback_dialogue: str,
    ) -> List[Dict[str, Any]]:
        if not raw_beats:
            return self._fallback_sequence(main_character, main_object, fallback_dialogue)

        normalized: List[NormalizedBeat] = []

        has_establishing = False
        has_movement_detail = False
        has_character_movement = False
        has_reaction = False

        for index, raw in enumerate(raw_beats, start=1):
            semantic_type = self._semantic_type(raw)

            if semantic_type == "establish_scene":
                has_establishing = True

            if semantic_type == "movement_detail":
                has_movement_detail = True

            if semantic_type == "character_movement":
                has_character_movement = True

            if semantic_type in {"character_reaction", "dialogue_or_thought"}:
                has_reaction = True

            normalized.append(
                self._normalize_single(
                    raw=raw,
                    beat_id=index,
                    semantic_type=semantic_type,
                    main_character=main_character,
                    main_object=main_object,
                    fallback_dialogue=fallback_dialogue,
                )
            )

        if not has_establishing:
            normalized.insert(
                0,
                self._make_beat(
                    beat_id=1,
                    semantic_type="establish_scene",
                    main_character=main_character,
                    main_object=main_object,
                    fallback_dialogue="",
                ),
            )

        if self._story_has_character_motion(raw_beats):
            if not has_movement_detail:
                normalized.insert(
                    1 if normalized else 0,
                    self._make_beat(
                        beat_id=2,
                        semantic_type="movement_detail",
                        main_character=main_character,
                        main_object=main_object,
                        fallback_dialogue="",
                    ),
                )

            if not has_character_movement:
                normalized.insert(
                    min(2, len(normalized)),
                    self._make_beat(
                        beat_id=3,
                        semantic_type="character_movement",
                        main_character=main_character,
                        main_object=main_object,
                        fallback_dialogue="",
                    ),
                )

        if not has_reaction:
            normalized.append(
                self._make_beat(
                    beat_id=len(normalized) + 1,
                    semantic_type="dialogue_or_thought",
                    main_character=main_character,
                    main_object=main_object,
                    fallback_dialogue=fallback_dialogue,
                )
            )

        for new_id, beat in enumerate(normalized, start=1):
            beat.beat_id = new_id

        return [beat.to_dict() for beat in normalized]

    def _normalize_single(
            self,
            raw: Dict[str, Any],
            beat_id: int,
            semantic_type: str,
            main_character: str,
            main_object: str,
            fallback_dialogue: str,
    ) -> NormalizedBeat:
        template_id = str(raw.get("template_id", "")).strip()

        if template_id not in self.available_template_ids:
            template_id = INTENT_TO_TEMPLATE.get(semantic_type, "medium_walk_approach")

        if template_id not in self.available_template_ids:
            template_id = next(iter(self.available_template_ids))

        target_role = self._target_role_for_semantic_type(
            semantic_type=semantic_type,
            proposed=str(raw.get("target_role", "")).strip(),
        )

        intent = self._intent_for_semantic_type(
            semantic_type=semantic_type,
            proposed=str(raw.get("intent", "")).strip(),
        )

        speaker = str(raw.get("speaker", "")).strip()

        if target_role in {"character", "feet", "head", "body"}:
            speaker = main_character
        elif semantic_type == "object_reveal":
            speaker = main_character
        elif speaker and speaker != main_character:
            speaker = main_character

        dialogue = str(raw.get("dialogue", "")).strip()

        if semantic_type == "dialogue_or_thought" and not dialogue:
            dialogue = fallback_dialogue

        emotion = str(raw.get("emotion", "neutral")).strip().lower() or "neutral"

        return NormalizedBeat(
            beat_id=beat_id,
            purpose=str(raw.get("purpose", self._default_purpose(semantic_type, main_character, main_object))),
            action=str(raw.get("action", self._default_action(semantic_type, main_character, main_object))),
            emotion=emotion,
            intent=intent,
            speaker=speaker,
            dialogue=dialogue,
            template_id=template_id,
            target_role=target_role,
            duration=float(raw.get("duration", self._default_duration(semantic_type))),
            transition=str(raw.get("transition", "cut")).strip() or "cut",
        )

    def _make_beat(
            self,
            beat_id: int,
            semantic_type: str,
            main_character: str,
            main_object: str,
            fallback_dialogue: str,
    ) -> NormalizedBeat:
        template_id = INTENT_TO_TEMPLATE.get(semantic_type, "medium_walk_approach")

        if template_id not in self.available_template_ids:
            template_id = next(iter(self.available_template_ids))

        target_role = self._target_role_for_semantic_type(semantic_type, "")
        intent = self._intent_for_semantic_type(semantic_type, "")

        speaker = ""

        if target_role in {"character", "feet", "head", "body"}:
            speaker = main_character
        elif semantic_type == "object_reveal":
            speaker = main_character

        dialogue = fallback_dialogue if semantic_type == "dialogue_or_thought" else ""

        return NormalizedBeat(
            beat_id=beat_id,
            purpose=self._default_purpose(semantic_type, main_character, main_object),
            action=self._default_action(semantic_type, main_character, main_object),
            emotion=self._default_emotion(semantic_type),
            intent=intent,
            speaker=speaker,
            dialogue=dialogue,
            template_id=template_id,
            target_role=target_role,
            duration=self._default_duration(semantic_type),
            transition="fade" if semantic_type == "dialogue_or_thought" else "cut",
        )

    def _semantic_type(self, raw: Dict[str, Any]) -> str:
        proposed_intent = str(raw.get("intent", "")).strip().lower()
        proposed_template = str(raw.get("template_id", "")).strip()
        proposed_target_role = str(raw.get("target_role", "")).strip().lower()
        dialogue = str(raw.get("dialogue", "")).strip()

        text = " ".join(
            [
                str(raw.get("purpose", "")),
                str(raw.get("action", "")),
                proposed_intent,
                str(raw.get("emotion", "")),
            ]
        ).lower()

        if proposed_template == "aerial_object_orbit":
            return "establish_scene"

        if proposed_template == "feet_follow_detail":
            return "movement_detail"

        if proposed_template == "medium_walk_approach":
            return "character_movement"

        if proposed_template == "over_shoulder_reveal":
            return "object_reveal"

        if proposed_template == "object_insert":
            return "object_insert"

        if proposed_template == "face_reaction_closeup":
            return "dialogue_or_thought" if dialogue else "character_reaction"

        if proposed_intent == "establish":
            return "establish_scene"

        if proposed_intent in {"approach", "exit"}:
            return "character_movement"

        if proposed_intent in {"reveal", "observe"} and proposed_target_role == "object":
            return "object_reveal"

        if proposed_intent == "insert":
            return "object_insert"

        if proposed_intent == "react":
            return "character_reaction"

        if proposed_intent in {"question", "dialogue"} or dialogue:
            return "dialogue_or_thought"

        if self._contains_any(text, {"walk", "move", "run", "enter", "exit", "approach", "follow", "stop"}):
            return "character_movement"

        if self._contains_any(text, {"establish", "wide", "location", "scene opens", "opening"}):
            return "establish_scene"

        if self._contains_any(text, {"reveal", "discover", "observe", "notice", "see", "look at"}):
            return "object_reveal"

        if self._contains_any(text, {"react", "emotion", "expression", "face", "shocked", "confused", "fearful"}):
            return "character_reaction"

        return "character_movement"

    def _target_role_for_semantic_type(self, semantic_type: str, proposed: str) -> str:
        proposed = proposed.lower()

        if proposed in VALID_TARGET_ROLES:
            if semantic_type == "movement_detail":
                return "feet"

            if semantic_type in {"character_reaction", "dialogue_or_thought"}:
                return "head"

            if semantic_type in {"establish_scene", "object_reveal", "object_insert"}:
                return "object"

            return proposed

        mapping = {
            "establish_scene": "object",
            "movement_detail": "feet",
            "character_movement": "character",
            "object_reveal": "object",
            "object_insert": "object",
            "character_reaction": "head",
            "dialogue_or_thought": "head",
        }

        return mapping.get(semantic_type, "character")

    def _intent_for_semantic_type(self, semantic_type: str, proposed: str) -> str:
        proposed = proposed.lower()

        if proposed in VALID_INTENTS:
            if semantic_type == "movement_detail":
                return "approach"

            return proposed

        mapping = {
            "establish_scene": "establish",
            "movement_detail": "approach",
            "character_movement": "approach",
            "object_reveal": "reveal",
            "object_insert": "insert",
            "character_reaction": "react",
            "dialogue_or_thought": "question",
        }

        return mapping.get(semantic_type, "observe")

    def _story_has_character_motion(self, raw_beats: List[Dict[str, Any]]) -> bool:
        for beat in raw_beats:
            semantic_type = self._semantic_type(beat)

            if semantic_type in {"movement_detail", "character_movement"}:
                return True

            text = f"{beat.get('purpose', '')} {beat.get('action', '')} {beat.get('intent', '')}".lower()

            if self._contains_any(text, {"walk", "move", "run", "enter", "exit", "approach", "stop"}):
                return True

        return False

    @staticmethod
    def _contains_any(text: str, keywords: set[str]) -> bool:
        return any(keyword in text for keyword in keywords)

    @staticmethod
    def _default_purpose(semantic_type: str, main_character: str, main_object: str) -> str:
        mapping = {
            "establish_scene": f"Establish the scene and introduce {main_object}.",
            "movement_detail": f"Show {main_character}'s movement detail.",
            "character_movement": f"Show {main_character} moving through the scene.",
            "object_reveal": f"Reveal {main_object} from the character's perspective.",
            "object_insert": f"Show an important detail of {main_object}.",
            "character_reaction": f"Show {main_character}'s emotional reaction.",
            "dialogue_or_thought": f"Show {main_character}'s thought or dialogue.",
        }

        return mapping.get(semantic_type, "Create a cinematic story beat.")

    @staticmethod
    def _default_action(semantic_type: str, main_character: str, main_object: str) -> str:
        mapping = {
            "establish_scene": f"The camera establishes {main_object} in the scene.",
            "movement_detail": f"A low detail shot follows {main_character}'s movement.",
            "character_movement": f"{main_character} moves through the scene.",
            "object_reveal": f"{main_object} is revealed from near {main_character}.",
            "object_insert": f"The camera focuses on {main_object}.",
            "character_reaction": f"{main_character} reacts emotionally.",
            "dialogue_or_thought": f"{main_character} expresses a thought.",
        }

        return mapping.get(semantic_type, "A cinematic beat plays.")

    @staticmethod
    def _default_emotion(semantic_type: str) -> str:
        mapping = {
            "establish_scene": "mysterious",
            "movement_detail": "focused",
            "character_movement": "focused",
            "object_reveal": "mysterious",
            "object_insert": "mysterious",
            "character_reaction": "confused",
            "dialogue_or_thought": "confused",
        }

        return mapping.get(semantic_type, "neutral")

    @staticmethod
    def _default_duration(semantic_type: str) -> float:
        mapping = {
            "establish_scene": 5.0,
            "movement_detail": 4.0,
            "character_movement": 5.0,
            "object_reveal": 5.0,
            "object_insert": 3.0,
            "character_reaction": 4.0,
            "dialogue_or_thought": 4.0,
        }

        return mapping.get(semantic_type, 4.0)

    def _fallback_sequence(
            self,
            main_character: str,
            main_object: str,
            fallback_dialogue: str,
    ) -> List[Dict[str, Any]]:
        semantic_sequence = [
            "establish_scene",
            "movement_detail",
            "character_movement",
            "object_reveal",
            "dialogue_or_thought",
        ]

        return [
            self._make_beat(
                beat_id=index,
                semantic_type=semantic_type,
                main_character=main_character,
                main_object=main_object,
                fallback_dialogue=fallback_dialogue,
            ).to_dict()
            for index, semantic_type in enumerate(semantic_sequence, start=1)
        ]