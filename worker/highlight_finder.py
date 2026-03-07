import json
import logging

from config import Config
from llm import LLMClient

logger = logging.getLogger(__name__)


class HighlightFinder:
    """Map-Reduce highlight detection using an LLM."""

    def __init__(self, llm: LLMClient, config: Config) -> None:
        self._llm = llm
        self._clip_duration = config.clip_duration
        self._map_chunks = config.map_chunks
        self._top_highlights = config.top_highlights

    # --- Map Phase ---

    def _build_map_prompt(self, chunk: list[dict]) -> str:
        mini_transcript = [{"s": s["start"], "t": s["text"]} for s in chunk]
        return f"""/no_think
Ти — експерт з вірального контенту для університетів та профорієнтації.
Твоя аудиторія — старшокласники 15-18 років та їх батьки, які обирають університет.

Ось транскрипт фрагменту відео з профорієнтаційного заходу університету:
{json.dumps(mini_transcript, ensure_ascii=False)}

ЗАВДАННЯ:
Знайди 2 найбільш захоплюючі моменти тривалістю рівно {self._clip_duration} секунд.
Обчисли точний час 'start' та 'end' (end = start + {self._clip_duration}).

Шукай моменти які містять:
- Вражаючі факти про університет, кампус або студентське життя
- Успішні історії випускників або студентів (кар'єра, досягнення, зарплата)
- Унікальні можливості: стипендії, стажування, міжнародні програми, обміни
- Сучасні лабораторії, обладнання або інноваційні проекти
- Емоційні або мотиваційні моменти від викладачів чи студентів
- Конкретні переваги спеціальності: попит на ринку праці, перспективи
- Цікаві факти про навчання, які здивують абітурієнтів

Уникай: організаційні оголошення, технічні паузи, вітання та прощання.

Поверни ТІЛЬКИ JSON масив (без пояснень):
[
  {{"start": 10.5, "end": 70.5, "title": "Короткий заголовок для Reels", "reason": "Чому це зачепить абітурієнта"}},
  {{"start": 150.0, "end": 210.0, "title": "Короткий заголовок для Reels", "reason": "Чому це зачепить абітурієнта"}}
]"""

    def map_highlights(self, chunk: list[dict], chunk_id: int) -> list[dict]:
        """Find candidate highlights in one transcript chunk."""
        logger.info("MAP: Processing chunk %d (%d segments)", chunk_id, len(chunk))
        prompt = self._build_map_prompt(chunk)
        candidates = self._llm.generate_json_array(prompt)
        logger.info("MAP chunk %d: found %d candidates", chunk_id, len(candidates))
        return candidates

    # --- Reduce Phase ---

    def _build_reduce_prompt(self, candidates: list[dict]) -> str:
        return f"""/no_think
Ти — SMM-менеджер університету, який збирає контент для Instagram Reels та TikTok.
Твоя аудиторія — старшокласники 15-18 років, які обирають університет.

З цих {len(candidates)} кандидатів вибери {self._top_highlights} найкращі кліпи:
{json.dumps(candidates, ensure_ascii=False)}

Критерії вибору (від важливого до менш важливого):
1. Емоційний вплив — чи викликає відео емоцію (захват, мотивацію, цікавість)?
2. Унікальність — чи є щось чого немає в інших університетах?
3. Практична цінність — чи корисна ця інформація для абітурієнта?
4. Різноманітність — обирай кліпи на різні теми (не два про одне й те саме)

Поверни ТІЛЬКИ список з {self._top_highlights} об'єктів JSON у тому ж форматі (без пояснень)."""

    def reduce_highlights(self, candidates: list[dict]) -> list[dict]:
        """Select top N highlights from all candidates."""
        logger.info("REDUCE: Selecting top %d from %d candidates", self._top_highlights, len(candidates))
        prompt = self._build_reduce_prompt(candidates)
        result = self._llm.generate_json_array(prompt)
        if not result:
            logger.warning("REDUCE returned empty; falling back to first %d candidates", self._top_highlights)
            return candidates[: self._top_highlights]
        return result

    # --- Public API ---

    def find_highlights(self, segments: list[dict]) -> list[dict]:
        """Run full map-reduce pipeline: split segments, map, reduce."""
        chunks = self._split_segments(segments, self._map_chunks)

        all_candidates: list[dict] = []
        for i, chunk in enumerate(chunks):
            all_candidates.extend(self.map_highlights(chunk, i + 1))

        if not all_candidates:
            logger.warning("No candidates found in any chunk")
            return []

        return self.reduce_highlights(all_candidates)

    @staticmethod
    def _split_segments(segments: list[dict], num_chunks: int) -> list[list[dict]]:
        avg_len = max(1, len(segments) // num_chunks)
        return [segments[i : i + avg_len] for i in range(0, len(segments), avg_len)]
