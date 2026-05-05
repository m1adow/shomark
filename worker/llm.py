import json
import logging
import requests

from config import Config

logger = logging.getLogger(__name__)


class LLMClient:
    """Ollama LLM client for text generation."""

    def __init__(self, config: Config) -> None:
        self._url = config.ollama_url
        self._model = config.ollama_model
        self._timeout = config.ollama_timeout

    def generate(self, prompt: str, temperature: float = 0.1) -> str:
        """Send a prompt to Ollama and return the raw response text."""
        payload = {
            "model": self._model,
            "prompt": prompt,
            "stream": False,
            "options": {
                "temperature": temperature,
            },
        }

        response = requests.post(self._url, json=payload, timeout=self._timeout)
        response.raise_for_status()
        raw = response.json().get("response", "").strip()
        logger.debug("LLM raw response (first 500 chars): %s", raw[:500])
        return raw

    def generate_json_array(self, prompt: str, temperature: float = 0.1) -> list[dict]:
        """Send a prompt and extract a JSON array from the response."""
        raw = self.generate(prompt, temperature)

        # With format:json Ollama returns clean JSON — try full parse first.
        try:
            parsed = json.loads(raw)
            if isinstance(parsed, list):
                return parsed
            if isinstance(parsed, dict):
                # Model sometimes wraps the array in an object key.
                for v in parsed.values():
                    if isinstance(v, list):
                        return v
        except json.JSONDecodeError:
            pass

        # Fallback: extract the [...] substring (handles markdown code blocks).
        start_idx = raw.find("[")
        end_idx = raw.rfind("]") + 1
        if start_idx == -1 or end_idx == 0:
            logger.warning("No JSON array found in LLM response (first 300 chars): %s", raw[:300])
            return []
        try:
            return json.loads(raw[start_idx:end_idx])
        except json.JSONDecodeError as e:
            logger.error("Failed to parse JSON from LLM response: %s", e)
            return []
