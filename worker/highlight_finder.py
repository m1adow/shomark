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
        # Stricter rule: start/end must equal the s/e of a real block above
        # (no arbitrary arithmetic). This kills mid-word cuts and 0-anchoring.
        time_window_block = (
            f"ДІАПАЗОН ЦЬОГО ФРАГМЕНТА: {chunk_start:.1f}s – {chunk_end:.1f}s.\n"
            f"ПРАВИЛО ЧАСУ (критично):\n"
            f"- 'start' = ТОЧНО значення поля 's' одного з блоків вище. НЕ обчислюй, не округлюй до 0.\n"
            f"- 'end' = ТОЧНО значення поля 'e' блоку, де думка завершується.\n"
            f"- Тривалість (end − start) у межах [{max(self._clip_duration - 10, 20)}, {self._clip_duration + 15}] секунд.\n"
            f"- 'start' має бути в межах [{chunk_start:.1f}, {max_start:.1f}]."
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
                f"3) 'end' = момент завершення думки або речення (оптимально start + {self._clip_duration}, дозволено до start + {self._clip_duration + 15}). Кожна сутність → один окремий об'єкт у JSON.\n"
                f"4) Якщо сутностей знайдено N, поверни саме N об'єктів (не 2, не {self._top_highlights}).\n\n"
                f"{time_window_block}\n\n"
                f"Стиль (title, reason, hashtags) — для аудиторії {profile['persona']} на {profile['platform']}, "
                f"але аудиторія НЕ є фільтром відбору."
            )
            # Use placeholders, not concrete numbers, to avoid biasing 'start' to 0/10.
            json_example = (
                "[\n"
                "  {\"start\": <реальний_час_першої_сутності>, \"end\": <кінець думки, ~start + " + str(self._clip_duration) + ", max start + " + str(self._clip_duration + 15) + ">, "
                "\"title\": \"Назва сутності 1\", \"reason\": \"Чому цей кліп\", \"viral_score\": 0.8, \"hashtags\": \"#хештег1 #хештег2\"},\n"
                "  {\"start\": <реальний_час_другої_сутності>, \"end\": <кінець думки, ~start + " + str(self._clip_duration) + ", max start + " + str(self._clip_duration + 15) + ">, "
                "\"title\": \"Назва сутності 2\", \"reason\": \"Чому цей кліп\", \"viral_score\": 0.8, \"hashtags\": \"#хештег1 #хештег2\"}\n"
                "  // ... стільки об'єктів, скільки сутностей знайдено\n"
                "]"
            )
        else:
            user_block = ""
            task_line = (
                f"Знайди 2 найбільш захоплюючі моменти.\n"
                f"{time_window_block}\n\n"
                f"Шукай моменти, які містять:\n{profile['criteria']}\n\n"
                f"Уникай: {profile['avoid']}.\n\n"
                f"ЯКІСТЬ ПОЛІВ (обов'язково):\n"
                f"- 'title': іменна фраза до 8 слів, з конкретним фактом/числом/власною назвою з кліпу.\n"
                f"  ПОГАНО: \"Цікавий момент про навчання\", \"Захоплива історія студента\".\n"
                f"  ДОБРЕ: \"Стипендія 8000 грн для першокурсників ФІТ\", \"Грант на стажування у Берліні\".\n"
                f"- 'reason': 1–2 речення, починаючи з дієслова вигоди для аудиторії "
                f"(\"Приваблює…\", \"Мотивує…\", \"Демонструє…\"). Має посилатися на конкретний факт із кліпу.\n"
                f"- 'hashtags': 3–5 штук. Щонайменше 2 мають бути ВЛАСНІ назви з кліпу "
                f"(спеціальність, технологія, програма, місто, ім'я). Заборонено лише загальні: "
                f"#навчання #університет #освіта #студент без жодного конкретного.\n"
                f"- 'viral_score' (рубрика): 0.9+ = названо досягнення/конкретне число/власну назву; "
                f"0.7 = емоційний, але без конкретики; 0.5 = інформативний, але плаский; <0.5 = філер. "
                f"Розкид оцінок між кандидатами обов'язковий — НЕ став усім 0.8."
            )
            # Use real s/e from the compacted blocks for the example, so the model sees
            # values it can actually copy from the data above.
            ex1_s = compact[0]["s"] if compact else chunk_start
            ex1_e = next((b["e"] for b in compact if b["e"] - ex1_s >= self._clip_duration - 10), compact[-1]["e"] if compact else chunk_end)
            ex2_s = compact[len(compact) // 2]["s"] if len(compact) > 2 else ex1_s
            ex2_e = next((b["e"] for b in compact if b["s"] >= ex2_s and b["e"] - ex2_s >= self._clip_duration - 10), compact[-1]["e"] if compact else chunk_end)
            json_example = (
                "[\n"
                f"  {{\"start\": {ex1_s:.1f}, \"end\": {ex1_e:.1f}, "
                "\"title\": \"Стипендія 8000 грн для першокурсників ФІТ\", "
                "\"reason\": \"Приваблює абітурієнтів конкретною сумою фінансової підтримки на старті навчання.\", "
                "\"viral_score\": 0.9, \"hashtags\": \"#ФІТ #стипендія8000 #першокурсник #абітурієнт2026\"},\n"
                f"  {{\"start\": {ex2_s:.1f}, \"end\": {ex2_e:.1f}, "
                "\"title\": \"Стажування у Berlin DevHub за обміном\", "
                "\"reason\": \"Демонструє реальну міжнародну можливість після другого курсу.\", "
                "\"viral_score\": 0.75, \"hashtags\": \"#BerlinDevHub #обмін #стажування #IT_кар'єра\"}\n"
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
                f"2. Конкретність — чи є у title/reason власна назва, число або іменоване досягнення?\n"
                f"3. Емоційний вплив — чи викликає відео емоцію (захват, мотивацію, цікавість)?\n"
                f"4. Тематична різноманітність — ЗАБОРОНЕНО, щоб два фінальні кліпи мали однаковий "
                f"перший іменник у title (напр. два про \"стипендію\"). Обирай різні теми.\n\n"
                f"ОНОВЛЕННЯ ПОЛІВ:\n"
                f"- 'viral_score' (рубрика): 0.9+ = конкретне досягнення/число/власна назва; "
                f"0.7 = емоційно, але загально; 0.5 = плоско. Розкид між фіналістами обов'язковий.\n"
                f"- Якщо title чи reason у кандидата генеричні (\"Цікавий момент…\", \"Захоплива історія…\") — "
                f"ПЕРЕПИШИ їх з конкретним фактом із поля reason кандидата.\n"
                f"- Хештеги: щонайменше 2 з 3–5 — власні назви з кліпу (не лише #навчання #університет).\n\n"
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

    # --- User-Instruction Pipeline (single-call full-transcript) ---

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

        # Send the full transcript — no sampling. Context window is sized
        # dynamically below based on actual prompt length so nothing is ever dropped.
        # Human-readable format: model processes [MM:SS] text far better than raw JSON
        transcript_lines = "\n".join(
            f"[{int(seg['start']) // 60:02d}:{int(seg['start']) % 60:02d}] {seg['text'].strip()}"
            for seg in segments
        )

        parts = [p for p in [description, additional_instructions] if p]
        combined = "\n".join(parts)

        prompt = (
            "Ти — відеоредактор. Тобі надано транскрипт відео та інструкцію від користувача.\n\n"
            "ТРАНСКРИПТ (формат [хх:хх] = хвилини:секунди від початку відео):\n"
            f"{transcript_lines}\n\n"
            f"ІНСТРУКЦІЯ: {combined}\n\n"
            "КРОК 1 — СПИСОК СУТНОСТЕЙ:\n"
            "Перш ніж відповідати, подумки склади нумерований список УСІХ окремих сутностей "
            "(професій / тем / пунктів), що згадуються у транскрипті відповідно до інструкції. "
            "Кількість об'єктів у фінальному JSON має ТОЧНО дорівнювати довжині цього списку — "
            "ані більше, ані менше. Якщо у транскрипті 6 професій — поверни 6 об'єктів, не 2 і не 10.\n\n"
            "КРОК 2 — ДЛЯ КОЖНОЇ СУТНОСТІ знайди у транскрипті головний блок, де спікер ДЕТАЛЬНО "
            "її розкриває (не перше побіжне згадування, а основний блок).\n\n"
            "ПРАВИЛА МЕЖ КЛІПУ (критично важливо для точності):\n"
            "- 'start': знайди рядок транскрипту, де спікер ВПЕРШЕ НАЗИВАЄ цю конкретну сутність у головному блоку "
            "(шукай рядок, що містить саму назву або її синонім). 'start' = час цього рядку у СЕКУНДАХ.\n"
            "  Конвертація [хх:хх] → секунди: хв*60 + сек. Приклад: [32:16] = 32*60+16 = 1936.\n"
            "  ЗАБОРОНА: не встановлюй 'start' на рядок, де ще говорять про ПОПЕРЕДНЮ сутність — "
            "навіть якщо він знаходиться за кілька секунд до назви твоєї сутності.\n"
            "- 'end': час, коли спікер ЗАВЕРШУЄ розмову про цю сутність і переходить до НАСТУПНОЇ "
            "(тобто рядок, де перший раз згадується наступна сутність або нова тема). "
            "'end' = час цього рядку у секундах. Діапазон: від start+30 до start+180.\n"
            "  ЗАБОРОНА: не включай у кліп початок наступної сутності.\n\n"
            "ПРАВИЛО МІНІМАЛЬНОГО ІНТЕРВАЛУ: між будь-якими двома 'start' — щонайменше 30 секунд. "
            "Якщо два кандидати потрапили б у той самий момент — обери лише той, що краще відповідає інструкції.\n\n"
            "САМОПЕРЕВІРКА перед відповіддю: для кожного кліпу переконайся, що:\n"
            "  1) рядок [хх:хх] з часом 'start' СПРАВДІ містить назву саме цієї сутності,\n"
            "  2) весь вміст між 'start' і 'end' стосується ЛИШЕ цієї сутності, без сусідніх,\n"
            "  3) жодні два кліпи НЕ мають однаковий або близький 'start' (≥ 30 секунд),\n"
            "  4) кількість об'єктів = кількість сутностей зі списку Кроку 1.\n"
            "  Якщо для сутності неможливо знайти унікальний 'start' — не включай її у відповідь.\n\n"
            f"Для кожного об'єкта:\n"
            f"- 'start': секунди (ціле число)\n"
            f"- 'end': секунди (ціле число)\n"
            f"- '_anchor': КОРОТКА копія перших 60–80 символів рядка транскрипту з часом 'start' "
            f"(формат \"[хх:хх] перші слова…\"). Використовується для перевірки — НЕ копіюй увесь рядок, "
            f"достатньо початку. Це поле має бути ЯКОМОГА КОРОТШИМ, щоб JSON не обірвався.\n"
            f"- 'title': назва сутності (українською, іменна фраза до 8 слів, без шаблонів типу "
            f"\"Цікавий момент\" — лише конкретна назва сутності)\n"
            f"- 'reason': 1–2 речення, починай з дієслова вигоди (\"Приваблює…\", \"Демонструє…\", "
            f"\"Мотивує…\"), посилайся на конкретний факт. Аудиторія: {profile['persona']}\n"
            f"- 'viral_score' (рубрика): 0.9+ = названо досягнення/число/власну назву; 0.7 = емоційно, "
            f"але загально; 0.5 = інформативно, але плоско. Розкид між кліпами обов'язковий.\n"
            f"- 'hashtags' = 3–5 хештегів для {profile['platform']} (через пробіл, українською). "
            f"Щонайменше 2 — власні назви з кліпу (технологія, програма, ім'я), не лише загальні теги.\n\n"
            f"Відповідай ТІЛЬКИ JSON масивом, без пояснень:\n"
            f'[{{"start": 1936, "end": 2037, "_anchor": "[32:16] …назва сутності…", '
            f'"title": "...", "reason": "...", "viral_score": 0.8, "hashtags": "..."}}]'
        )

        # Calculate the context window from actual prompt size.
        # ~3 chars per token for Ukrainian Cyrillic; reserve 8192 tokens for output
        # — each clip object with _anchor is ~150-200 tokens, so this fits ~40 clips
        # before truncation. Previous 4096 reserve truncated mid-string at ~6 clips.
        required_ctx = max(12288, len(prompt) // 3 + 8192)

        logger.info(
            "USER-INSTRUCTION: full transcript (%d segments), prompt_chars=%d, num_ctx=%d",
            len(segments), len(prompt), required_ctx,
        )
        results = self._llm.generate_json_array(prompt, num_ctx=required_ctx)

        # Anchor validation: each clip's `_anchor` must be a substring match of the
        # transcript line that *actually* corresponds to its `start`. This catches
        # the model when it copies a plausible-looking line but anchors `start`
        # into an adjacent entity's block. After validation we strip `_anchor`
        # so it never reaches downstream consumers.
        if results:
            # Build a map from (mm:ss) → segment text for fast lookup.
            line_at = {}
            for seg in segments:
                key = f"[{int(seg['start']) // 60:02d}:{int(seg['start']) % 60:02d}]"
                line_at[key] = seg["text"].strip()

            validated: list[dict] = []
            for h in results:
                anchor = (h.get("_anchor") or "").strip()
                start_s = h.get("start", 0)
                title = (h.get("title") or "").strip()
                # Find the transcript line whose [mm:ss] tag is closest to start_s.
                key = f"[{int(start_s) // 60:02d}:{int(start_s) % 60:02d}]"
                actual_line = line_at.get(key)
                anchor_ok = True
                if actual_line and anchor:
                    # Normalise: drop the [mm:ss] prefix from anchor before comparing,
                    # then require either anchor-text appears inside actual_line or
                    # vice versa (LLM sometimes truncates the line).
                    anchor_text = anchor
                    if anchor_text.startswith("["):
                        rb = anchor_text.find("]")
                        if rb != -1:
                            anchor_text = anchor_text[rb + 1:].strip()
                    needle = anchor_text[:40].lower()
                    haystack = actual_line.lower()
                    anchor_ok = bool(needle) and (needle in haystack or haystack[:40] in anchor_text.lower())
                    if not anchor_ok:
                        # Last-chance: at least the title's first word should appear
                        # in the actual line (covers paraphrased anchors).
                        first_word = title.split()[0].lower() if title else ""
                        if first_word and len(first_word) >= 4 and first_word in haystack:
                            anchor_ok = True

                if not anchor_ok:
                    logger.warning(
                        "USER-INSTRUCTION: dropped clip — anchor mismatch (start=%ss, title=%r, anchor=%r, actual=%r)",
                        start_s, title[:40], anchor[:60], (actual_line or "")[:60],
                    )
                    continue
                # Strip internal field before returning.
                h.pop("_anchor", None)
                validated.append(h)
            if len(validated) < len(results):
                logger.info(
                    "USER-INSTRUCTION: anchor validation kept %d/%d clips",
                    len(validated), len(results),
                )
            results = validated

        if exclude_ranges and results:
            results = [
                h for h in results
                if not any(
                    not (h.get("end", 0) <= r["start"] or h.get("start", 0) >= r["end"])
                    for r in exclude_ranges
                )
            ]

        # Deduplicate: two clips are considered duplicates when their starts are
        # within 30 s of each other (the LLM sometimes anchors every entity to the
        # same timestamp with only slightly different ends). Keep the highest viral_score.
        deduped: list[dict] = []
        for h in sorted(results, key=lambda x: x.get("viral_score", 0), reverse=True):
            h_start = h.get("start", 0)
            if not any(abs(h_start - kept.get("start", 0)) < 30 for kept in deduped):
                deduped.append(h)

        deduped.sort(key=lambda r: r.get("start", 0))
        if len(deduped) < len(results):
            logger.warning(
                "USER-INSTRUCTION: dropped %d near-duplicate start(s) (kept %d)",
                len(results) - len(deduped), len(deduped),
            )

        # Trim clip ends to prevent overlapping content between adjacent clips.
        # After sorting by start, cap each clip's end at the next clip's start.
        # Remove any clip whose duration falls below MIN_CLIP_DURATION after trimming.
        MIN_CLIP_DURATION = 30
        trimmed: list[dict] = []
        for i, clip in enumerate(deduped):
            end = clip.get("end", 0)
            if i + 1 < len(deduped):
                next_start = deduped[i + 1].get("start", 0)
                if end > next_start:
                    logger.debug(
                        "Trim clip %d: end %.0fs → %.0fs (next clip starts at %.0fs)",
                        i + 1, end, next_start, next_start,
                    )
                    end = next_start
            duration = end - clip.get("start", 0)
            if duration >= MIN_CLIP_DURATION:
                trimmed.append({**clip, "end": end})
            else:
                logger.warning(
                    "Removed clip too short after overlap trim: %.0fs–%.0fs (%.0fs)",
                    clip.get("start", 0), end, duration,
                )
        if len(trimmed) < len(deduped):
            logger.warning(
                "USER-INSTRUCTION: removed %d clips after overlap trim (kept %d)",
                len(deduped) - len(trimmed), len(trimmed),
            )
        deduped = trimmed

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

