"""
Yakkinator - Voice Designer API Server
FastAPI wrapper around Qwen3-TTS VoiceDesign (Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign, Apache-2.0).
It creates an original voice from a text description ("a raspy middle-aged man with a warm
British accent"). The app saves the clip the user picks to the voice library, where Echo-TTS and
IndexTTS-2 clone it like any other sample.

Launched as a subprocess by the .NET application, from its own venv (qwen-tts pins
transformers 4.57.3, which the other engines can't share).
"""
import sys
for _stream in (sys.stdout, sys.stderr):
    if hasattr(_stream, 'reconfigure'):
        _stream.reconfigure(encoding='utf-8', errors='replace', line_buffering=True)
import os
import io
import random
import asyncio
import argparse
import threading
import warnings
from contextlib import asynccontextmanager

import numpy as np
from fastapi import FastAPI, HTTPException
from fastapi.responses import Response
from pydantic import BaseModel
import uvicorn

# qwen-tts imports the `sox` wrapper for a tokenizer this model doesn't use; it only warns
# when the SoX program is missing, so keep that out of the log.
warnings.filterwarnings("ignore", message=".*SoX could not be found.*")

model = None
load_error = None
_infer_lock = threading.Lock()


def _load(model_dir: str):
    global model, load_error
    try:
        import torch
        from qwen_tts import Qwen3TTSModel
        # YAK_DESIGN_DEVICE=cpu is for testing only: it works, but takes minutes per clip
        device = os.environ.get("YAK_DESIGN_DEVICE", "cuda:0")
        if device.startswith("cuda") and not torch.cuda.is_available():
            raise RuntimeError("No CUDA GPU found. The voice designer needs an NVIDIA GPU.")
        print(f"[DESIGN] Loading Qwen3-TTS VoiceDesign from {model_dir} on {device}...")
        model = Qwen3TTSModel.from_pretrained(
            model_dir,
            device_map=device,
            dtype=torch.bfloat16 if device.startswith("cuda") else torch.float32,
            # flash-attn doesn't build on Windows; PyTorch's SDPA kernel is used instead
            attn_implementation="sdpa",
        )
        print("[DESIGN] Voice designer loaded successfully.")
    except Exception as e:  # surfaced through /api/health and the log
        load_error = f"{type(e).__name__}: {e}"
        print(f"[DESIGN] FATAL: Failed to load the voice designer: {load_error}")
        sys.exit(1)


@asynccontextmanager
async def lifespan(app: FastAPI):
    _load(os.environ["DESIGN_MODEL_DIR"])
    yield
    print("[DESIGN] Shutting down...")


# No CORS: the only client is the local .NET app.
app = FastAPI(title="Yakkinator Voice Designer API", lifespan=lifespan)


class DesignRequest(BaseModel):
    text: str
    instruct: str
    language: str = "Auto"
    seed: int | None = None
    temperature: float | None = None
    top_p: float | None = None
    max_new_tokens: int = 2048


def _wav_bytes(audio: np.ndarray, sr: int) -> bytes:
    import soundfile as sf
    buf = io.BytesIO()
    sf.write(buf, audio, sr, format='WAV', subtype='PCM_16')
    return buf.getvalue()


@app.get("/api/health")
async def health():
    return {
        "status": "ready" if model is not None else ("error" if load_error else "loading"),
        "model": "Qwen3-TTS VoiceDesign",
        "error": load_error,
    }


@app.get("/api/languages")
async def languages():
    langs = None
    if model is not None:
        try:
            langs = model.model.get_supported_languages()
        except Exception:
            langs = None
    return {"languages": sorted(str(l) for l in (langs or []))}


@app.post("/api/design")
def design(req: DesignRequest):
    """One candidate voice per call. Plain `def`: FastAPI runs it in a worker thread so
    /api/health stays responsive."""
    if model is None:
        raise HTTPException(status_code=503, detail="Model not loaded yet")
    text = req.text.strip()
    instruct = req.instruct.strip()
    if not text:
        raise HTTPException(status_code=400, detail="The sample text is empty")
    if not instruct:
        raise HTTPException(status_code=400, detail="Describe the voice you want")

    import torch
    # The seed is what makes a candidate reproducible; report it so the app can store it
    seed = req.seed if req.seed is not None else random.randint(0, 2**31 - 1)
    kwargs = {"max_new_tokens": req.max_new_tokens}
    if req.temperature is not None:
        kwargs["temperature"] = req.temperature
    if req.top_p is not None:
        kwargs["top_p"] = req.top_p

    try:
        with _infer_lock:
            torch.manual_seed(seed)
            if torch.cuda.is_available():
                torch.cuda.manual_seed_all(seed)
            print(f"[DESIGN] Seed {seed}: {instruct[:80]}")
            wavs, sr = model.generate_voice_design(
                text=text,
                instruct=instruct,
                language=req.language or "Auto",
                **kwargs,
            )
    except ValueError as e:  # e.g. an unsupported language
        raise HTTPException(status_code=400, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"{type(e).__name__}: {e}")

    if not wavs:
        raise HTTPException(status_code=500, detail="Generation produced no audio")
    audio = np.asarray(wavs[0], dtype=np.float32).reshape(-1)
    peak = float(np.max(np.abs(audio))) if audio.size else 0.0
    if peak > 1.0:
        audio = audio / peak
    return Response(content=_wav_bytes(audio, int(sr)), media_type="audio/wav",
                    headers={"X-Seed": str(seed), "X-Sample-Rate": str(int(sr))})


@app.post("/api/shutdown")
async def shutdown():
    loop = asyncio.get_running_loop()
    loop.call_later(0.5, lambda: os._exit(0))
    return {"status": "shutting_down"}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=5303)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--model-dir", required=True)
    args = parser.parse_args()

    os.environ["DESIGN_MODEL_DIR"] = os.path.abspath(args.model_dir)
    uvicorn.run(app, host=args.host, port=args.port, log_level="info")
