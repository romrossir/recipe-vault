from __future__ import annotations

import os
from dataclasses import dataclass, field
from pathlib import Path

import yaml


@dataclass
class OllamaConfig:
    base_url: str = "http://localhost:11434"
    model: str = "llama3.2"
    timeout: float = 300.0


@dataclass
class ApiConfig:
    base_url: str = "http://localhost:5000"


@dataclass
class OcrConfig:
    lang: str = "fr"
    dpi: int = 300
    confidence_threshold: float = 0.7


@dataclass
class Config:
    ollama: OllamaConfig = field(default_factory=OllamaConfig)
    api: ApiConfig = field(default_factory=ApiConfig)
    ocr: OcrConfig = field(default_factory=OcrConfig)
    work_dir: str = "./work"


def _merge(base: dict, override: dict) -> dict:
    for key, value in override.items():
        if isinstance(value, dict) and isinstance(base.get(key), dict):
            _merge(base[key], value)
        else:
            base[key] = value
    return base


def load_config(config_dir: Path | None = None) -> Config:
    config_dir = config_dir or Path.cwd()

    data: dict = {}

    default_path = config_dir / "config.yaml"
    if default_path.exists():
        with open(default_path, encoding="utf-8") as f:
            data = yaml.safe_load(f) or {}

    local_path = config_dir / "config.local.yaml"
    if local_path.exists():
        with open(local_path, encoding="utf-8") as f:
            local = yaml.safe_load(f) or {}
            _merge(data, local)

    env_map = {
        "RECIPE_EXTRACTOR_OLLAMA_URL": ("ollama", "base_url"),
        "RECIPE_EXTRACTOR_OLLAMA_MODEL": ("ollama", "model"),
        "RECIPE_EXTRACTOR_API_URL": ("api", "base_url"),
        "RECIPE_EXTRACTOR_LANG": ("ocr", "lang"),
        "RECIPE_EXTRACTOR_DPI": ("ocr", "dpi"),
        "RECIPE_EXTRACTOR_WORK_DIR": ("work_dir",),
    }
    for env_var, keys in env_map.items():
        value = os.environ.get(env_var)
        if value is not None:
            target = data
            for key in keys[:-1]:
                target = target.setdefault(key, {})
            target[keys[-1]] = value

    ollama_data = data.get("ollama", {})
    api_data = data.get("api", {})
    ocr_data = data.get("ocr", {})

    return Config(
        ollama=OllamaConfig(
            base_url=str(ollama_data.get("base_url", OllamaConfig.base_url)),
            model=str(ollama_data.get("model", OllamaConfig.model)),
            timeout=float(ollama_data.get("timeout", OllamaConfig.timeout)),
        ),
        api=ApiConfig(
            base_url=str(api_data.get("base_url", ApiConfig.base_url)),
        ),
        ocr=OcrConfig(
            lang=str(ocr_data.get("lang", OcrConfig.lang)),
            dpi=int(ocr_data.get("dpi", OcrConfig.dpi)),
            confidence_threshold=float(
                ocr_data.get("confidence_threshold", OcrConfig.confidence_threshold)
            ),
        ),
        work_dir=str(data.get("work_dir", Config.work_dir)),
    )
