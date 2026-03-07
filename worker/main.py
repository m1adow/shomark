import os
import json
import requests
import whisper
from moviepy.editor import VideoFileClip

# --- Configuration ---
OLLAMA_URL = "http://ollama:11434/api/generate"
MODEL = "qwen3:30b-a3b"  # Recommended for 2026 reasoning/math tasks
CLIP_DURATION = 60  # seconds

# 1. Transcribe with Timestamps
def transcribe_video(video_path):
    print("--- Transcribing Video (Ukrainian) ---")
    # Using 'base' or 'turbo' for better Ukrainian accuracy than 'tiny'
    model = whisper.load_model("tiny") 
    result = model.transcribe(video_path, language="uk")
    # We keep the start/end/text structure
    return result['segments']

# 2. Map-Reduce Strategy (Time-Aware)
def split_segments_into_chunks(segments, num_chunks=3):
    """Splits segments list into equal parts based on timing."""
    avg_len = len(segments) // num_chunks
    chunks = [segments[i:i + avg_len] for i in range(0, len(segments), avg_len)]
    return chunks

def map_highlights(chunk, chunk_id):
    print(f"--- MAP: Processing Chunk {chunk_id} ---")
    
    mini_transcript = [{"s": s['start'], "t": s['text']} for s in chunk]
    
    prompt = f"""/no_think
Ти — експерт з вірального контенту для університетів та профорієнтації.
Твоя аудиторія — старшокласники 15-18 років та їх батьки, які обирають університет.

Ось транскрипт фрагменту відео з профорієнтаційного заходу університету:
{json.dumps(mini_transcript, ensure_ascii=False)}

ЗАВДАННЯ:
Знайди 2 найбільш захоплюючі моменти тривалістю рівно {CLIP_DURATION} секунд.
Обчисли точний час 'start' та 'end' (end = start + {CLIP_DURATION}).

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

    payload = {
        "model": MODEL,
        "prompt": prompt,
        "stream": False,
        "options": {"temperature": 0.1} # Low temp for precise math
    }

    try:
        response = requests.post(OLLAMA_URL, json=payload)
        data = response.json()
        raw = data.get('response', '').strip()
        # Extract JSON array from response
        start_idx = raw.find('[')
        end_idx = raw.rfind(']') + 1
        if start_idx == -1 or end_idx == 0:
            print("No JSON array found in response")
            return []
        return json.loads(raw[start_idx:end_idx])
    except Exception as e:
        print(f"Error in MAP: {e}")
        return []

def reduce_highlights(candidates):
    print("--- REDUCE: Selecting top 3 highlights ---")
    prompt = f"""/no_think
Ти — SMM-менеджер університету, який збирає контент для Instagram Reels та TikTok.
Твоя аудиторія — старшокласники 15-18 років, які обирають університет.

З цих {len(candidates)} кандидатів вибери 3 найкращі кліпи:
{json.dumps(candidates, ensure_ascii=False)}

Критерії вибору (від важливого до менш важливого):
1. Емоційний вплив — чи викликає відео емоцію (захват, мотивацію, цікавість)?
2. Унікальність — чи є щось чого немає в інших університетах?
3. Практична цінність — чи корисна ця інформація для абітурієнта?
4. Різноманітність — обирай кліпи на різні теми (не два про одне й те саме)

Поверни ТІЛЬКИ список з 3 об'єктів JSON у тому ж форматі (без пояснень)."""

    payload = {
        "model": MODEL,
        "prompt": prompt,
        "stream": False,
        "options": {"temperature": 0.1}
    }

    try:
        response = requests.post(OLLAMA_URL, json=payload)
        data = response.json()
        raw = data.get('response', '').strip()
        start_idx = raw.find('[')
        end_idx = raw.rfind(']') + 1
        if start_idx == -1 or end_idx == 0:
            return candidates[:3]
        return json.loads(raw[start_idx:end_idx])
    except Exception as e:
        print(f"Error in REDUCE: {e}")
        return candidates[:3]

# 3. Precise Cutting
def process_highlights(video_path, highlights):
    print("--- Cutting Clips ---")
    if not os.path.exists("outputs"): os.makedirs("outputs")
    
    video = VideoFileClip(video_path)
    
    for i, clip in enumerate(highlights):
        start = clip['start']
        end = clip['end']
        
        # Guard against duration errors
        end = min(end, video.duration)
        if end - start < 10: continue # Skip glitches
            
        print(f"🎬 Cutting: {start}s to {end}s")
        new_clip = video.subclip(start, end)
        output = f"outputs/highlight_{i+1}.mp4"
        new_clip.write_videofile(output, codec="libx264", audio_codec="aac")
        
        # Save Metadata
        with open(f"{output}.txt", "w", encoding="utf-8") as f:
            f.write(f"Title: {clip.get('title')}\nReason: {clip.get('reason')}")

    video.close()

if __name__ == "__main__":
    VIDEO_FILE = "your_video.mp4"
    
    # 1. Transcribe
    segments = transcribe_video(VIDEO_FILE)
    
    # 2. Map (Find candidates in chunks)
    chunks = split_segments_into_chunks(segments, num_chunks=3)
    all_candidates = []
    for i, chunk in enumerate(chunks):
        all_candidates.extend(map_highlights(chunk, i+1))
    
    # 3. Reduce (Select best 3)
    final_highlights = reduce_highlights(all_candidates)
    
    # 4. Process
    process_highlights(VIDEO_FILE, final_highlights)