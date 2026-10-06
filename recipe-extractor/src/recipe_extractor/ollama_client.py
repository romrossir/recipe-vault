from __future__ import annotations

import httpx

from .config import OllamaConfig


class OllamaClient:
    def __init__(self, config: OllamaConfig):
        self._client = httpx.Client(
            base_url=config.base_url,
            timeout=config.timeout,
        )
        self._model = config.model

    def generate(self, system_prompt: str, user_message: str) -> str:
        response = self._client.post(
            "/api/chat",
            json={
                "model": self._model,
                "stream": False,
                "messages": [
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": user_message},
                ],
            },
        )
        response.raise_for_status()
        data = response.json()
        return data["message"]["content"]

    def close(self) -> None:
        self._client.close()
