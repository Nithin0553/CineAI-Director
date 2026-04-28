# CineAI Director Brain

This folder contains the external AI Director layer for the CineAI-Director project.

The goal is to move the directing intelligence outside Unity. Unity should mainly behave as a Beat Script interpreter: it reads explicit values from a JSON file and generates a playable Timeline/Cinemachine cutscene.

## Target Architecture

```text
User scene context + free-form story/script idea
        ↓
AI Director Brain / LLM
        ↓
Universal Beat Script JSON
        ↓
Unity Beat Script Interpreter
        ↓
Playable Timeline + Cinemachine cutscene
```

## Why this exists

The current Unity implementation can generate cutscenes, but many cinematic choices still depend on rule-based logic inside Unity. This module is designed to generate complete beat scripts with exact executable parameters, so Unity does not need to guess camera positions, shot choices, movement, timing, or character actions.

## First Milestone

The first milestone is a local Python prototype that:

1. Accepts project context from the user.
2. Accepts a free-form story or script idea.
3. Builds a strict LLM prompt.
4. Generates a Universal Beat Script JSON file.
5. Validates the JSON structure.
6. Saves the output into `Assets/Resources/BeatScripts/` for Unity to load.

## Planned Folder Structure

```text
ai_director/
  main.py
  director_brain.py
  prompt_templates.py
  schema.py
  validator.py
  scene_context.example.json
  outputs/
```

## Unity Responsibility

Unity should eventually stop acting as the director. Its job should be limited to:

- Load the beat script.
- Resolve characters, objects, and locations.
- Assign camera transforms, FOV, LookAt, Follow, and movement values.
- Assign animations and movement paths.
- Build Timeline tracks.
- Play the generated cutscene.

## LLM Responsibility

The LLM should decide:

- Number of beats.
- Cinematic pacing.
- Shot type and camera placement.
- Character actions.
- Dialogue and voiceover placement.
- Emotion and story intent.
- Transitions.
- Exact values needed by Unity.

## Fine-tuning Plan

Fine-tuning should not be the first step. First, we need a strong schema, prompt, validator, and many working examples. After enough examples are collected, the model can be fine-tuned using pairs like:

```text
Input: scene context + free-form story
Output: validated Universal Beat Script JSON
```
