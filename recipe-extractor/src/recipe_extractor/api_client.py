from __future__ import annotations

import json
from pathlib import Path

import httpx

from .config import ApiConfig


class RecipeApiClient:
    def __init__(self, config: ApiConfig):
        self._client = httpx.Client(
            base_url=config.base_url,
            timeout=30.0,
        )

    def create_recipe(self, recipe: dict) -> dict:
        response = self._client.post("/api/recipes", json=recipe)
        response.raise_for_status()
        return response.json()

    def ingest_manual(self, source_file_path: Path, recipes: list[dict]) -> list[dict]:
        recipes_json = json.dumps(recipes, ensure_ascii=False)
        with open(source_file_path, "rb") as f:
            mime = "application/pdf" if source_file_path.suffix == ".pdf" else "image/jpeg"
            response = self._client.post(
                "/api/recipes/ingest/manual",
                files={"file": (source_file_path.name, f, mime)},
                data={"recipes": recipes_json},
            )
        response.raise_for_status()
        return response.json()

    def close(self) -> None:
        self._client.close()
