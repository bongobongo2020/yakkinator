"""
Yakkinator - Voice Designer API Server
Creates an original voice from a text description ("a raspy middle-aged man with a warm Scottish
accent"). The app saves the clip the user picks to the voice library, where Echo-TTS and
IndexTTS-2 clone it like any other sample.

One script, three designers, each run from its own venv because their dependencies clash:
  --engine qwen    Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign (Apache-2.0): best all-round prompt
                   following; 10 languages; reads Irish/Scottish requests as American.
  --engine moss    OpenMOSS-Team/MOSS-VoiceGenerator + MOSS-Audio-Tokenizer (Apache-2.0):
                   the only one that does Irish accents; close on Scottish. Transformers 5.
  --engine voxcpm  openbmb/VoxCPM2 (Apache-2.0): best Scottish accents, 48 kHz output.
(Accent results come from Yakkinator's own test with the CommonAccent accent classifier.)
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

ENGINE_NAMES = {"qwen": "Qwen3-TTS VoiceDesign", "moss": "MOSS-VoiceGenerator", "voxcpm": "VoxCPM2"}

engine = None       # "qwen" | "moss" | "voxcpm"
designer = None     # engine-specific object with .design(text, instruct, language, opts) -> (audio, sr)
load_error = None
_infer_lock = threading.Lock()


def _device():
    import torch
    # YAK_DESIGN_DEVICE=cpu is for testing only: it works, but takes a minute or more per clip
    device = os.environ.get("YAK_DESIGN_DEVICE", "cuda:0")
    if device.startswith("cuda") and not torch.cuda.is_available():
        raise RuntimeError("No CUDA GPU found. The voice designer needs an NVIDIA GPU.")
    return device


class QwenDesigner:
    def __init__(self, model_dir):
        import torch
        from qwen_tts import Qwen3TTSModel
        device = _device()
        self.model = Qwen3TTSModel.from_pretrained(
            model_dir,
            device_map=device,
            dtype=torch.bfloat16 if device.startswith("cuda") else torch.float32,
            # flash-attn doesn't build on Windows; PyTorch's SDPA kernel is used instead
            attn_implementation="sdpa",
        )

    def design(self, text, instruct, language, opts):
        kwargs = {"max_new_tokens": opts.max_new_tokens}
        if opts.temperature is not None:
            kwargs["temperature"] = opts.temperature
        if opts.top_p is not None:
            kwargs["top_p"] = opts.top_p
        wavs, sr = self.model.generate_voice_design(text=text, instruct=instruct, language=language or "Auto", **kwargs)
        return wavs[0], sr


class MossDesigner:
    def __init__(self, model_dir, codec_dir):
        import torch
        from transformers import AutoModel, AutoProcessor
        device = _device()
        if device.startswith("cuda"):
            # MOSS's own advice: the cuDNN SDPA backend is broken for this model
            torch.backends.cuda.enable_cudnn_sdp(False)
        dtype = torch.bfloat16 if device.startswith("cuda") else torch.float32
        # Local folders, not repo ids: the remote code turns ids into Windows paths and breaks
        self.processor = AutoProcessor.from_pretrained(model_dir, trust_remote_code=True,
                                                       normalize_inputs=True, codec_path=codec_dir)
        self.model = AutoModel.from_pretrained(model_dir, trust_remote_code=True,
                                               attn_implementation="sdpa" if device.startswith("cuda") else "eager",
                                               torch_dtype=dtype).to(device).eval()
        self.device = device
        # The audio tokenizer is big (~7 GB in fp32) and only decodes here: keep it on the GPU when
        # there's room, otherwise decode on the CPU (slower, but it doesn't run out of memory)
        codec_device = "cpu"
        if device.startswith("cuda"):
            free, _ = torch.cuda.mem_get_info()
            if free > 9 * 1024 ** 3:
                codec_device = device
        self.processor.audio_tokenizer = self.processor.audio_tokenizer.to(codec_device)
        print(f"[DESIGN] MOSS audio tokenizer on {codec_device}")

    def design(self, text, instruct, language, opts):
        import torch
        batch = self.processor([[self.processor.build_user_message(text=text, instruction=instruct)]], mode="generation")
        with torch.no_grad():
            outputs = self.model.generate(input_ids=batch["input_ids"].to(self.device),
                                          attention_mask=batch["attention_mask"].to(self.device))
        message = list(self.processor.decode(outputs))[0]
        audio = message.audio_codes_list[0].float().cpu().numpy()
        return audio, self.processor.model_config.sampling_rate


class VoxCpmDesigner:
    def __init__(self, model_dir):
        from voxcpm import VoxCPM
        device = _device()
        # optimize=False: torch.compile needs Triton, which isn't reliably available on Windows
        self.model = VoxCPM.from_pretrained(model_dir, load_denoiser=False, local_files_only=True,
                                            optimize=False, device=device)

    def design(self, text, instruct, language, opts):
        # VoxCPM2 takes the description in parentheses in front of the script
        wav = self.model.generate(text=f"({instruct}){text}", cfg_value=2.0, inference_timesteps=10)
        return wav, self.model.tts_model.sample_rate


def _load(name: str, model_dir: str, codec_dir: str | None):
    global engine, designer, load_error
    try:
        print(f"[DESIGN] Loading {ENGINE_NAMES[name]} from {model_dir}...")
        if name == "qwen":
            designer = QwenDesigner(model_dir)
        elif name == "moss":
            if not codec_dir:
                raise ValueError("MOSS needs --codec-dir (the MOSS-Audio-Tokenizer folder)")
            designer = MossDesigner(model_dir, codec_dir)
        elif name == "voxcpm":
            designer = VoxCpmDesigner(model_dir)
        else:
            raise ValueError(f"Unknown designer engine: {name}")
        engine = name
        print("[DESIGN] Voice designer loaded successfully.")
    except Exception as e:  # surfaced through /api/health and the log
        load_error = f"{type(e).__name__}: {e}"
        print(f"[DESIGN] FATAL: Failed to load the voice designer: {load_error}")
        sys.exit(1)


@asynccontextmanager
async def lifespan(app: FastAPI):
    _load(os.environ["DESIGN_ENGINE"], os.environ["DESIGN_MODEL_DIR"], os.environ.get("DESIGN_CODEC_DIR") or None)
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
        "status": "ready" if designer is not None else ("error" if load_error else "loading"),
        "engine": os.environ.get("DESIGN_ENGINE"),
        "model": ENGINE_NAMES.get(os.environ.get("DESIGN_ENGINE", ""), "voice designer"),
        "error": load_error,
    }


@app.post("/api/design")
def design(req: DesignRequest):
    """One candidate voice per call. Plain `def`: FastAPI runs it in a worker thread so
    /api/health stays responsive."""
    if designer is None:
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
    try:
        with _infer_lock:
            random.seed(seed)
            np.random.seed(seed % (2**32))
            torch.manual_seed(seed)
            if torch.cuda.is_available():
                torch.cuda.manual_seed_all(seed)
            print(f"[DESIGN] {engine} seed {seed}: {instruct[:80]}")
            audio, sr = designer.design(text, instruct, req.language, req)
    except ValueError as e:  # e.g. an unsupported language
        raise HTTPException(status_code=400, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"{type(e).__name__}: {e}")

    audio = np.asarray(audio, dtype=np.float32).reshape(-1)
    if audio.size == 0:
        raise HTTPException(status_code=500, detail="Generation produced no audio")
    peak = float(np.max(np.abs(audio)))
    if peak > 1.0:
        audio = audio / peak
    return Response(content=_wav_bytes(audio, int(sr)), media_type="audio/wav",
                    headers={"X-Seed": str(seed), "X-Sample-Rate": str(int(sr)), "X-Engine": engine or ""})


@app.post("/api/shutdown")
async def shutdown():
    loop = asyncio.get_running_loop()
    loop.call_later(0.5, lambda: os._exit(0))
    return {"status": "shutting_down"}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=5303)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--engine", default="qwen", choices=sorted(ENGINE_NAMES))
    parser.add_argument("--model-dir", required=True)
    parser.add_argument("--codec-dir", default="")
    args = parser.parse_args()

    os.environ["DESIGN_ENGINE"] = args.engine
    os.environ["DESIGN_MODEL_DIR"] = os.path.abspath(args.model_dir)
    os.environ["DESIGN_CODEC_DIR"] = os.path.abspath(args.codec_dir) if args.codec_dir else ""
    uvicorn.run(app, host=args.host, port=args.port, log_level="info")
