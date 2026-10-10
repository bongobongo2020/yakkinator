"""
IndexTTS Studio - API Server
FastAPI wrapper around IndexTTS-2 inference engine.
Launched as a subprocess by the .NET application.
"""
import sys
# Force UTF-8 encoding for stdout/stderr to prevent charmap errors on Windows
# when the IndexTTS tokenizer emits SentencePiece markers (e.g. ▁ ▁).
# Line buffering ensures the .NET host sees log lines as they happen when piped.
for _stream in (sys.stdout, sys.stderr):
    if hasattr(_stream, 'reconfigure'):
        _stream.reconfigure(encoding='utf-8', errors='replace', line_buffering=True)
import os
import uuid
import hashlib
import asyncio
import argparse
import threading
from pathlib import Path
from contextlib import asynccontextmanager

from fastapi import FastAPI, UploadFile, File, Form, HTTPException
from fastapi.responses import FileResponse
from starlette.background import BackgroundTask
import uvicorn

# Global TTS engine reference
tts_engine = None
model_version = None

@asynccontextmanager
async def lifespan(app: FastAPI):
    """Initialize TTS engine on startup."""
    global tts_engine, model_version

    model_dir = os.environ.get("INDEXTTS_MODEL_DIR", "./checkpoints")
    config_path = os.path.join(model_dir, "config.yaml")
    use_fp16 = os.environ.get("INDEXTTS_FP16", "true").lower() == "true"

    print(f"[API] Loading IndexTTS-2 from {model_dir} (fp16={use_fp16})...")

    # Check if model directory exists
    if not os.path.exists(model_dir):
        print(f"[API] FATAL: Model directory does not exist: {model_dir}")
        print(f"[API] Please run Setup to download models.")
        sys.exit(1)

    # Check if config.yaml exists
    if not os.path.exists(config_path):
        print(f"[API] FATAL: config.yaml not found in {model_dir}")
        print(f"[API] Please run Setup to download models.")
        sys.exit(1)

    try:
        from indextts.infer_v2 import IndexTTS2
        tts_engine = IndexTTS2(cfg_path=config_path, model_dir=model_dir, use_fp16=use_fp16)
        model_version = "IndexTTS-2"
        print("[API] IndexTTS-2 loaded successfully.")
    except ImportError:
        try:
            from indextts.infer import IndexTTS
            tts_engine = IndexTTS(cfg_path=config_path, model_dir=model_dir, use_fp16=use_fp16)
            model_version = "IndexTTS-1.5"
            print("[API] IndexTTS-1.5 loaded successfully.")
        except Exception as e:
            print(f"[API] FATAL: Failed to load any IndexTTS model: {e}")
            sys.exit(1)
    except Exception as e:
        print(f"[API] FATAL: Failed to load model: {e}")
        sys.exit(1)

    yield

    print("[API] Shutting down...")
    tts_engine = None


# No CORS middleware: the only client is the local .NET app, and a wildcard CORS
# policy would let any web page the user visits drive this server.
app = FastAPI(title="IndexTTS Studio API", lifespan=lifespan)

# Ensure output directories exist
UPLOAD_DIR = Path("./uploads")
OUTPUT_DIR = Path("./outputs")
UPLOAD_DIR.mkdir(exist_ok=True)
OUTPUT_DIR.mkdir(exist_ok=True)

AUDIO_SUFFIXES = {".wav", ".mp3", ".flac", ".ogg", ".m4a"}
MAX_CACHED_UPLOADS = 32

# The model holds GPU state and is not thread-safe; serialize inference.
_infer_lock = threading.Lock()


def _store_upload(upload: UploadFile, prefix: str) -> Path:
    """Persist an uploaded audio file under a content-hash name.

    IndexTTS caches speaker/emotion conditioning keyed by the prompt *path*, so a
    stable name per unique file lets repeated generations with the same voice skip
    re-extracting embeddings.
    """
    data = upload.file.read()
    suffix = Path(upload.filename or "").suffix.lower()
    if suffix not in AUDIO_SUFFIXES:
        suffix = ".wav"
    digest = hashlib.sha1(data).hexdigest()[:16]
    path = UPLOAD_DIR / f"{prefix}_{digest}{suffix}"
    if path.exists():
        os.utime(path)  # mark as recently used for pruning
    else:
        path.write_bytes(data)
    return path


def _prune_uploads():
    files = sorted(UPLOAD_DIR.glob("*"), key=lambda f: f.stat().st_mtime, reverse=True)
    for f in files[MAX_CACHED_UPLOADS:]:
        _unlink_quietly(f)


def _unlink_quietly(path: Path):
    try:
        path.unlink()
    except OSError:
        pass


@app.get("/api/health")
async def health():
    """Health check endpoint."""
    return {
        "status": "ready" if tts_engine is not None else "loading",
        "model": model_version,
    }


@app.post("/api/tts")
def generate_speech(
    voice: UploadFile = File(..., description="Reference voice audio file"),
    text: str = Form(..., description="Text to synthesize"),
    emotion_mode: str = Form("none", description="none|audio|vector|text"),
    emotion_audio: UploadFile = File(None, description="Emotion reference audio"),
    emotion_alpha: float = Form(0.6, description="Emotion blend strength 0.0-1.0"),
    emo_joy: float = Form(0.0),
    emo_anger: float = Form(0.0),
    emo_sadness: float = Form(0.0),
    emo_fear: float = Form(0.0),
    emo_disgust: float = Form(0.0),
    emo_melancholy: float = Form(0.0),
    emo_surprise: float = Form(0.0),
    emo_calm: float = Form(0.0),
    temperature: float = Form(1.0),
    top_p: float = Form(0.8),
    top_k: int = Form(30),
    max_tokens: int = Form(120),
):
    """Generate speech from text using a reference voice.

    Declared as a plain `def` so FastAPI runs it in a worker thread: inference is
    blocking and would otherwise stall the event loop (and /api/health) while it runs.
    """
    if tts_engine is None:
        raise HTTPException(status_code=503, detail="Model not loaded yet")
    if not text.strip():
        raise HTTPException(status_code=400, detail="Text is empty")

    job_id = str(uuid.uuid4())[:8]
    voice_path = _store_upload(voice, "spk")
    output_path = OUTPUT_DIR / f"{job_id}_output.wav"

    generation_kwargs = {"temperature": temperature, "top_p": top_p, "top_k": top_k}

    if model_version == "IndexTTS-1.5":
        # v1.5 API: different argument names and no emotion control
        kwargs = {
            "audio_prompt": str(voice_path),
            "text": text,
            "output_path": str(output_path),
            "verbose": True,
            "max_text_tokens_per_sentence": max_tokens,
            **generation_kwargs,
        }
    else:
        kwargs = {
            "spk_audio_prompt": str(voice_path),
            "text": text,
            "output_path": str(output_path),
            "verbose": True,
            "max_text_tokens_per_segment": max_tokens,
            **generation_kwargs,
        }

        if emotion_mode == "audio" and emotion_audio is not None:
            kwargs["emo_audio_prompt"] = str(_store_upload(emotion_audio, "emo"))
            kwargs["emo_alpha"] = emotion_alpha

        elif emotion_mode == "vector":
            kwargs["emo_vector"] = [
                emo_joy, emo_anger, emo_sadness, emo_fear,
                emo_disgust, emo_melancholy, emo_surprise, emo_calm
            ]
            kwargs["use_random"] = False

        elif emotion_mode == "text":
            kwargs["use_emo_text"] = True
            kwargs["emo_alpha"] = emotion_alpha

    try:
        with _infer_lock:
            tts_engine.infer(**kwargs)
    except Exception as e:
        _unlink_quietly(output_path)
        raise HTTPException(status_code=500, detail=str(e))
    finally:
        _prune_uploads()

    if not output_path.exists():
        raise HTTPException(status_code=500, detail="Generation failed - no output file")

    # The client keeps its own copy, so delete ours once sent instead of letting outputs pile up.
    return FileResponse(
        str(output_path),
        media_type="audio/wav",
        filename=f"indextts_{job_id}.wav",
        headers={"X-Job-Id": job_id},
        background=BackgroundTask(_unlink_quietly, output_path),
    )


@app.post("/api/voices/save")
async def save_voice(
    name: str = Form(...),
    audio: UploadFile = File(...),
):
    """Save a voice profile for reuse."""
    voices_dir = Path("./voices")
    voices_dir.mkdir(exist_ok=True)

    safe_name = "".join(c for c in name if c.isalnum() or c in "_- ").strip()
    if not safe_name:
        raise HTTPException(status_code=400, detail="Invalid voice name")
    voice_path = voices_dir / f"{safe_name}.wav"

    with open(voice_path, "wb") as f:
        f.write(await audio.read())

    return {"name": safe_name, "path": str(voice_path)}


@app.get("/api/voices")
async def list_voices():
    """List saved voice profiles."""
    voices_dir = Path("./voices")
    if not voices_dir.exists():
        return {"voices": []}

    voices = []
    for f in voices_dir.glob("*.wav"):
        st = f.stat()
        voices.append({
            "name": f.stem,
            "path": str(f),
            "size": st.st_size,
            "modified": st.st_mtime,
        })
    return {"voices": sorted(voices, key=lambda v: v["modified"], reverse=True)}


@app.get("/api/outputs")
async def list_outputs():
    """List generated output files."""
    outputs = []
    for f in OUTPUT_DIR.glob("*.wav"):
        st = f.stat()
        outputs.append({
            "id": f.stem,
            "path": str(f),
            "size": st.st_size,
            "created": st.st_ctime,
        })
    return {"outputs": sorted(outputs, key=lambda o: o["created"], reverse=True)}


@app.get("/api/outputs/{filename}")
async def get_output(filename: str):
    """Download a specific output file."""
    path = (OUTPUT_DIR / filename).resolve()
    # Reject traversal such as "..\\..\\secret" (backslashes are valid in a path segment)
    if path.parent != OUTPUT_DIR.resolve() or not path.is_file():
        raise HTTPException(status_code=404, detail="File not found")
    return FileResponse(str(path), media_type="audio/wav")


@app.post("/api/shutdown")
async def shutdown():
    """Graceful shutdown endpoint (called by .NET app on close)."""
    loop = asyncio.get_running_loop()
    loop.call_later(0.5, lambda: os._exit(0))
    return {"status": "shutting_down"}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=5299)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--model-dir", default="./checkpoints")
    # --fp16 / --no-fp16 (previously store_true with default=True, so fp16 could never be turned off)
    parser.add_argument("--fp16", action=argparse.BooleanOptionalAction, default=True)
    args = parser.parse_args()

    os.environ["INDEXTTS_MODEL_DIR"] = args.model_dir
    os.environ["INDEXTTS_FP16"] = "true" if args.fp16 else "false"

    uvicorn.run(app, host=args.host, port=args.port, log_level="info")
