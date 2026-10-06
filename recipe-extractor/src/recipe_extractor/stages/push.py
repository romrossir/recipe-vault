from __future__ import annotations

import json
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path

from rich.console import Console

from ..api_client import RecipeApiClient
from ..models import Recipe

console = Console()


@dataclass
class PushResult:
    total: int = 0
    success: int = 0
    failed: int = 0
    errors: list[str] = field(default_factory=list)


def _recipe_to_api_payload(recipe: Recipe) -> dict:
    return {
        "title": recipe.title,
        "author": recipe.author,
        "prepTime": recipe.prep_time,
        "cookTime": recipe.cook_time,
        "servings": recipe.servings,
        "ingredients": [
            {"name": i.name, "quantity": i.quantity, "unit": i.unit}
            for i in recipe.ingredients
        ],
        "steps": recipe.steps,
        "sourcePages": [recipe.source_page] if recipe.source_page else [],
    }


def run_push(
    json_dir: Path,
    api_client: RecipeApiClient,
    source_file: Path | None = None,
    dry_run: bool = False,
) -> PushResult:
    json_dir = json_dir.resolve()
    json_files = sorted(json_dir.glob("*.json"))
    json_files = [f for f in json_files if not f.name.startswith("_")]

    if not json_files:
        console.print("[yellow]No recipe JSON files found.[/yellow]")
        return PushResult()

    recipes: list[dict] = []
    for jf in json_files:
        try:
            recipe = Recipe.model_validate_json(jf.read_text(encoding="utf-8"))
            recipes.append(_recipe_to_api_payload(recipe))
        except Exception as e:
            console.print(f"[red]Invalid JSON in {jf.name}: {e}[/red]")

    result = PushResult(total=len(recipes))

    if dry_run:
        console.print(f"[cyan]Dry run: would push {len(recipes)} recipe(s):[/cyan]")
        for r in recipes:
            pages = r.get("sourcePages", [])
            console.print(f"  - {r['title']} (page {pages})")
        return result

    try:
        if source_file and source_file.exists():
            response = api_client.ingest_manual(source_file, recipes)
            result.success = len(response)
            console.print(
                f"[green]Pushed {result.success} recipe(s) "
                f"with source file {source_file.name}.[/green]"
            )
        else:
            for recipe_payload in recipes:
                try:
                    api_client.create_recipe(recipe_payload)
                    result.success += 1
                except Exception as e:
                    result.failed += 1
                    result.errors.append(f"{recipe_payload['title']}: {e}")
                    console.print(
                        f"[red]Failed to push '{recipe_payload['title']}': {e}[/red]"
                    )

            console.print(
                f"[green]Pushed {result.success}/{result.total} recipe(s).[/green]"
            )
    except Exception as e:
        result.failed = result.total
        result.errors.append(str(e))
        console.print(f"[red]Push failed: {e}[/red]")

    push_dir = json_dir.parent / "push"
    push_dir.mkdir(parents=True, exist_ok=True)
    log_path = push_dir / "_push_log.json"
    log_entry = {
        "pushed_at": datetime.now(timezone.utc).isoformat(),
        "total": result.total,
        "success": result.success,
        "failed": result.failed,
        "errors": result.errors,
    }
    with open(log_path, "w", encoding="utf-8") as f:
        json.dump(log_entry, f, ensure_ascii=False, indent=2)

    return result
