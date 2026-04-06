import json
import logging
from concurrent.futures import ThreadPoolExecutor, as_completed

from config import Config
from llm import LLMClient

logger = logging.getLogger(__name__)

# Audience-specific persona and search criteria injected into prompts
_AUDIENCE_PROFILES: dict[str, dict] = {
    "Applicants": {
        "persona": "старшокласники 15–18 років та їхні батьки, які обирають університет",
        "platform": "Instagram Reels та TikTok",
        "criteria": (
            "- Вражаючі факти про університет, кампус або студентське життя\n"
            "- Успішні історії випускників (кар'єра, зарплата, досягнення)\n"
            "- Унікальні можливості: стипендії, стажування, міжнародні обміни\n"
            "- Сучасні лабораторії, обладнання або інноваційні проекти\n"
            "- Емоційні або мотиваційні моменти від викладачів чи студентів\n"
            "- Конкретні переваги спеціальності: попит на ринку праці, перспективи"
        ),
        "avoid": "організаційні оголошення, технічні паузи, вітання та прощання",
    },
    "Masters": {
        "persona": "фахівці з вищою освітою, які розглядають магістратуру для кар'єрного зростання",
        "platform": "LinkedIn та YouTube",
        "criteria": (
            "- Наукові досягнення та прикладні дослідження кафедри\n"
            "- Колаборації з індустрією, R&D-партнерства, гранти\n"
            "- Кар'єрні переходи та зростання зарплати після магістратури\n"
            "- Гнучкий формат навчання (вечірній, дистанційний, hybrid)\n"
            "- Унікальні авторські курси або провідні викладачі-практики\n"
            "- Міжнародна акредитація, подвійні дипломи, закордонні стажування"
        ),
        "avoid": "загальні факти про університет, базові рекламні гасла",
    },
    "Professionals": {
        "persona": "IT-спеціалісти та практики галузі, які шукають нішеві знання або сертифікацію",
        "platform": "LinkedIn, Telegram-канали та корпоративні спільноти",
        "criteria": (
            "- Прикладні технічні кейси, реальні проєкти та рішення\n"
            "- Конкретні інструменти, технології або фреймворки, що вивчаються\n"
            "- ROI навчання: як це підвищить продуктивність або рівень доходу\n"
            "- Авторитет спікерів: їхній досвід у галузі, відомі роботодавці\n"
            "- Нетворкінг і доступ до ексклюзивної спільноти практиків\n"
            "- Сертифікати, що визнаються роботодавцями або міжнародними організаціями"
        ),
        "avoid": "академічні банальності, загальні мотиваційні кліше",
    },
}

_DEFAULT_AUDIENCE = "Applicants"


def _audience_profile(target_audience: str | None) -> dict:
    """Return the audience profile for the given audience key, falling back to Applicants."""
    return _AUDIENCE_PROFILES.get(target_audience or _DEFAULT_AUDIENCE, _AUDIENCE_PROFILES[_DEFAULT_AUDIENCE])


class HighlightFinder:
    """Map-Reduce highlight detection using an LLM."""

    def __init__(self, llm: LLMClient, config: Config) -> None:
        self._llm = llm
        self._clip_duration = config.clip_duration
        self._map_chunks = config.map_chunks
        self._top_highlights = config.top_highlights

    # --- Map Phase ---

    def _build_map_prompt(
        self,
        chunk: list[dict],
        target_audience: str | None,
        description: str | None,
    ) -> str:
        profile = _audience_profile(target_audience)
        compact = self._compact_segments(chunk)

        context_block = ""
        if description:
            context_block = f"\nКОНТЕКСТ КАМПАНІЇ: {description}\n"

        return f"""/no_think
МОВА ВІДПОВІДІ: УКРАЇНСЬКА. Усі поля (title, reason, hashtags) — ТІЛЬКИ українською мовою.

Ти — експерт з вірального контенту.
Цільова аудиторія: {profile["persona"]}.
Платформи публікації: {profile["platform"]}.{context_block}

Ось транскрипт фрагменту відео (s — час початку в секундах, e — час кінця, t — текст):
{json.dumps(compact, ensure_ascii=False)}

ЗАВДАННЯ:
Знайди 2 найбільш захоплюючі моменти тривалістю рівно {self._clip_duration} секунд.
Обчисли точний час 'start' та 'end' (end = start + {self._clip_duration}).

Шукай моменти, які містять:
{profile["criteria"]}

Уникай: {profile["avoid"]}.

Для кожного моменту:
- viral_score (0.0–1.0): оцінка вірального потенціалу для цільової аудиторії
- hashtags: 3–5 релевантних хештегів для {profile["platform"]} (через пробіл, українською)

УВАГА: title, reason та hashtags — ТІЛЬКИ українською мовою!
Поверни ТІЛЬКИ JSON масив (без пояснень):
[
  {{"start": 10.5, "end": 70.5, "title": "Короткий заголовок українською", "reason": "Чому це зачепить аудиторію", "viral_score": 0.85, "hashtags": "#хештег1 #хештег2 #хештег3"}},
  {{"start": 150.0, "end": 210.0, "title": "Короткий заголовок українською", "reason": "Чому це зачепить аудиторію", "viral_score": 0.7, "hashtags": "#хештег1 #хештег2 #хештег3"}}
]"""

    def map_highlights(
        self,
        chunk: list[dict],
        chunk_id: int,
        target_audience: str | None,
        description: str | None,
    ) -> list[dict]:
        """Find candidate highlights in one transcript chunk."""
        logger.info("MAP: Processing chunk %d (%d segments)", chunk_id, len(chunk))
        prompt = self._build_map_prompt(chunk, target_audience, description)
        candidates = self._llm.generate_json_array(prompt)
        logger.info("MAP chunk %d: found %d candidates", chunk_id, len(candidates))
        return candidates

    # --- Reduce Phase ---

    def _build_reduce_prompt(
        self,
        candidates: list[dict],
        target_audience: str | None,
        description: str | None,
    ) -> str:
        profile = _audience_profile(target_audience)

        context_block = ""
        if description:
            context_block = f"\nКОНТЕКСТ КАМПАНІЇ: {description}\n"

        return f"""/no_think
МОВА ВІДПОВІДІ: УКРАЇНСЬКА. Усі поля (title, reason, hashtags) — ТІЛЬКИ українською мовою.

Ти — SMM-менеджер, який готує контент для {profile["platform"]}.
Цільова аудиторія: {profile["persona"]}.{context_block}

З цих {len(candidates)} кандидатів вибери {self._top_highlights} найкращі кліпи:
{json.dumps(candidates, ensure_ascii=False)}

Критерії вибору (від важливого до менш важливого):
1. Відповідність аудиторії — чи резонує момент із потребами/інтересами цільової групи?
2. Емоційний вплив — чи викликає відео емоцію (захват, мотивацію, цікавість)?
3. Унікальність — чи є щось, чого немає в конкурентів?
4. Різноманітність — обирай кліпи на різні теми (не два про одне й те саме)

Для кожного об'єкта збережи всі поля (start, end, title, reason, viral_score, hashtags).
Онови viral_score та hashtags на основі фінального рейтингу.

УВАГА: title, reason та hashtags — ТІЛЬКИ українською мовою! Перепиши англійські поля українською.
Поверни ТІЛЬКИ список з {self._top_highlights} об'єктів JSON у тому ж форматі (без пояснень)."""

    def reduce_highlights(
        self,
        candidates: list[dict],
        target_audience: str | None,
        description: str | None,
    ) -> list[dict]:
        """Select top N highlights from all candidates."""
        logger.info("REDUCE: Selecting top %d from %d candidates", self._top_highlights, len(candidates))
        prompt = self._build_reduce_prompt(candidates, target_audience, description)
        result = self._llm.generate_json_array(prompt)
        if not result:
            logger.warning("REDUCE returned empty; falling back to first %d candidates", self._top_highlights)
            return candidates[: self._top_highlights]
        return result

    # --- Public API ---

    @staticmethod
    def _compact_segments(segments: list[dict], max_gap: float = 2.0) -> list[dict]:
        """Merge adjacent segments into paragraph blocks to reduce LLM token count.

        Segments with a gap <= max_gap seconds are joined into a single entry.
        Returns: [{"s": start, "e": end, "t": "merged text"}, ...]
        """
        if not segments:
            return []
        blocks: list[dict] = []
        cur_start = segments[0]["start"]
        cur_end = segments[0]["end"]
        cur_texts = [segments[0]["text"].strip()]

        for seg in segments[1:]:
            if seg["start"] - cur_end <= max_gap:
                cur_end = seg["end"]
                cur_texts.append(seg["text"].strip())
            else:
                blocks.append({"s": round(cur_start, 1), "e": round(cur_end, 1), "t": " ".join(cur_texts)})
                cur_start = seg["start"]
                cur_end = seg["end"]
                cur_texts = [seg["text"].strip()]

        blocks.append({"s": round(cur_start, 1), "e": round(cur_end, 1), "t": " ".join(cur_texts)})
        return blocks

    def find_highlights(
        self,
        segments: list[dict],
        target_audience: str | None = None,
        description: str | None = None,
    ) -> list[dict]:
        """Run full map-reduce pipeline: split segments, map in parallel, reduce."""
        logger.info(
            "Starting highlight detection — audience: %s, description: %s",
            target_audience or _DEFAULT_AUDIENCE,
            (description[:80] + "…") if description and len(description) > 80 else description,
        )
        chunks = self._split_segments(segments, self._map_chunks)

        all_candidates: list[dict] = []
        with ThreadPoolExecutor(max_workers=len(chunks)) as pool:
            futures = {
                pool.submit(self.map_highlights, chunk, i + 1, target_audience, description): i
                for i, chunk in enumerate(chunks)
            }
            for future in as_completed(futures):
                try:
                    all_candidates.extend(future.result())
                except Exception:
                    logger.exception("MAP chunk %d failed", futures[future])

        if not all_candidates:
            logger.warning("No candidates found in any chunk")
            return []

        return self.reduce_highlights(all_candidates, target_audience, description)

    @staticmethod
    def _split_segments(segments: list[dict], num_chunks: int) -> list[list[dict]]:
        """Split segments into exactly num_chunks roughly equal parts."""
        n = len(segments)
        num_chunks = min(num_chunks, n)
        k, remainder = divmod(n, num_chunks)
        chunks: list[list[dict]] = []
        start = 0
        for i in range(num_chunks):
            end = start + k + (1 if i < remainder else 0)
            chunks.append(segments[start:end])
            start = end
        return chunks

