from __future__ import annotations

import json
from typing import Any, Dict


class DirectorPromptBuilder:
    """
    Builds the prompt sent to the LLM.

    Version 2:
    The LLM should generate cinematic camera parameters directly.
    Templates are now only references/fallbacks, not the main output.
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

Your task is to convert a user's natural language story into a structured cinematic beat plan.

IMPORTANT RULES:
1. Output valid JSON only.
2. Do not include markdown.
3. Do not explain anything.
4. Use only character names from scene_context.characters.
5. Use only object names from scene_context.objects.
6. Use 3 to 8 beats depending on story complexity.
7. Each beat must include a camera object with direct camera parameters.
8. Do not rely only on templates. Generate camera parameters directly.
9. Use templates only as style references and fallback guidance.
10. Use target roles, not hardcoded bone names.

VALID target_role / look_at_role / follow_role:
- character
- object
- feet
- head
- body
- null for follow_role only when the camera should not follow anything

VALID shot_type:
- establishing_shot
- wide_shot
- medium_shot
- close_up
- extreme_close_up
- over_the_shoulder
- insert_shot
- reaction_shot
- tracking_shot

VALID movement:
- static
- follow
- orbit
- pan
- tilt
- dolly_in
- dolly_out
- truck_left
- truck_right

CAMERA PARAMETER GUIDANCE:
- fov should usually be between 24 and 60.
- close_up should usually use fov 24 to 35.
- medium_shot should usually use fov 40 to 60.
- wide_shot should usually use fov 45 to 65.
- offset is relative to the follow/look target unless force_world_position is true.
- For establishing object shots, use target_role object, look_at_role object, follow_role null, movement orbit/static.
- For movement detail shots, use target_role feet, look_at_role feet, follow_role feet, movement follow.
- For character walking shots, use target_role character, look_at_role character, follow_role character, movement follow.
- For over-shoulder reveal, use target_role object, look_at_role object, follow_role character, movement pan/static.
- For reaction/dialogue close-up, use target_role head, look_at_role head, follow_role head, movement follow/static.

SCENE CONTEXT:
{context_json}

STYLE REFERENCE TEMPLATES:
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
      "intent": "establish | approach | reveal | react | question | dialogue | exit | observe | insert",
      "speaker": "character name or empty string",
      "dialogue": "dialogue text or empty string",
      "template_id": "optional template id from style references",
      "target_role": "character | object | feet | head | body",
      "duration": 3.0,
      "transition": "cut | fade | dissolve | match_cut",
      "camera": {{
        "shot_type": "wide_shot",
        "target_role": "object",
        "look_at_role": "object",
        "follow_role": null,
        "movement": "orbit",
        "fov": 50.0,
        "offset": {{ "x": 0.0, "y": 4.0, "z": -8.0 }},
        "force_world_position": false,
        "position": {{ "x": 0.0, "y": 0.0, "z": 0.0 }},
        "rotation": {{ "x": 0.0, "y": 0.0, "z": 0.0 }},
        "speed": 5.0,
        "radius": 7.0
      }}
    }}
  ]
}}
""".strip()