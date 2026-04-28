from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any, Dict

from director_brain import LocalDirectorBrain
from schema import unity_legacy_beat_script
from validator import BeatScriptValidationError, validate_universal_script


ROOT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = ROOT_DIR.parent

DEFAULT_CONTEXT_PATH = ROOT_DIR / "scene_context.example.json"
OUTPUT_DIR = ROOT_DIR / "outputs"
UNITY_BEATS_DIR = PROJECT_ROOT / "Assets" / "Resources" / "BeatScripts"


def load_json(path: Path) -> Dict[str, Any]:
    if not path.exists():
        raise FileNotFoundError(f"File not found: {path}")

    with path.open("r", encoding="utf-8") as file:
        return json.load(file)


def save_json(path: Path, data: Dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)

    with path.open("w", encoding="utf-8") as file:
        json.dump(data, file, indent=2)

    print(f"Saved: {path}")


def interactive_story_input() -> str:
    print("\nEnter your free-form story/script idea.")
    print("Press ENTER twice when finished.\n")

    lines = []
    while True:
        line = input()
        if line.strip() == "":
            break
        lines.append(line)

    story = "\n".join(lines).strip()

    if not story:
        raise ValueError("Story/script idea cannot be empty.")

    return story


def main() -> None:
    parser = argparse.ArgumentParser(description="CineAI Director Brain prototype")
    parser.add_argument(
        "--context",
        type=str,
        default=str(DEFAULT_CONTEXT_PATH),
        help="Path to scene context JSON file.",
    )
    parser.add_argument(
        "--story",
        type=str,
        default=None,
        help="Free-form story/script idea. If omitted, interactive input is used.",
    )
    parser.add_argument(
        "--name",
        type=str,
        default="generated_scene",
        help="Output beat script file name without .json.",
    )

    args = parser.parse_args()

    context_path = Path(args.context)
    scene_context = load_json(context_path)

    story = args.story if args.story else interactive_story_input()

    brain = LocalDirectorBrain()
    universal_script = brain.generate(scene_context=scene_context, story=story)

    universal_data = universal_script.to_dict()

    try:
        validate_universal_script(universal_data)
    except BeatScriptValidationError as error:
        print("\nValidation failed:")
        print(error)
        raise SystemExit(1)

    legacy_data = unity_legacy_beat_script(universal_script)

    save_json(OUTPUT_DIR / f"{args.name}_universal.json", universal_data)
    save_json(OUTPUT_DIR / f"{args.name}_unity.json", legacy_data)
    save_json(UNITY_BEATS_DIR / f"{args.name}.json", legacy_data)

    print("\nGeneration complete.")
    print(f"Unity beat script created at: Assets/Resources/BeatScripts/{args.name}.json")
    print("In Unity, set CutsceneCompiler.beatScriptName to:")
    print(args.name)


if __name__ == "__main__":
    main()