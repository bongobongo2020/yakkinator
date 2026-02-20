"""
IndexTTS Studio - API Server
FastAPI wrapper around IndexTTS-2 inference engine.
Launched as a subprocess by the .NET application.
"""
import sys
import os
import uuid
import time
import argparse
from pathlib import Path
from contextlib import asynccontextmanager

from fastapi import FastAPI, UploadFile, File, Form, HTTPException
from fastapi.responses import FileResponse, JSONResponse
from fastapi.middleware.cors import CORSMiddleware
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

    try:
        from indextts.infer_v2 import IndexTTS2
        tts_engine = IndexTTS2(cfg_path=config_path, model_dir=model_dir, is_fp16=use_fp16)
        model_version = "IndexTTS-2"
        print("[API] IndexTTS-2 loaded successfully.")
    except ImportError:
        try:
            from indextts.infer import IndexTTS
            tts_engine = IndexTTS(cfg_path=config_path, model_dir=model_dir, is_fp16=use_fp16)
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


app = FastAPI(title="IndexTTS Studio API", lifespan=lifespan)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

# Ensure output directories exist
UPLOAD_DIR = Path("./uploads")
OUTPUT_DIR = Path("./outputs")
UPLOAD_DIR.mkdir(exist_ok=True)
OUTPUT_DIR.mkdir(exist_ok=True)


@app.get("/api/health")
async def health():
    """Health check endpoint."""
    return {
        "status": "ready" if tts_engine is not None else "loading",
        "model": model_version,
    }


@app.post("/api/tts")
async def generate_speech(
    voice: UploadFile = File(..., description="Reference voice WAV file"),
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
    """Generate speech from text using a reference voice."""
    if tts_engine is None:
        raise HTTPException(status_code=503, detail="Model not loaded yet")

    # Save uploaded voice file
    job_id = str(uuid.uuid4())[:8]
    voice_path = UPLOAD_DIR / f"{job_id}_voice.wav"
    with open(voice_path, "wb") as f:
        f.write(await voice.read())

    output_path = OUTPUT_DIR / f"{job_id}_output.wav"

    try:
        kwargs = {
            "spk_audio_prompt": str(voice_path),
            "text": text,
            "output_path": str(output_path),
            "verbose": True,
        }

        # Add generation params if the model supports them
        try:
            kwargs["temperature"] = temperature
            kwargs["top_p"] = top_p
            kwargs["top_k"] = top_k
        except:
            pass

        if emotion_mode == "audio" and emotion_audio is not None:
            emo_path = UPLOAD_DIR / f"{job_id}_emo.wav"
            with open(emo_path, "wb") as f:
                f.write(await emotion_audio.read())
            kwargs["emo_audio_prompt"] = str(emo_path)
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

        tts_engine.infer(**kwargs)

        if not output_path.exists():
            raise HTTPException(status_code=500, detail="Generation failed - no output file")

        return FileResponse(
            str(output_path),
            media_type="audio/wav",
            filename=f"indextts_{job_id}.wav",
            headers={"X-Job-Id": job_id}
        )

    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
    finally:
        # Cleanup voice upload (keep output for history)
        if voice_path.exists():
            voice_path.unlink()


@app.post("/api/voices/save")
async def save_voice(
    name: str = Form(...),
    audio: UploadFile = File(...),
):
    """Save a voice profile for reuse."""
    voices_dir = Path("./voices")
    voices_dir.mkdir(exist_ok=True)

    safe_name = "".join(c for c in name if c.isalnum() or c in "._- ").strip()
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
        voices.append({
            "name": f.stem,
            "path": str(f),
            "size": f.stat().st_size,
            "modified": f.stat().st_mtime,
        })
    return {"voices": sorted(voices, key=lambda v: v["modified"], reverse=True)}


@app.get("/api/outputs")
async def list_outputs():
    """List generated output files."""
    outputs = []
    for f in OUTPUT_DIR.glob("*.wav"):
        outputs.append({
            "id": f.stem,
            "path": str(f),
            "size": f.stat().st_size,
            "created": f.stat().st_ctime,
        })
    return {"outputs": sorted(outputs, key=lambda o: o["created"], reverse=True)}


@app.get("/api/outputs/{filename}")
async def get_output(filename: str):
    """Download a specific output file."""
    path = OUTPUT_DIR / filename
    if not path.exists():
        raise HTTPException(status_code=404, detail="File not found")
    return FileResponse(str(path), media_type="audio/wav")


@app.post("/api/shutdown")
async def shutdown():
    """Graceful shutdown endpoint (called by .NET app on close)."""
    import asyncio
    loop = asyncio.get_event_loop()
    loop.call_later(0.5, lambda: os._exit(0))
    return {"status": "shutting_down"}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=5299)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--model-dir", default="./checkpoints")
    parser.add_argument("--fp16", action="store_true", default=True)
    args = parser.parse_args()

    os.environ["INDEXTTS_MODEL_DIR"] = args.model_dir
    os.environ["INDEXTTS_FP16"] = "true" if args.fp16 else "false"

    uvicorn.run(app, host=args.host, port=args.port, log_level="info")
