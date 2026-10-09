from __future__ import annotations

import json
import re
from pathlib import Path

from pydantic import ValidationError
from rich.console import Console
from rich.progress import Progress

from ..models import Recipe
from ..ollama_client import OllamaClient

SYSTEM_PROMPT = """\
Tu es un assistant specialise dans l'extraction structuree de recettes de cuisine.

A partir du texte Markdown fourni (issu d'un OCR de magazine), extrais toutes les recettes \
et retourne UNIQUEMENT un tableau JSON valide avec exactement ces proprietes pour chaque recette :

[
  {
    "title": "string",
    "author": "string ou null",
    "prep_time": "string ou null (ex: '20 min', '1h30')",
    "cook_time": "string ou null (ex: '45 min', '2h')",
    "servings": "string ou null (ex: '4 personnes', '6 parts')",
    "ingredients": [
      {
        "name": "string",
        "quantity": "string ou null",
        "unit": "string ou null"
      }
    ],
    "tags": ["string (1 a 3 tags decrivant le type de recette, ex: dessert, buche, tarte, salade, vegetarien)"]
  }
]

IMPORTANT :
- Retranscris le texte exactement tel quel, mot pour mot.
- Ne resume pas, ne simplifie pas, ne condense pas les ingredients.
- Si le texte ne contient aucune recette, retourne un tableau vide [].
- Ne retourne rien d'autre que le JSON.\
"""

console = Console()


def _extract_json(text: str) -> str:
    fence_match = re.search(r"```\w*\s*([\s\S]*?)\s*```", text)
    if fence_match:
        return fence_match.group(1).strip()

    array_match = re.search(r"\[[\s\S]*\]", text)
    if array_match:
        return array_match.group(0)

    brace_match = re.search(r"\{[\s\S]*\}", text)
    if brace_match:
        return brace_match.group(0)

    return text.strip()


def _parse_page_number(md_path: Path) -> int | None:
    match = re.search(r"page_(\d+)", md_path.stem)
    if match:
        return int(match.group(1))
    return None


def _parse_recipes(raw: str, source_page: int | None) -> list[Recipe]:
    json_str = _extract_json(raw)

    try:
        data = json.loads(json_str)
    except json.JSONDecodeError:
        return []

    if isinstance(data, dict):
        data = [data]

    if not isinstance(data, list):
        return []

    recipes: list[Recipe] = []
    for item in data:
        try:
            recipe = Recipe.model_validate(item)
            recipe.source_page = source_page
            recipes.append(recipe)
        except ValidationError:
            continue

    return recipes


def run_structure(
    md_dir: Path,
    output_dir: Path,
    ollama: OllamaClient,
) -> list[Path]:
    md_dir = md_dir.resolve()
    output_dir = output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    md_files = sorted(md_dir.glob("*.md"))
    if not md_files:
        console.print("[yellow]No .md files found.[/yellow]")
        return []

    written: list[Path] = []

    with Progress() as progress:
        task = progress.add_task("Structuring recipes", total=len(md_files))

        for md_path in md_files:
            page_num = _parse_page_number(md_path)
            content = md_path.read_text(encoding="utf-8")

            if "no_recipe" in content or not content.strip():
                progress.advance(task)
                continue

            try:
                raw = ollama.generate(SYSTEM_PROMPT, content)
            except Exception as e:
                console.print(f"[red]LLM error for {md_path.name}: {e}[/red]")
                progress.advance(task)
                continue

            recipes = _parse_recipes(raw, page_num)

            for idx, recipe in enumerate(recipes):
                page_part = f"page_{page_num:03d}" if page_num else md_path.stem
                recipe_part = f"recipe_{idx + 1:02d}"
                out_path = output_dir / f"{page_part}_{recipe_part}.json"

                with open(out_path, "w", encoding="utf-8") as f:
                    f.write(recipe.model_dump_json(indent=2, by_alias=False))

                written.append(out_path)

            progress.advance(task)

    console.print(f"[green]Wrote {len(written)} recipe file(s).[/green]")
    return written
