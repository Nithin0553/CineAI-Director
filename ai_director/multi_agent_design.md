# Camera-Artist-Inspired Multi-Agent Design for CineAI Director

This design adapts the idea of cinematic-language planning from multi-agent video-generation research into a Unity cutscene generation pipeline.

The goal is not to generate final video frames. The goal is to generate a Unity-executable Beat Script that can be loaded by the CineAI Director Unity interpreter.

## Core Difference

Video generation systems usually output visual frames or video prompts.

CineAI Director outputs structured execution data:

```text
Story + Scene Context
→ Multi-Agent AI Director
→ Universal Beat Script JSON
→ Unity Timeline + Cinemachine
```

## Proposed Agents

### 1. Story Understanding Agent
Extracts:
- characters
- objects
- location
- dialogue
- emotions
- story intent
- important actions

### 2. Director Agent
Converts the story into cinematic beats:
- beat order
- dramatic purpose
- emotional progression
- what should be shown in each beat

### 3. Cinematographer Agent
Plans camera language:
- shot type
- camera position
- camera rotation
- FOV
- LookAt target
- Follow target
- camera movement

### 4. Blocking Agent
Plans physical staging:
- character start position
- character end position
- facing direction
- object relationship
- movement intention

### 5. Editor Agent
Plans pacing:
- duration
- transition type
- shot rhythm
- final scene flow

### 6. Unity Safety Validator Agent
Checks that:
- JSON is valid
- scene object names match context
- required fields exist
- camera values are usable
- Unity-compatible output is generated

## Implementation Strategy

First implementation can use one LLM call with internal agent sections.
Later implementation can use separate calls for each agent.

Recommended first version:

```text
scene_context.json + free-form story
→ one prompt with 6 agent roles
→ universal JSON
→ validator.py
→ schema.py conversion
→ generated_scene.json
```

## Future Fine-Tuning Dataset

Each training example should contain:

```json
{
  "scene_context": {},
  "story": "Free-form script idea",
  "universal_beat_script": {}
}
```

Fine-tuning should happen only after collecting many validated examples.
