from __future__ import annotations

import json
from typing import Any, Dict


class DirectorPromptBuilder:
    """
    Builds the prompt that will later be sent to an LLM.

    The LLM's job:
    - understand the user's natural language story
    - select cinematic beats
    - output valid structured JSON only

    Unity's job:
    - execute the JSON
    """

    def build_prompt(
            self,
            story: str,
            scene_context: Dict[str, Any],
            cinematic_templates: Dict[str, Any],
    ) -> str:
        context_json = json.dumps(scene_context, indent=2)
        templates_json = json.dumps(cinematic_templates, indent=2)

        return f"""
You are an AI Film Director for a Unity cutscene generation system.

Your task is to convert a user's natural language story into a structured cutscene beat plan.

IMPORTANT RULES:
1. Output valid JSON only.
2. Do not include markdown.
3. Do not explain anything.
4. Use only character names from scene_context.characters.
5. Use only object names from scene_context.objects.
6. Use only template_id values from cinematic_templates.
7. Every beat must have a clear purpose, action, emotion, intent, template_id, target_role, duration, and transition.
8. target_role must be one of:
   - character
   - object
   - feet
   - head
   - body
9. Use 3 to 8 beats depending on story complexity.
10. Use cinematic logic: establish, approach, reveal, reaction, dialogue, exit.

SCENE CONTEXT:
{context_json}

AVAILABLE CINEMATIC TEMPLATES:
{templates_json}

USER STORY:
{story}

OUTPUT JSON SCHEMA:
{{
  "scene_title": "string",
  "beats": [
    {{
      "beat_id": 1,
      "purpose": "string",
      "action": "string",
      "emotion": "neutral | mysterious | concerned | focused | confused | shocked | sad | happy | angry | fearful | determined",
      "intent": "establish | approach | reveal | react | question | dialogue | exit | observe",
      "speaker": "character name or empty string",
      "dialogue": "dialogue text or empty string",
      "template_id": "one template id from cinematic_templates",
      "target_role": "character | object | feet | head | body",
      "duration": 3.0,
      "transition": "cut | fade | dissolve | match_cut"
    }}
  ]
}}
""".strip()