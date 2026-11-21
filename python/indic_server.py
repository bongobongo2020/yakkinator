"""
Yakkinator - Indic-Speak API Server
FastAPI wrapper around bodhan-ai/indic-speak (Llama-3.2-3B speech LM -> SNAC codes -> Vocos, 24 kHz).
Launched as a subprocess by the .NET application, from its own venv (it needs transformers v5,
which the IndexTTS environment can't share).

The model repo ships its own inference.py (class TTS); we import it from the downloaded model
directory rather than re-implementing the prompt format and decoder.
"""
import sys
for _stream in (sys.stdout, sys.stderr):
    if hasattr(_stream, 'reconfigure'):
        _stream.reconfigure(encoding='utf-8', errors='replace', line_buffering=True)
import os
import re
import io
import uuid
import asyncio
import argparse
import threading
from contextlib import asynccontextmanager

import numpy as np
from fastapi import FastAPI, HTTPException
from fastapi.responses import Response
from pydantic import BaseModel
import uvicorn

SAMPLE_RATE = 24000
# The model's own cap (max_new_tokens=2520) is roughly half a minute of audio, so long
# texts are split at sentence boundaries and the pieces joined with a short pause.
MAX_CHUNK_CHARS = 280
PAUSE_SECONDS = 0.25

tts_engine = None
load_error = None
_infer_lock = threading.Lock()


def _load(model_dir: str):
    global tts_engine, load_error
    try:
        if not os.path.isfile(os.path.join(model_dir, "inference.py")):
            raise FileNotFoundError(f"inference.py not found in {model_dir}. Install Indic-Speak from the Setup page.")
        # inference.py imports its vocos/ package relative to the model folder
        sys.path.insert(0, model_dir)
        os.chdir(model_dir)
        from inference import TTS  # noqa: E402  (shipped with the model)
        print(f"[INDIC] Loading Indic-Speak from {model_dir}...")
        tts_engine = TTS(model_dir)
        print("[INDIC] Indic-Speak loaded successfully.")
    except Exception as e:  # surfaced through /api/health and the log
        load_error = f"{type(e).__name__}: {e}"
        print(f"[INDIC] FATAL: Failed to load Indic-Speak: {load_error}")
        sys.exit(1)


@asynccontextmanager
async def lifespan(app: FastAPI):
    _load(os.environ["INDIC_MODEL_DIR"])
    yield
    print("[INDIC] Shutting down...")


# No CORS: the only client is the local .NET app.
app = FastAPI(title="Yakkinator Indic-Speak API", lifespan=lifespan)


class TtsRequest(BaseModel):
    text: str
    speaker: str
    style: str | None = None
    temperature: float = 0.6
    top_p: float = 0.9
    top_k: int = 50
    max_new_tokens: int = 2520
    seed: int | None = None


# Sentence ends: Latin . ! ?, Devanagari danda/double danda, Urdu full stop, and line breaks
_SENTENCE_END = re.compile(r'(?<=[.!?।॥۔؟])\s+|\n+')
_CLAUSE_END = re.compile(r'(?<=[,;:،])\s+')


def _split_text(text: str) -> list[str]:
    """Packs sentences into chunks of at most MAX_CHUNK_CHARS; over-long sentences split at commas, then spaces."""
    pieces: list[str] = []
    for sentence in (s.strip() for s in _SENTENCE_END.split(text)):
        if not sentence:
            continue
        if len(sentence) <= MAX_CHUNK_CHARS:
            pieces.append(sentence)
            continue
        for clause in (c.strip() for c in _CLAUSE_END.split(sentence)):
            while len(clause) > MAX_CHUNK_CHARS:
                cut = clause.rfind(' ', 0, MAX_CHUNK_CHARS)
                if cut <= 0:
                    cut = MAX_CHUNK_CHARS
                pieces.append(clause[:cut].strip())
                clause = clause[cut:].strip()
            if clause:
                pieces.append(clause)

    chunks: list[str] = []
    for piece in pieces:
        if chunks and len(chunks[-1]) + 1 + len(piece) <= MAX_CHUNK_CHARS:
            chunks[-1] += ' ' + piece
        else:
            chunks.append(piece)
    return chunks


def _to_numpy(wav) -> np.ndarray:
    if hasattr(wav, 'detach'):  # torch tensor
        wav = wav.detach().float().cpu().numpy()
    return np.asarray(wav, dtype=np.float32).reshape(-1)


def _wav_bytes(audio: np.ndarray) -> bytes:
    import soundfile as sf
    buf = io.BytesIO()
    sf.write(buf, audio, SAMPLE_RATE, format='WAV', subtype='PCM_16')
    return buf.getvalue()


@app.get("/api/health")
async def health():
    return {
        "status": "ready" if tts_engine is not None else ("error" if load_error else "loading"),
        "model": "Indic-Speak",
        "error": load_error,
    }


@app.post("/api/tts")
def generate(req: TtsRequest):
    """Plain `def`: FastAPI runs it in a worker thread so /api/health stays responsive."""
    if tts_engine is None:
        raise HTTPException(status_code=503, detail="Model not loaded yet")
    text = req.text.strip()
    if not text:
        raise HTTPException(status_code=400, detail="Text is empty")
    if not req.speaker.strip():
        raise HTTPException(status_code=400, detail="Pick a speaker")

    chunks = _split_text(text)
    pause = np.zeros(int(SAMPLE_RATE * PAUSE_SECONDS), dtype=np.float32)
    parts: list[np.ndarray] = []
    kwargs = dict(
        speaker=req.speaker.strip(),
        temperature=req.temperature,
        top_p=req.top_p,
        top_k=req.top_k,
        max_new_tokens=req.max_new_tokens,
    )
    if req.style and req.style.strip():
        kwargs["style"] = req.style.strip()

    try:
        with _infer_lock:
            for i, chunk in enumerate(chunks):
                print(f"[INDIC] Chunk {i + 1}/{len(chunks)} ({len(chunk)} chars)")
                # A fixed seed is offset per chunk so repeated phrases don't sound stamped out
                seed = None if req.seed is None else req.seed + i
                audio = _to_numpy(tts_engine(chunk, seed=seed, **kwargs))
                if parts:
                    parts.append(pause)
                parts.append(audio)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"{type(e).__name__}: {e}")

    if not parts:
        raise HTTPException(status_code=500, detail="Generation produced no audio")

    audio = np.concatenate(parts)
    peak = float(np.max(np.abs(audio))) if audio.size else 0.0
    if peak > 1.0:
        audio = audio / peak
    job_id = str(uuid.uuid4())[:8]
    return Response(content=_wav_bytes(audio), media_type="audio/wav", headers={"X-Job-Id": job_id})


@app.post("/api/shutdown")
async def shutdown():
    loop = asyncio.get_running_loop()
    loop.call_later(0.5, lambda: os._exit(0))
    return {"status": "shutting_down"}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=5302)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--model-dir", required=True)
    args = parser.parse_args()

    os.environ["INDIC_MODEL_DIR"] = os.path.abspath(args.model_dir)
    uvicorn.run(app, host=args.host, port=args.port, log_level="info")
