from __future__ import annotations

import json
import os
import urllib.error
import urllib.request
from typing import Any, Dict, List


class LLMDirector:
    """
    LLM adapter for AI Director planning.

    Modes:
    - use_mock=True: deterministic fallback, no external model required.
    - use_mock=False: calls local Ollama server.

    Requirements for real LLM mode:
    1. Install Ollama.
    2. Run:
       ollama pull llama3.1
    3. Make sure Ollama is running locally.
    4. Run:
       python ai_director/main.py --use-real-llm --story "..." --name generated_scene
    """

    def __init__(self, use_mock: bool = True) -> None:
        self.use_mock = use_mock
        self.provider = os.getenv("CINEAI_LLM_PROVIDER", "ollama").lower()
        self.model = os.getenv("CINEAI_OLLAMA_MODEL", "llama3.1")
        self.ollama_url = os.getenv("CINEAI_OLLAMA_URL", "http://localhost:11434/api/generate")

    def generate_plan(self, prompt: str, scene_context: Dict[str, Any], story: str) -> Dict[str, Any]:
        if self.use_mock:
            return self._mock_plan(scene_context, story)

        raw_response = self._call_llm(prompt)
        return self._parse_json(raw_response)

    def _call_llm(self, prompt: str) -> str:
        if self.provider != "ollama":
            raise ValueError(f"Unsupported LLM provider: {self.provider}")

        payload = {
            "model": self.model,
            "prompt": prompt,
            "stream": False,
            "format": "json",
            "options": {
                "temperature": 0.2,
                "top_p": 0.9,
            },
        }

        data = json.dumps(payload).encode("utf-8")

        request = urllib.request.Request(
            self.ollama_url,
            data=data,
            headers={"Content-Type": "application/json"},
            method="POST",
        )

        try:
            with urllib.request.urlopen(request, timeout=300) as response:
                response_body = response.read().decode("utf-8")
        except urllib.error.URLError as exc:
            raise ConnectionError(
                "Could not connect to Ollama. Make sure Ollama is installed and running. "
                "Try: ollama pull llama3.1"
            ) from exc

        try:
            response_json = json.loads(response_body)
        except json.JSONDecodeError as exc:
            raise ValueError(f"Ollama returned invalid wrapper JSON: {response_body}") from exc

        if "response" not in response_json:
            raise ValueError(f"Ollama response did not contain 'response': {response_json}")

        return response_json["response"]

    def _parse_json(self, raw_response: str) -> Dict[str, Any]:
        cleaned = raw_response.strip()

        if cleaned.startswith("```"):
            cleaned = cleaned.strip("`")
            cleaned = cleaned.replace("json", "", 1).strip()

        try:
            return json.loads(cleaned)
        except json.JSONDecodeError as exc:
            raise ValueError(f"LLM returned invalid JSON:\n{cleaned}") from exc

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