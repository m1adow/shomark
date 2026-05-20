import json
import logging
import time
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
        additional_instructions: str | None = None,
        exclude_ranges: list[dict] | None = None,
    ) -> str:
        profile = _audience_profile(target_audience)
        compact = self._compact_segments(chunk)

        # Ground the model with the chunk's actual time window so it cannot drift
        # to the example's numbers (e.g. always returning 0–60 s).
        chunk_start = compact[0]["s"] if compact else 0.0
        chunk_end = compact[-1]["e"] if compact else 0.0
        max_start = max(chunk_start, chunk_end - self._clip_duration)
        time_window_block = (
            f"ДІАПАЗОН ЦЬОГО ФРАГМЕНТА: {chunk_start:.1f}s – {chunk_end:.1f}s. "
            f"Поле 'start' має бути в межах [{chunk_start:.1f}, {max_start:.1f}] — НЕ використовуй 0, "
            f"якщо діапазон починається не з 0."
        )


        exclude_block = ""
        if exclude_ranges:
            ranges_str = ", ".join(f"{r['start']:.1f}s–{r['end']:.1f}s" for r in exclude_ranges)
            exclude_block = f"\nВЖЕ ВИБРАНІ КЛІПИ (не вибирай нові, що перекривають ці діапазони): {ranges_str}\n"

        # Combine description + additional_instructions into one directive block.
        # description = user's primary intent (always set when the user typed something).
        # additional_instructions = extra refinement, only present on regeneration.
        parts = [p for p in [description, additional_instructions] if p]
        combined_instructions = "\n".join(parts) if parts else None

        # When the user provides explicit instructions, they take precedence over the
        # default "find 2 viral moments by audience criteria" behaviour. The audience
        # profile is downgraded to a styling hint so the LLM does not override the
        # user's selection rule (e.g. "a clip per profession").
        if combined_instructions:
            user_block = (
                "\n=== ГОЛОВНА ІНСТРУКЦІЯ ВІД КОРИСТУВАЧА (НАЙВИЩИЙ ПРІОРИТЕТ) ===\n"
                f"{combined_instructions}\n"
                "Дотримуйся цієї інструкції БУКВАЛЬНО. Вона визначає, ЯКІ саме моменти вибирати\n"
                "і СКІЛЬКИ їх має бути. Якщо інструкція каже \"зроби кліп на кожну X\" — поверни\n"
                "стільки кліпів, скільки X РЕАЛЬНО згадано у транскрипті (може бути 1, 3, 5, 10).\n"
                "=== КІНЕЦЬ ГОЛОВНОЇ ІНСТРУКЦІЇ ===\n"
            )
            task_line = (
                "АЛГОРИТМ:\n"
                "1) Прочитай транскрипт і випиши УСІ окремі сутності (професії / теми / пункти),\n"
                "   що згадуються у відповідності до інструкції користувача.\n"
                "2) Для КОЖНОЇ сутності знайди у транскрипті блок (s, e), де спікер вперше про неї говорить,\n"
                "   і візьми 'start' = s цього блоку (НЕ 0, НЕ початок фрагмента, якщо сутність згадана пізніше).\n"
                f"3) 'end' = start + {self._clip_duration}. Кожна сутність → один окремий об'єкт у JSON.\n"
                f"4) Якщо сутностей знайдено N, поверни саме N об'єктів (не 2, не {self._top_highlights}).\n\n"
                f"{time_window_block}\n\n"
                f"Стиль (title, reason, hashtags) — для аудиторії {profile['persona']} на {profile['platform']}, "
                f"але аудиторія НЕ є фільтром відбору."
            )
            # Use placeholders, not concrete numbers, to avoid biasing 'start' to 0/10.
            json_example = (
                "[\n"
                "  {\"start\": <реальний_час_першої_сутності>, \"end\": <start + " + str(self._clip_duration) + ">, "
                "\"title\": \"Назва сутності 1\", \"reason\": \"Чому цей кліп\", \"viral_score\": 0.8, \"hashtags\": \"#хештег1 #хештег2\"},\n"
                "  {\"start\": <реальний_час_другої_сутності>, \"end\": <start + " + str(self._clip_duration) + ">, "
                "\"title\": \"Назва сутності 2\", \"reason\": \"Чому цей кліп\", \"viral_score\": 0.8, \"hashtags\": \"#хештег1 #хештег2\"}\n"
                "  // ... стільки об'єктів, скільки сутностей знайдено\n"
                "]"
            )
        else:
            user_block = ""
            task_line = (
                f"Знайди 2 найбільш захоплюючі моменти тривалістю рівно {self._clip_duration} секунд.\n"
                f"Обчисли точний час 'start' та 'end' (end = start + {self._clip_duration}).\n"
                f"{time_window_block}\n\n"
                f"Шукай моменти, які містять:\n{profile['criteria']}\n\n"
                f"Уникай: {profile['avoid']}."
            )
            json_example = (
                "[\n"
                f"  {{\"start\": {chunk_start + 5:.1f}, \"end\": {chunk_start + 5 + self._clip_duration:.1f}, "
                "\"title\": \"Короткий заголовок українською\", \"reason\": \"Чому це зачепить аудиторію\", "
                "\"viral_score\": 0.85, \"hashtags\": \"#хештег1 #хештег2 #хештег3\"},\n"
                f"  {{\"start\": {min(chunk_start + 60, max_start):.1f}, \"end\": {min(chunk_start + 60, max_start) + self._clip_duration:.1f}, "
                "\"title\": \"Короткий заголовок українською\", \"reason\": \"Чому це зачепить аудиторію\", "
                "\"viral_score\": 0.7, \"hashtags\": \"#хештег1 #хештег2 #хештег3\"}\n"
                "]"
            )

        return f"""МОВА ВІДПОВІДІ: УКРАЇНСЬКА. Усі поля (title, reason, hashtags) — ТІЛЬКИ українською мовою.
{user_block}
Ти — експерт з вірального контенту.
Цільова аудиторія: {profile["persona"]}.
Платформи публікації: {profile["platform"]}.{exclude_block}

Ось транскрипт фрагменту відео (s — час початку в секундах, e — час кінця, t — текст):
{json.dumps(compact, ensure_ascii=False)}

ЗАВДАННЯ:
{task_line}

Для кожного моменту:
- viral_score (0.0–1.0): оцінка вірального потенціалу для цільової аудиторії
- hashtags: 3–5 релевантних хештегів для {profile["platform"]} (через пробіл, українською)

УВАГА: title, reason та hashtags — ТІЛЬКИ українською мовою!
Поверни ТІЛЬКИ JSON масив (без пояснень):
{json_example}"""

    def map_highlights(
        self,
        chunk: list[dict],
        chunk_id: int,
        target_audience: str | None,
        description: str | None,
        additional_instructions: str | None = None,
        exclude_ranges: list[dict] | None = None,
    ) -> list[dict]:
        """Find candidate highlights in one transcript chunk."""
        t0 = time.perf_counter()
        logger.info("MAP: Processing chunk %d (%d segments)", chunk_id, len(chunk))
        prompt = self._build_map_prompt(chunk, target_audience, description, additional_instructions, exclude_ranges)
        candidates = self._llm.generate_json_array(prompt)
        logger.info("MAP chunk %d: found %d candidates (%.1fs)", chunk_id, len(candidates), time.perf_counter() - t0)
        return candidates

    # --- Reduce Phase ---

    def _build_reduce_prompt(
        self,
        candidates: list[dict],
        target_audience: str | None,
        description: str | None,
        additional_instructions: str | None = None,
        exclude_ranges: list[dict] | None = None,
    ) -> str:
        profile = _audience_profile(target_audience)

        exclude_block = ""
        if exclude_ranges:
            ranges_str = ", ".join(f"{r['start']:.1f}s–{r['end']:.1f}s" for r in exclude_ranges)
            exclude_block = f"\nВЖЕ ВИБРАНІ КЛІПИ (відхили будь-який кандидат, що перекриває ці діапазони): {ranges_str}\n"

        parts = [p for p in [description, additional_instructions] if p]
        combined_instructions = "\n".join(parts) if parts else None

        # When user instructions are present, prioritise them over the default
        # "select top N viral" rule and let the LLM keep all candidates that match.
        if combined_instructions:
            user_block = (
                "\n=== ГОЛОВНА ІНСТРУКЦІЯ ВІД КОРИСТУВАЧА (НАЙВИЩИЙ ПРІОРИТЕТ) ===\n"
                f"{combined_instructions}\n"
                "Кількість кліпів у відповіді визначає ця інструкція, а НЕ замовчуване число.\n"
                "Видаляй лише дублікати або кандидатів, що НЕ відповідають інструкції.\n"
                "=== КІНЕЦЬ ГОЛОВНОЇ ІНСТРУКЦІЇ ===\n"
            )
            selection_block = (
                f"З цих {len(candidates)} кандидатів залиш УСІ, що відповідають головній інструкції користувача,\n"
                f"об'єднай дублікати (різні чанки можуть знайти той самий момент) і відсортуй за viral_score:\n"
                f"{json.dumps(candidates, ensure_ascii=False)}\n\n"
                f"Критерії: 1) точна відповідність інструкції користувача; 2) різноманітність (не дублюй однакові моменти).\n"
                f"Поверни стільки об'єктів JSON, скільки реально відповідають інструкції — не обмежуйся числом {self._top_highlights}."
            )
        else:
            user_block = ""
            selection_block = (
                f"З цих {len(candidates)} кандидатів вибери {self._top_highlights} найкращі кліпи:\n"
                f"{json.dumps(candidates, ensure_ascii=False)}\n\n"
                f"Критерії вибору (від важливого до менш важливого):\n"
                f"1. Відповідність аудиторії — чи резонує момент із потребами/інтересами цільової групи?\n"
                f"2. Емоційний вплив — чи викликає відео емоцію (захват, мотивацію, цікавість)?\n"
                f"3. Унікальність — чи є щось, чого немає в конкурентів?\n"
                f"4. Різноманітність — обирай кліпи на різні теми (не два про одне й те саме)\n\n"
                f"Поверни ТІЛЬКИ список з {self._top_highlights} об'єктів JSON у тому ж форматі (без пояснень)."
            )

        return f"""МОВА ВІДПОВІДІ: УКРАЇНСЬКА. Усі поля (title, reason, hashtags) — ТІЛЬКИ українською мовою.
{user_block}
Ти — SMM-менеджер, який готує контент для {profile["platform"]}.
Цільова аудиторія: {profile["persona"]}.{exclude_block}

{selection_block}

Для кожного об'єкта збережи всі поля (start, end, title, reason, viral_score, hashtags).
Онови viral_score та hashtags на основі фінального рейтингу.

УВАГА: title, reason та hashtags — ТІЛЬКИ українською мовою! Перепиши англійські поля українською."""

    def reduce_highlights(
        self,
        candidates: list[dict],
        target_audience: str | None,
        description: str | None,
        additional_instructions: str | None = None,
        exclude_ranges: list[dict] | None = None,
    ) -> list[dict]:
        """Select top N highlights from all candidates."""
        t0 = time.perf_counter()
        logger.info("REDUCE: Selecting top %d from %d candidates", self._top_highlights, len(candidates))

        # Trim reason text in candidates to reduce input tokens for the reduce call.
        trimmed = [
            {**c, "reason": (c.get("reason") or "")[:150]}
            for c in candidates
        ]

        prompt = self._build_reduce_prompt(trimmed, target_audience, description, additional_instructions, exclude_ranges)
        result = self._llm.generate_json_array(prompt)
        if not result:
            logger.warning("REDUCE returned empty; falling back to first %d candidates", self._top_highlights)
            return candidates[: self._top_highlights]
        logger.info("REDUCE complete: %d highlights selected (%.1fs)", len(result), time.perf_counter() - t0)
        return result

    # --- Public API ---

    @staticmethod
    def _compact_segments(
        segments: list[dict],
        max_gap: float = 2.0,
        max_block_chars: int = 300,
        max_blocks: int = 60,
    ) -> list[dict]:
        """Merge adjacent segments into paragraph blocks to reduce LLM token count.

        Segments with a gap <= max_gap seconds are joined into a single entry.
        Each block's text is capped at max_block_chars characters, and the total
        number of blocks is capped at max_blocks (evenly subsampled if exceeded).
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
                text = " ".join(cur_texts)
                blocks.append({"s": round(cur_start, 1), "e": round(cur_end, 1), "t": text[:max_block_chars]})
                cur_start = seg["start"]
                cur_end = seg["end"]
                cur_texts = [seg["text"].strip()]

        text = " ".join(cur_texts)
        blocks.append({"s": round(cur_start, 1), "e": round(cur_end, 1), "t": text[:max_block_chars]})

        # Subsample evenly when over budget to keep prompt size predictable.
        if len(blocks) > max_blocks:
            step = len(blocks) / max_blocks
            blocks = [blocks[int(i * step)] for i in range(max_blocks)]

        return blocks

    # --- Two-Pass Pipeline (user instruction mode) ---

    def _extract_entities(
        self,
        segments: list[dict],
        description: str,
        additional_instructions: str | None = None,
    ) -> list[str]:
        """Pass 1: extract distinct entities (professions, topics, etc.) from the full transcript."""
        compact = self._compact_segments(segments, max_blocks=120)
        parts = [p for p in [description, additional_instructions] if p]
        combined = "\n".join(parts)
        prompt = (
            "Ти — аналітик відеоконтенту.\n\n"
            "Ось транскрипт відео (s — час початку в секундах, e — час кінця, t — текст):\n"
            f"{json.dumps(compact, ensure_ascii=False)}\n\n"
            f"ІНСТРУКЦІЯ: {combined}\n\n"
            "Знайди у транскрипті УСІ конкретні ІМЕНОВАНІ сутності того типу, що вказаний в інструкції.\n"
            "Правило: якщо інструкція про 'IT-професії' → повертай конкретні назви професій ('DevOps-інженер', 'Frontend-розробник'), а НЕ теми ('IT-ринок', 'Баг').\n"
            "Аналогічно для будь-якого іншого типу сутностей.\n\n"
            "Відповідай ТІЛЬКИ JSON масивом рядків, без пояснень, без markdown:\n"
            '["Назва 1", "Назва 2", "Назва 3"]'
        )
        raw = self._llm.generate(prompt, temperature=0.1, think=False)
        logger.debug("TWO-PASS Pass 1 raw response: %s", raw[:600])
        start_idx = raw.find("[")
        end_idx = raw.rfind("]") + 1
        if start_idx == -1 or end_idx == 0:
            logger.warning("TWO-PASS Pass 1: no JSON array found. Raw response: %s", raw[:500])
            return []
        try:
            parsed = json.loads(raw[start_idx:end_idx])
            if isinstance(parsed, list):
                return [str(e) for e in parsed if e]
        except json.JSONDecodeError as exc:
            logger.error("TWO-PASS Pass 1: JSON parse error: %s", exc)
        return []

    def _find_timestamps_for_entities(
        self,
        segments: list[dict],
        entities: list[str],
        target_audience: str | None,
    ) -> list[dict]:
        """Pass 2: find the best timestamp for each entity in a single LLM call."""
        profile = _audience_profile(target_audience)
        compact = self._compact_segments(segments, max_blocks=120)
        chunk_start = compact[0]["s"] if compact else 0.0
        chunk_end = compact[-1]["e"] if compact else 0.0
        max_start = max(chunk_start, chunk_end - self._clip_duration)
        entities_json = json.dumps(entities, ensure_ascii=False)
        prompt = (
            "Ти — аналітик відеоконтенту.\n\n"
            "Ось транскрипт відео (s — час початку в секундах, e — час кінця, t — текст):\n"
            f"{json.dumps(compact, ensure_ascii=False)}\n\n"
            f"СПИСОК СУТНОСТЕЙ: {entities_json}\n\n"
            f"ЗАВДАННЯ: Для КОЖНОЇ сутності зі списку знайди у транскрипті той блок,\n"
            f"де спікер ВПЕРШЕ суттєво про неї говорить.\n"
            f"- 'start' = значення 's' цього блоку (секунди), обов'язково в межах [{chunk_start:.1f}, {max_start:.1f}]\n"
            f"- 'end' = start + {self._clip_duration}\n"
            f"- 'title' = назва сутності (українською)\n"
            f"- 'reason' = 1–2 речення, чому цей кліп цікавий для: {profile['persona']}\n"
            f"- 'viral_score' = 0.0–1.0\n"
            f"- 'hashtags' = 3–5 хештегів для {profile['platform']} (через пробіл, українською)\n\n"
            f"Якщо сутність НЕ знайдена у транскрипті — не включай її у відповідь.\n"
            f"Поверни ТІЛЬКИ JSON масив (без пояснень):\n"
            f'[{{"start": <число>, "end": <число>, "title": "...", "reason": "...", "viral_score": 0.8, "hashtags": "..."}}]'
        )
        results = self._llm.generate_json_array(prompt)
        logger.info("TWO-PASS Pass 2: found timestamps for %d/%d entities", len(results), len(entities))
        return results

    def _find_highlights_two_pass(
        self,
        segments: list[dict],
        target_audience: str | None,
        description: str,
        additional_instructions: str | None = None,
        exclude_ranges: list[dict] | None = None,
    ) -> list[dict]:
        """Single LLM call that follows the user instruction directly over the full transcript."""
        t0 = time.perf_counter()
        profile = _audience_profile(target_audience)

        # Sample segments evenly — avoids the time-gap merger that collapses
        # continuous speech into ~5 giant truncated blocks, losing all entity names.
        max_sample = 120
        if len(segments) > max_sample:
            step = len(segments) / max_sample
            sampled = [segments[int(i * step)] for i in range(max_sample)]
        else:
            sampled = segments

        # Human-readable format: model processes [MM:SS] text far better than raw JSON
        transcript_lines = "\n".join(
            f"[{int(seg['start']) // 60:02d}:{int(seg['start']) % 60:02d}] {seg['text'].strip()}"
            for seg in sampled
        )
        chunk_end = sampled[-1]["end"] if sampled else 0.0
        max_start = max(0.0, chunk_end - self._clip_duration)

        parts = [p for p in [description, additional_instructions] if p]
        combined = "\n".join(parts)

        prompt = (
            "Ти — відеоредактор. Тобі надано транскрипт відео та інструкцію від користувача.\n\n"
            "ТРАНСКРИПТ (формат [хх:хх] = хвилини:секунди від початку відео):\n"
            f"{transcript_lines}\n\n"
            f"ІНСТРУКЦІЯ: {combined}\n\n"
            "ЗАВДАННЯ: Виконай інструкцію БУКВАЛЬНО. Для КОЖНОЇ сутності (професії, теми, пункту тощо), "
            "про яку потрібно зробити кліп, знайди у транскрипті момент першого суттєвого згадування "
            "і поверни один JSON об'єкт.\n\n"
            f"Правила для кожного об'єкта:\n"
            f"- 'start': час у секундах від початку відео (не більше {max_start:.0f})\n"
            f"- 'end': start + {self._clip_duration}\n"
            f"- 'title': назва сутності (українською)\n"
            f"- 'reason': 1–2 речення, чому кліп цікавий для: {profile['persona']}\n"
            f"- 'viral_score': 0.0–1.0\n"
            f"- 'hashtags': 3–5 хештегів для {profile['platform']} (через пробіл, українською)\n\n"
            f"Відповідай ТІЛЬКИ JSON масивом, без пояснень:\n"
            f'[{{"start": 219, "end": {219 + self._clip_duration}, "title": "...", "reason": "...", "viral_score": 0.8, "hashtags": "..."}}]'
        )

        logger.info("USER-INSTRUCTION: single LLM call over full transcript (%d segments sampled)", len(sampled))
        results = self._llm.generate_json_array(prompt)

        if exclude_ranges and results:
            results = [
                h for h in results
                if not any(
                    not (h.get("end", 0) <= r["start"] or h.get("start", 0) >= r["end"])
                    for r in exclude_ranges
                )
            ]

        seen: set[tuple[float, float]] = set()
        deduped: list[dict] = []
        for h in results:
            key = (round(h.get("start", 0), 1), round(h.get("end", 0), 1))
            if key not in seen:
                seen.add(key)
                deduped.append(h)

        deduped.sort(key=lambda r: r.get("start", 0))
        logger.info("USER-INSTRUCTION: %d highlights in %.1fs", len(deduped), time.perf_counter() - t0)
        return deduped

    def find_highlights(
        self,
        segments: list[dict],
        target_audience: str | None = None,
        description: str | None = None,
        additional_instructions: str | None = None,
        exclude_ranges: list[dict] | None = None,
    ) -> list[dict]:
        """Run highlight detection: two-pass for user instructions, map-reduce otherwise."""
        t_total = time.perf_counter()
        logger.info(
            "Starting highlight detection — audience: %s, description: %s",
            target_audience or _DEFAULT_AUDIENCE,
            (description[:80] + "…") if description and len(description) > 80 else description,
        )

        if description:
            result = self._find_highlights_two_pass(
                segments, target_audience, description, additional_instructions, exclude_ranges
            )
            if result:
                logger.info(
                    "TWO-PASS pipeline done — %d highlights in %.1fs",
                    len(result),
                    time.perf_counter() - t_total,
                )
                return result
            logger.warning("TWO-PASS returned no highlights; skipping map-reduce (description-mode ignores user instruction)")
            return []

        chunks = self._split_segments(segments, self._map_chunks)

        all_candidates: list[dict] = []
        t_map = time.perf_counter()
        with ThreadPoolExecutor(max_workers=len(chunks)) as pool:
            futures = {
                pool.submit(self.map_highlights, chunk, i + 1, target_audience, description, additional_instructions, exclude_ranges): i
                for i, chunk in enumerate(chunks)
            }
            for future in as_completed(futures):
                try:
                    all_candidates.extend(future.result())
                except Exception:
                    logger.exception("MAP chunk %d failed", futures[future])
        logger.info("MAP phase complete: %d candidates in %.1fs", len(all_candidates), time.perf_counter() - t_map)

        if not all_candidates:
            logger.warning("No candidates found in any chunk")
            return []

        t_reduce = time.perf_counter()
        result = self.reduce_highlights(all_candidates, target_audience, description, additional_instructions, exclude_ranges)

        # Deduplicate by (start, end) — the LLM sometimes returns the same
        # timestamps from multiple map chunks or multiple reduce items.
        seen: set[tuple[float, float]] = set()
        deduped: list[dict] = []
        for h in result:
            key = (round(h.get("start", 0), 1), round(h.get("end", 0), 1))
            if key not in seen:
                seen.add(key)
                deduped.append(h)
        if len(deduped) < len(result):
            logger.info("Deduped %d duplicate highlights (kept %d)", len(result) - len(deduped), len(deduped))
        result = deduped

        logger.info(
            "Highlight detection done — map=%.1fs | reduce=%.1fs | total=%.1fs",
            t_reduce - t_map,
            time.perf_counter() - t_reduce,
            time.perf_counter() - t_total,
        )
        return result

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

