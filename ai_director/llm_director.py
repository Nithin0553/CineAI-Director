from __future__ import annotations

import json
from typing import Any, Dict, List


class LLMDirector:
    """
    LLM adapter for AI Director planning.

    Current stage:
    - Uses a safe deterministic fallback so the project runs without API keys.
    - Later, replace _call_llm() with OpenAI, Ollama, LM Studio, or a fine-tuned model.

    Expected output:
    {
      "scene_title": "...",
      "beats": [...]
    }
    """

    def __init__(self, use_mock: bool = True) -> None:
        self.use_mock = use_mock

    def generate_plan(self, prompt: str, scene_context: Dict[str, Any], story: str) -> Dict[str, Any]:
        if self.use_mock:
            return self._mock_plan(scene_context, story)

        raw_response = self._call_llm(prompt)
        return self._parse_json(raw_response)

    def _call_llm(self, prompt: str) -> str:
        """
        Replace this later with a real LLM call.

        Options:
        - OpenAI API
        - Ollama local model
        - LM Studio local server
        - fine-tuned HuggingFace model
        """
        raise NotImplementedError("Real LLM connection will be added in the next step.")

    def _parse_json(self, raw_response: str) -> Dict[str, Any]:
        try:
            return json.loads(raw_response)
        except json.JSONDecodeError as exc:
            raise ValueError(f"LLM returned invalid JSON: {exc}") from exc

    def _mock_plan(self, scene_context: Dict[str, Any], story: str) -> Dict[str, Any]:
        characters: List[str] = scene_context.get("characters", [])
        objects: List[str] = scene_context.get("objects", [])

        main_character = characters[0] if characters else "CHARACTER"
        main_object = objects[0] if objects else "OBJECT"
        dialogue = self._extract_dialogue_or_default(story)

        return {
            "scene_title": "Generated Cutscene",
            "beats": [
                {
                    "beat_id": 1,
                    "purpose": "Establish the mysterious object in the location.",
                    "action": f"A wide aerial orbit reveals {main_object}.",
                    "emotion": "mysterious",
                    "intent": "establish",
                    "speaker": "",
                    "dialogue": "",
                    "template_id": "aerial_object_orbit",
                    "target_role": "object",
                    "duration": 5.0,
                    "transition": "cut",
                },
                {
                    "beat_id": 2,
                    "purpose": "Show the character approaching through movement detail.",
                    "action": f"Low detail shot follows {main_character}'s feet as the character walks forward.",
                    "emotion": "focused",
                    "intent": "approach",
                    "speaker": main_character,
                    "dialogue": "",
                    "template_id": "feet_follow_detail",
                    "target_role": "feet",
                    "duration": 5.0,
                    "transition": "cut",
                },
                {
                    "beat_id": 3,
                    "purpose": "Show the character approaching the object.",
                    "action": f"{main_character} walks toward {main_object}.",
                    "emotion": "concerned",
                    "intent": "approach",
                    "speaker": main_character,
                    "dialogue": "",
                    "template_id": "medium_walk_approach",
                    "target_role": "character",
                    "duration": 6.0,
                    "transition": "cut",
                },
                {
                    "beat_id": 4,
                    "purpose": "Reveal the object from the character's point of view.",
                    "action": f"Over-the-shoulder shot reveals {main_object} in front of {main_character}.",
                    "emotion": "mysterious",
                    "intent": "reveal",
                    "speaker": main_character,
                    "dialogue": "",
                    "template_id": "over_shoulder_reveal",
                    "target_role": "object",
                    "duration": 5.0,
                    "transition": "cut",
                },
                {
                    "beat_id": 5,
                    "purpose": "Show the character's emotional reaction.",
                    "action": f"Close-up on {main_character}'s face as they react.",
                    "emotion": "confused",
                    "intent": "question",
                    "speaker": main_character,
                    "dialogue": dialogue,
                    "template_id": "face_reaction_closeup",
                    "target_role": "head",
                    "duration": 5.0,
                    "transition": "fade",
                },
            ],
        }

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