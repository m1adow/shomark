import json
import logging
import re
import requests

from config import Config

logger = logging.getLogger(__name__)


class LLMClient:
    """Ollama LLM client for text generation."""

    def __init__(self, config: Config, model: str | None = None) -> None:
        self._url = config.ollama_url
        self._model = model or config.ollama_model
        self._timeout = config.ollama_timeout
        self._num_predict = config.ollama_num_predict
        logger.info("LLMClient initialised — model: %s, url: %s", self._model, self._url)

    def generate(self, prompt: str, temperature: float = 0.1, num_predict: int | None = None, think: bool = True, num_ctx: int | None = None) -> str:
        """Send a prompt to Ollama and return the raw response text."""
        options: dict = {
            "temperature": temperature,
            "num_predict": num_predict if num_predict is not None else self._num_predict,
        }
        if num_ctx is not None:
            options["num_ctx"] = num_ctx
        payload = {
            "model": self._model,
            "prompt": prompt,
            "stream": False,
            "options": options,
        }
        if not think:
            payload["think"] = False

        response = requests.post(self._url, json=payload, timeout=self._timeout)
        response.raise_for_status()
        data = response.json()
        logger.info(
            "LLM call complete — model: %s, prompt_tokens: %s, eval_tokens: %s, total_duration_s: %.2f",
            data.get("model", self._model),
            data.get("prompt_eval_count", "?"),
            data.get("eval_count", "?"),
            data.get("total_duration", 0) / 1e9,
        )
        raw = data.get("response", "").strip()
        # Strip <think>...</think> blocks emitted by reasoning models (e.g. Qwen3).
        raw = re.sub(r"<think>[\s\S]*?</think>", "", raw, flags=re.IGNORECASE).strip()
        logger.debug("LLM raw response (first 500 chars): %s", raw[:500])
        return raw

    def generate_json_array(self, prompt: str, temperature: float = 0.1, num_ctx: int | None = None) -> list[dict]:
        """Send a prompt and extract a JSON array from the response."""
        raw = self.generate(prompt, temperature, think=False, num_ctx=num_ctx)

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
        json_str = raw[start_idx:end_idx]
        try:
            return json.loads(json_str)
        except json.JSONDecodeError:
            pass

        # Repair common model formatting issues and retry.
        repaired = re.sub(r"//[^\n]*", "", json_str)       # strip JS-style // comments
        repaired = re.sub(r",\s*([}\]])", r"\1", repaired)  # remove trailing commas
        try:
            return json.loads(repaired)
        except json.JSONDecodeError:
            pass

        # Last resort: truncate to the last complete object.
        last_close = repaired.rfind("}")
        if last_close != -1:
            truncated = repaired[:last_close + 1] + "]"
            try:
                result = json.loads(truncated)
                if isinstance(result, list):
                    logger.warning("JSON truncated to %d complete objects", len(result))
                    return result
            except json.JSONDecodeError:
                pass

        logger.error("Failed to parse JSON from LLM response: %s", json_str[:300])
        return []

    def summarize(self, segments: list[dict]) -> str:
        """Generate a concise Ukrainian summary of the video transcript via LLM.

        Samples segments evenly across the full transcript to build a representative
        input, then asks the model to produce a 2–3 sentence summary.
        """
        if not segments:
            return ""

        # Sample up to 30 evenly-spaced segments — enough for a 2-3 sentence summary.
        max_segs = 30
        if len(segments) > max_segs:
            step = len(segments) / max_segs
            sampled = [segments[int(i * step)] for i in range(max_segs)]
        else:
            sampled = segments

        text = " ".join(seg.get("text", "").strip() for seg in sampled).strip()
        if not text:
            return ""

        # Cap input at ~1500 chars — keeps the prompt short and prompt-eval fast.
        if len(text) > 1500:
            text = text[:1500]

        prompt = (
            "Ти — асистент, який стисло переказує зміст відеозаписів.\n\n"
            f"Транскрипт відео:\n{text}\n\n"
            "Напиши короткий підсумок відео — 2–3 речення українською мовою. "
            "Опиши головну тему та ключові моменти. "
            "Не згадуй, що це транскрипт. Відповідай ТІЛЬКИ підсумком, без заголовків."
        )

        # num_ctx=1536 keeps KV-cache allocation small (vs the model default of 8 k+).
        # num_predict=150 is plenty for 2-3 sentences; capping it prevents over-generation.
        return self.generate(prompt, temperature=0.3, think=False, num_predict=150, num_ctx=1536)
