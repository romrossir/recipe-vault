from __future__ import annotations

from pydantic import BaseModel, Field


class Ingredient(BaseModel):
    name: str
    quantity: str | None = None
    unit: str | None = None


class Recipe(BaseModel):
    title: str
    author: str | None = None
    prep_time: str | None = None
    cook_time: str | None = None
    servings: str | None = None
    ingredients: list[Ingredient] = Field(default_factory=list)
    tags: list[str] = Field(default_factory=list)
    source_page: int | None = None
