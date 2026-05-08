# CineAI Director

CineAI Director is an AI-powered automatic cutscene generation system built for Unity.

The goal of this project is to generate a playable cinematic cutscene from normal natural language story input, without requiring the user to manually write camera positions, animation timing, Timeline clips, or Cinemachine camera setups.

Example input:

```text
Uncle Ben walks toward the mysterious rock. He stops in front of it, looks confused, and thinks: Strange, where did this rock come from?
```

The system should understand this like a film director and automatically convert it into:

```text
Story input
→ AI/LLM scene understanding
→ cinematic beat planning
→ camera planning
→ character movement planning
→ Unity-ready beat script JSON
→ automatic Unity Timeline generation
→ playable cutscene
```

---

## 1. Project Goal

The long-term goal is to build a universal AI Director system for Unity.

The system should not be hardcoded only for one scene, one character, or one game. It should eventually work with different Unity projects by reading the available scene objects, characters, animations, anchors, and world positions.

The current prototype focuses on this test scene:

```text
Character: UNCLE_BEN
Object: ROCK
Scene: outdoor terrain environment
Action: Uncle Ben walks toward the rock, stops, reacts, and thinks/dialogues
```

However, the architecture is being designed to become universal.

---

## 2. Current System Pipeline

The project has two major sides:

```text
Python AI Director side
Unity Runtime/Editor side
```

### Full Flow

```text
1. User writes a natural language story.
2. Python sends the story to the local LLM through Ollama.
3. The LLM creates a cinematic beat plan.
4. Python normalizes and validates the AI output.
5. Python exports Unity-compatible beat script JSON.
6. Unity loads the JSON.
7. Unity resolves scene objects and character anchors.
8. Unity creates Cinemachine virtual cameras.
9. Unity creates Timeline tracks and clips.
10. Unity plays/scrubs the generated cutscene.
```

---

## 3. Current Repository Structure

Important files and responsibilities:

```text
ai_director/
  main.py
  director_brain.py
  prompt_builder.py
  llm_director.py
  semantic_normalizer.py
  camera_parameter_normalizer.py
  cinematic_knowledge.py
  schema.py
  validator.py
  scene_context.example.json
  scene_context.generated.json

Assets/
  Resources/
    BeatScripts/
      generated_scene.json
    Animations/
      Walk
      Idle
    AnimationLibrary/
      animation_map.json

  Scripts/
    Core/
      Beat.cs
      BeatListWrapper.cs
      BeatScriptLoader.cs

    Runtime/
      CutsceneCompiler.cs
      CutsceneCharacterMover.cs
      CharacterMoveData.cs
      ActionPlanner.cs

    Timeline/
      TimelineBuilder.cs
      AnimationLibraryResolver.cs

    Cinematics/
      ShotPlanner.cs
      PlannedShot.cs
      CinemachineMotionExtension.cs
      CameraSafetyValidator.cs

    Bindings/
      SceneBindingResolver.cs
      VirtualCameraAnchor.cs

    Editor/
      SceneContextExporter.cs
```

---

# Python Side

## 4. `main.py`

`main.py` is the entry point for generating beat scripts.

It loads:

```text
scene context JSON
natural language story
LLM mode
```

It generates:

```text
ai_director/outputs/generated_scene_universal.json
ai_director/outputs/generated_scene_unity.json
Assets/Resources/BeatScripts/generated_scene.json
```

Current real LLM command:

```powershell
python ai_director\main.py --context ai_director\scene_context.generated.json --use-real-llm --story "Uncle Ben walks toward the mysterious rock. He stops in front of it, looks confused, and thinks: Strange, where did this rock come from?" --name generated_scene
```

Mock mode for quick testing:

```powershell
python ai_director\main.py --mock-llm --story "Uncle Ben walks toward the mysterious rock. He stops in front of it, looks confused, and thinks: Strange, where did this rock come from?" --name generated_scene
```

---

## 5. `llm_director.py`

`llm_director.py` connects Python to Ollama.

The system uses a local LLM through:

```text
http://localhost:11434/api/generate
```

The timeout was increased to 300 seconds because Version 2 prompts are larger.

Recommended model:

```powershell
ollama pull llama3.1:8b
$env:CINEAI_OLLAMA_MODEL="llama3.1:8b"
```

---

## 6. `prompt_builder.py`

`prompt_builder.py` builds the prompt given to the LLM.

Earlier versions asked the LLM to choose from predefined cinematic templates. Current Version 2 asks the LLM to generate camera parameters directly.

The prompt asks for beat fields such as:

```json
{
  "beat_id": 1,
  "purpose": "string",
  "action": "string",
  "emotion": "neutral",
  "intent": "approach",
  "speaker": "UNCLE_BEN",
  "dialogue": "",
  "target_role": "character",
  "duration": 4,
  "transition": "cut",
  "camera": {
    "shot_type": "medium_shot",
    "target_role": "character",
    "look_at_role": "character",
    "follow_role": "character",
    "movement": "follow",
    "fov": 55,
    "offset": { "x": 0, "y": 1.8, "z": 4.5 },
    "force_world_position": false
  }
}
```

---

## 7. `director_brain.py`

`director_brain.py` is the main multi-agent planning file.

Current internal agents:

```text
StoryUnderstandingAgent
DirectorAgent
BlockingAgent
CinematographerAgent
EditorAgent
UnitySafetyValidatorAgent
```

It converts LLM output into a validated universal beat script.

### Important blocking update

The older `BlockingAgent` used fixed movement points such as:

```text
default_character_start
default_character_middle
default_character_end
```

This caused the character to move in a fixed lane.

The new direction is object-relative blocking:

```text
object position = scene_context.default_object_position
character start = scene_context.default_character_start
direction = normalize(character_start - object_position)
start = object + direction * approach_start_distance
middle = object + direction * approach_middle_distance
end = object + direction * approach_stop_distance
```

This is more universal because the movement path is calculated from real scene positions instead of fixed numbers.

---

## 8. `semantic_normalizer.py`

`semantic_normalizer.py` normalizes weak or inconsistent LLM beat output.

It helps repair issues like:

```text
missing establishing beat
missing movement beat
wrong target role
wrong dialogue beat
wrong intent category
```

It currently still uses semantic beat categories such as:

```text
establish_scene
movement_detail
character_movement
object_reveal
character_reaction
dialogue_or_thought
```

This file is a temporary stability layer. As the LLM improves, this file should become less controlling and more of a validator.

---

## 9. `camera_parameter_normalizer.py`

`camera_parameter_normalizer.py` is the Version 2 camera parameter safety layer on the Python side.

It validates and clamps camera parameters generated by the LLM:

```text
shot_type
target_role
look_at_role
follow_role
movement
fov
offset
position
rotation
force_world_position
```

It prevents completely invalid values, for example:

```text
FOV too small
FOV too large
offset too extreme
invalid movement type
invalid target role
```

Important design decision:

This file should avoid story-specific rules like:

```python
if target_role == "feet":
    force this exact offset
```

The better long-term solution is a Unity-side camera safety validator because Unity knows the real target positions, terrain, and camera transforms.

---

## 10. `cinematic_knowledge.py`

Earlier this file was the main source of camera templates.

Now it should be treated as a fallback/reference library, not the main director.

Current direction:

```text
Version 1: template-driven camera planning
Version 2: LLM generates camera parameters directly
Version 3: scene-aware camera and blocking planning
```

So `cinematic_knowledge.py` remains useful only as a fallback and style guide.

---

# Unity Side

## 11. `BeatScriptLoader.cs`

Loads generated JSON from:

```text
Assets/Resources/BeatScripts/generated_scene.json
```

This file converts JSON into Unity `Beat` objects.

---

## 12. `SceneBindingResolver.cs`

Resolves names from the beat script into real Unity scene objects.

Examples:

```text
UNCLE_BEN → character root object
ROCK → rock object
UNCLE_BEN_HEAD → generated/found head anchor
UNCLE_BEN_FEET → generated/found feet anchor
```

The resolver creates virtual camera anchors such as:

```text
UNCLE_BEN_HEAD_ANCHOR
UNCLE_BEN_FEET_ANCHOR
```

This is needed because camera shots should focus on semantic body parts like head or feet, not random mesh bones.

---

## 13. `VirtualCameraAnchor.cs`

Separate script used by the generated anchors.

Important past issue:

The `VirtualCameraAnchor` class was previously inside `SceneBindingResolver.cs`, which created missing script issues in Unity.

Fix:

```text
VirtualCameraAnchor must be in its own file:
Assets/Scripts/Bindings/VirtualCameraAnchor.cs
```

Old broken anchor objects should be deleted and regenerated.

---

## 14. `ShotPlanner.cs`

Converts a `Beat` into a `PlannedShot`.

It resolves:

```text
follow target
look-at target
camera offset
FOV
movement type
exact camera position or target-relative camera
```

Current approach:

```text
LLM/Python generates values
ShotPlanner converts them into Unity camera setup
SceneBindingResolver resolves target names
```

---

## 15. `CutsceneCompiler.cs`

Main Unity generator.

It does the following:

```text
1. Loads beat script.
2. Clears existing timeline tracks.
3. Clears generated cameras.
4. Creates Cinemachine track.
5. Creates virtual cameras.
6. Creates animation and activation tracks.
7. Builds character movement schedule.
8. Saves Timeline asset.
9. Assigns Timeline to PlayableDirector.
10. Adds/updates CutsceneCharacterMover.
```

Important current fix:

`PrePositionActor()` must place the character at the start position before creating the camera.

Wrong behavior:

```text
Camera was generated from the character end position.
Then movement changed character position.
This caused bad framing.
```

Correct behavior:

```text
Place actor at beat start position.
Generate camera from start pose.
Let mover handle start → end movement.
```

---

## 16. `CutsceneCharacterMover.cs`

Moves characters using the current `PlayableDirector.time`.

Important update:

The original mover only worked during play mode:

```csharp
if (playableDirector.state != PlayState.Playing) return;
```

That meant Timeline scrubbing showed animation but not movement.

Updated behavior:

```text
Works during Play mode
Works during Timeline preview/scrubbing
Uses PlayableDirector time
Disables root motion so scripted movement controls world position
```

Current design:

```text
Timeline animation clip = visual body/leg movement
CutsceneCharacterMover = actual world position movement
```

This avoids relying on root motion and makes movement more universal.

---

## 17. `TimelineBuilder.cs`

Creates Timeline tracks and clips.

It creates:

```text
CinemachineTrack
AnimationTrack
ActivationTrack
SignalTrack / MarkerTrack if needed
```

Earlier, it looked for animation clips directly using:

```csharp
Resources.Load<AnimationClip>("Animations/" + animationName)
```

This was too rigid.

Now it uses:

```text
AnimationLibraryResolver
```

---

## 18. `AnimationLibraryResolver.cs`

Universal animation resolver.

Instead of hardcoding animations in C#, it reads:

```text
Assets/Resources/AnimationLibrary/animation_map.json
```

Example:

```json
{
  "default_idle": "Idle",
  "aliases": [
    {
      "action": "walk",
      "clips": ["Walk", "Walking", "WalkForward", "walk"]
    },
    {
      "action": "reaction",
      "clips": ["Reaction", "React", "Thinking", "Confused", "Idle"]
    }
  ]
}
```

This makes the system more universal.

For another game, update `animation_map.json` instead of changing C# code.

---

## 19. `animation_map.json`

Located at:

```text
Assets/Resources/AnimationLibrary/animation_map.json
```

Important:

This file must be named exactly:

```text
animation_map.json
```

Not:

```text
animation_map.json.cs
```

A previous mistake caused Unity to compile JSON as C# and show:

```text
error CS1513: } expected
```

---

## 20. `CameraSafetyValidator.cs`

New universal Unity-side camera safety layer.

Purpose:

```text
Validate generated cameras using real Unity scene geometry.
```

It should protect against:

```text
camera below terrain
camera too close to target
camera looking too far into sky
camera placed under the target
camera inside character/object
```

Important current discovery:

The validator must not only adjust the camera transform. It must also update `CinemachineMotionExtension.initialOffset`.

Reason:

```text
1. CameraSafetyValidator adjusts camera position.
2. CinemachineMotionExtension runs during Timeline preview/playback.
3. It reapplies the old unsafe offset.
4. Camera goes back to bad framing.
```

Required fix inside `CameraSafetyValidator.ValidateCamera()`:

```csharp
CinemachineMotionExtension motionExtension = cam.GetComponent<CinemachineMotionExtension>();

if (motionExtension != null)
{
    Transform motionTarget = motionExtension.target != null
        ? motionExtension.target
        : motionExtension.lookTarget;

    if (motionTarget != null)
    {
        motionExtension.initialOffset = safePosition - motionTarget.position;
        motionExtension.useWorldAnchor = false;
    }
}
```

This is the next important fix to apply if the camera still shows the character walking in the sky.

---

## 21. `CinemachineMotionExtension.cs`

Custom component that drives camera motion after creation.

It handles:

```text
static
follow
orbit
dolly_in
dolly_out
pan
```

Important:

This script can override camera transform every frame. Therefore, camera safety must update the extension’s internal offset, not only the camera object transform.

---

## 22. `SceneContextExporter.cs`

Editor tool for exporting real Unity scene data.

Menu:

```text
CineAI → Export Scene Context
```

Exports:

```text
characters
objects
locations
default_character_start
default_object_position
approach_stop_distance
approach_middle_distance
approach_start_distance
available_animations
```

Output path:

```text
ai_director/scene_context.generated.json
```

This is important because Python should not guess scene positions.

Correct flow:

```text
Unity exports real scene context
Python uses generated scene context
Unity generates timeline from generated beat script
```

Run Python with:

```powershell
python ai_director\main.py --context ai_director\scene_context.generated.json --use-real-llm --story "Uncle Ben walks toward the mysterious rock. He stops in front of it, looks confused, and thinks: Strange, where did this rock come from?" --name generated_scene
```

---

# Current Known Issues

## 23. Camera still sometimes shows character walking in sky

Current diagnosis:

The character position in JSON is not actually high in the sky. The problem is mostly camera framing/offset.

Example JSON showed:

```json
"char_start_y": -0.174587,
"char_end_y": -0.174587
```

So the character is not being placed high.

The camera is looking from a bad low/upward angle.

Main suspected cause:

```text
CinemachineMotionExtension reapplies unsafe original offset after CameraSafetyValidator runs.
```

Next fix:

```text
Update CameraSafetyValidator so it also updates CinemachineMotionExtension.initialOffset.
```

---

## 24. Beat 2 foot shot can be too low

The LLM may generate a low offset like:

```json
"camera_offset_y": 0.25,
"camera_offset_z": -2.4
```

This is not always physically safe.

We decided not to use a hardcoded rule like:

```python
if target_role == "feet":
    force offset
```

because that becomes rule-based.

Better approach:

```text
Unity-side CameraSafetyValidator checks real final camera geometry and repairs invalid camera placement.
```

---

## 25. Character movement now works but framing is still unstable

Current state:

```text
Character transform movement works.
Walking animation plays.
Timeline preview movement works.
But camera framing still needs safety correction.
```

---

# Current Design Philosophy

The system should move from rule-based to universal AI-driven generation.

## Version 1

Template-based.

```text
LLM chooses cinematic template
Unity executes template
```

Good for proof of concept but not fully autonomous.

## Version 2

Parameter-generating AI director.

```text
LLM generates camera parameters directly
Python validates basic values
Unity validates physical camera safety
```

This is the current stage.

## Version 3

Scene-aware cinematic AI.

```text
Unity exports real scene metadata
AI plans using actual scene positions
Unity validates and executes
```

We have started this by adding `SceneContextExporter.cs`.

---

# Recommended Workflow From Now

Use this full workflow every time.

## Step 1: Export real Unity scene context

In Unity:

```text
CineAI → Export Scene Context
```

Confirm Console shows:

```text
Exact scene object match: UNCLE_BEN
Exact scene object match: ROCK
Scene context exported
```

## Step 2: Generate beat script from Python

```powershell
python ai_director\main.py --context ai_director\scene_context.generated.json --use-real-llm --story "Uncle Ben walks toward the mysterious rock. He stops in front of it, looks confused, and thinks: Strange, where did this rock come from?" --name generated_scene
```

## Step 3: Regenerate Unity Timeline

In Unity:

```text
Click CineAI_Manager
Generate Cutscene Timeline
```

## Step 4: Check logs

Expected logs:

```text
Beat script loaded
Bound Cinemachine track
Animation map loaded
Animation resolved
Animation clip assigned
Camera safety validation completed
Cutscene generated
```

## Step 5: Preview Timeline

Scrub Timeline and check:

```text
character moves physically
walking animation plays
camera follows correct target
camera does not go below/under character
camera does not look into sky
```

---

# Important Notes for Future ChatGPT Sessions

If continuing this project in ChatGPT, give this context:

```text
We are building CineAI Director, an AI-powered Unity cutscene generation system.
The system takes natural language story input, uses Ollama/LLM to generate cinematic beats and camera parameters, exports Unity beat script JSON, and Unity generates Timeline + Cinemachine cameras automatically.
We are currently in Version 2: LLM-generated camera parameters with Python validation and Unity-side physical camera safety.
The biggest current issue is camera framing: the character moves and animation works, but cameras sometimes show the character walking in the sky because CinemachineMotionExtension reapplies old unsafe offsets.
The next fix is to update CameraSafetyValidator so it also updates CinemachineMotionExtension.initialOffset after safety correction.
```

---

# Current Next Task

The next task is:

```text
Update CameraSafetyValidator.cs so safety corrections persist during Timeline playback and scrubbing.
```

Specifically:

```text
After safe camera position is calculated:
1. Move camera transform.
2. Recompute offset from motion target.
3. Assign it back to CinemachineMotionExtension.initialOffset.
4. Regenerate Timeline.
5. Test Beat 2 and Beat 3 again.
```

---

# Project Status Summary

Current working features:

```text
Natural language story input
Ollama LLM integration
Version 2 prompt for direct camera parameter generation
Semantic beat normalization
Unity-compatible beat script export
Real Unity scene context export
Timeline generation
Cinemachine camera generation
Animation track generation
Animation library resolver through JSON
Character movement during play and Timeline scrubbing
Head/feet virtual anchors
Object-relative blocking started
Camera safety validator started
```

Current incomplete features:

```text
Robust camera safety persistence with CinemachineMotionExtension
Terrain-aware character pathing
Obstacle avoidance
Advanced scene-aware shot composition
Dialogue/audio/subtitle system
Facial expression system
Automatic lighting control
Full multi-character blocking
Final cinematic polish
```

---

# Final Vision

CineAI Director should eventually work like this:

```text
User: "A hero walks into an abandoned temple, notices a glowing artifact, and becomes afraid."

System:
1. Detects hero, temple, artifact, emotion, and story intent.
2. Finds matching Unity character, location, and object.
3. Plans cinematic beats.
4. Places character using real scene geometry.
5. Chooses camera shots dynamically.
6. Validates all cameras physically.
7. Generates Timeline automatically.
8. Plays a complete cinematic cutscene.
```

The final goal is not just generating a Timeline.

The final goal is an AI system that behaves like a game cinematic director inside Unity.
