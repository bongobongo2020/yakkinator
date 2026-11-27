# Local Voice-Design TTS: Head-to-Head Comparisons and Practical Integration (as of late Nov 2026)

## Which open voice-design model do benchmarks and the community rate best (English)?

### Takeaway
Among open-weight models, Qwen3-TTS-12Hz-1.7B-VoiceDesign (Apache-2.0, released Jan 2026) has the strongest *published* English instruction-following numbers and the broadest third-party adoption. The best-scoring model in the newest benchmark (StepAudio 3 Gen, Sep 2026) is API-only. VoxCPM2 (Apache-2.0, 48 kHz) and MOSS-VoiceGenerator (Apache-2.0) are the main open alternatives. I found no independent human-preference arena that ranks open voice-design models against each other. Nearly all numbers are vendor-reported.

### Cited Findings
- InstructTTSEval (arXiv 2506.16381) has three tasks: APS (Acoustic-Parameter Specification), DSD (Descriptive-Style Directive) and RP (Role-Play). It has ~6k test cases and is scored by Gemini as an LLM judge. The authors say fine-grained acoustic control is still unsolved even for the top models — [InstructTTSEval paper](https://arxiv.org/html/2506.16381v1)
- The CapTalk paper (Apr 2026) criticizes InstructTTSEval for relying on large models to judge description-speech consistency "without sufficient human validation" — [CapTalk arXiv 2604.08363](https://arxiv.org/pdf/2604.08363) (via search summary)
- Qwen3-TTS-12Hz-1.7B-VD self-reported scores on InstructTTSEval: EN APS 82.9 / DSD 82.4 / RP 68.4; ZH APS 85.2 / DSD 81.1 / RP 65.1. Gemini-flash scores higher on APS (EN 92.3), so closed models still lead — [Qwen3-TTS benchmarks page (mintlify mirror of repo docs)](https://www.mintlify.com/QwenLM/Qwen3-TTS/resources/benchmarks); [Qwen blog](https://qwen.ai/blog?id=qwen3tts-0115)
- Qwen's blog says VoiceDesign beat MiniMax-Voice-Design on InstructTTS-Eval (vendor claim) — [Qwen blog](https://qwen.ai/blog?id=qwen3tts-0115)
- StepAudio 3 Gen Technical Report (arXiv 2609.12945, submitted 11 Sep 2026), InstructTTSEval-EN AVG: StepAudio 3 Gen 77.7, Qwen3-TTS-1.7B-VoiceDesign 73.5, Ming-omni-tts-0.5B 65.1, MOSS-VoiceGenerator 64.9. VoiceSculptor has only a ZH score (52.5). Qwen3-TTS-VD is the best open model in this third-party table — [StepAudio 3 Gen report (arXiv HTML)](https://arxiv.org/html/2609.12945)
- The same report ran a blind human Elo battle among 5 voice-design models. StepAudio 3 Gen ranked first (Elo 1668.5, aggregate win rate 75.5%, 65.3–85.7% per opponent). Per-model Elo for the others appears only in a figure I could not read — [StepAudio 3 Gen report](https://arxiv.org/html/2609.12945)
- StepAudio 3 Gen is offered as a free limited-time preview API. I found no Hugging Face weights or license for it. The older Step-Audio-TTS-3B was Apache-2.0 — [StepFun release coverage](https://runtimewire.com/article/stepfun-stepaudio-3-five-audio-models); [WaveSpeed guide](https://wavespeed.ai/blog/audio-and-voice-models/stepaudio-3-model-guide/); [Step-Audio-TTS-3B](https://huggingface.co/stepfun-ai/Step-Audio-TTS-3B)
- VoiceSculptor (arXiv 2601.10629) claims open-source SOTA on InstructTTSEval-**Zh** only. It is not relevant for English — [VoiceSculptor](https://arxiv.org/pdf/2601.10629)
- MOSS-VoiceGenerator (OpenMOSS, paper 30 Mar 2026; 1.7B; MossTTSDelay architecture; Apache-2.0) claims it "outperforms all TTS systems" with no predefined voices, based on a 160-sample subjective test. The model card gives no InstructTTSEval number. It is explicitly pitched as "a voice design layer for other TTS systems" — [MOSS-VoiceGenerator model card](https://huggingface.co/OpenMOSS-Team/MOSS-VoiceGenerator/blob/main/README.md); [arXiv 2603.28086](https://arxiv.org/abs/2603.28086)
- VoxCPM2 (OpenBMB): 2B params, 30 languages, 48 kHz output, Apache-2.0. Voice design works by putting a description in parentheses before the text, e.g. "(A young woman, gentle and sweet voice)Hello...". The HF card claims "state-of-the-art or competitive" results and points to GitHub for InstructTTSEval tables. I did not retrieve those numbers — [VoxCPM2 model card](https://huggingface.co/openbmb/VoxCPM2/blob/main/README.md)
- A 2026 roundup says Breeze TTS 2, Qwen3-TTS VoiceDesign and VoxCPM2 all now do promptable voice design, and that Maya1's remaining edge is its Apache-2.0 license. It also warns that the top two on its board are non-commercial despite being called "open source" — [Pinggy roundup](https://pinggy.io/blog/best_open_source_self_hosted_text_to_speech_models/) / [ScriptByAI](https://www.scriptbyai.com/best-open-source-tts-models/) (via search summary; I did not verify which page made which statement)
- Cekura's list (published 20 Nov 2026, licenses checked 11 Nov 2026) ranks Qwen3-TTS #3 of 10 open TTS models and confirms Apache-2.0 for code and weights. Its own benchmark (codes/addresses accuracy) put Qwen3-TTS last of 29 at 49%. That test measures read-out accuracy, not voice design — [Cekura](https://www.cekura.ai/blogs/open-source-tts)
- A vendor comparison blog (Voice Creator Pro, 2026) says VoxCPM2 is the only one of VoxCPM2/OmniVoice/Qwen3-TTS that outputs 48 kHz natively (the other two output 24 kHz). It recommends VoxCPM2 for 48 kHz output and Qwen3-TTS for directed performances — [Voice Creator Pro](https://voicecreator.pro/blog/voxcpm2-vs-omnivoice-vs-qwen3-tts) (via search snippet; fetch returned HTTP 429)
- Maya1 (MayaResearch): a Medium review says Apache-2.0, real-time streaming, voice design from natural language and 20+ emotion tags. I did not check this against an official source — [Medium review](https://medium.com/@sonublaze24/maya1-tts-by-mayaresearch-free-voice-model-complete-review-and-guide-63f8f00a5a89)
- A third-party review says Qwen3-TTS still trails commercial models such as Gemini-pro on custom-voice instruction-following, though the gap is narrowing — [search summary of 2026 guides](https://dev.to/czmilo/qwen3-tts-the-complete-2026-guide-to-open-source-voice-cloning-and-ai-speech-generation-1in6)

### Inferences
- For English voice design with open weights, Qwen3-TTS-1.7B-VoiceDesign is the defensible default. It is the only open model with an independent (StepAudio) table placing it first among open models (73.5 vs 64.9–65.1), and it has the largest tooling ecosystem.
- VoxCPM2 is the best alternative when output fidelity matters, because a 48 kHz reference suits Echo-TTS's 44.1 kHz pipeline. Its voice-design accuracy has no independent comparison I could find.
- MOSS-VoiceGenerator is designed for exactly the "design layer for another cloner" use case, but it scored lowest on InstructTTSEval-EN in the StepAudio table.
- The best-ranked model (StepAudio 3 Gen) is not usable for a local-only app.

### Gaps
- I found no r/LocalLLaMA threads or other community head-to-head comparisons of VoiceDesign vs VoxCPM2 vs MOSS vs Maya1. Searches returned only vendor or blog content.
- I found no public TTS Arena (HF/Artificial Analysis) leaderboard that is specific to voice design.
- VoxCPM2's InstructTTSEval numbers (on its GitHub) were not retrieved.
- Breeze TTS 2's license, and which "top two" models are non-commercial, are unverified.
- Per-model Elo values from StepAudio Fig. 3(a) are unreadable in HTML.

## Real-world Windows + NVIDIA experience: install pain, VRAM, speed

### Takeaway
Qwen3-TTS runs on native Windows if you skip flash-attn and use PyTorch SDPA. Forks and GUIs report this consistently. The 1.7B model needs about 4–6 GB in bf16 and 8–12 GB in fp32, so it fits 8–24 GB cards. VoxCPM2 needs ~8 GB and runs at RTF ~0.3 on a 4090. MOSS pins a recent torch (2.9.1+cu128) and Python 3.12. Very new GPUs on CUDA 13 have reported wheel availability problems.

### Cited Findings
- The official Qwen3-TTS example loads with `attn_implementation="flash_attention_2"` and bf16. Install is `pip install -U flash-attn --no-build-isolation` (with `MAX_JOBS=4` on machines with less than 96 GB RAM) — [QwenLM/Qwen3-TTS](https://github.com/QwenLM/Qwen3-TTS)
- The Qwen3-TTS-JP fork (RTX 5090 tested) says FlashAttention 2 does not work on Windows. It recommends `--no-flash-attn` so the model uses SDPA, and says startup otherwise fails with an FA2 import error. It recommends 8 GB+ VRAM — [Qwen3-TTS-JP](https://github.com/hiroki-abe-58/Qwen3-TTS-JP)
- Qwen3-TTS-GUI README: flash-attn installation commonly fails on Windows; "skip it — everything still works (just slower)". If you want to try anyway: Python 3.11, MSVC Build Tools and a CUDA toolkit matching torch, or WSL2 — [Qwen3-TTS-GUI](https://github.com/StroepWafel/Qwen3-TTS-GUI)
- A missing flash-attn produces only a warning, and the code falls back to manual PyTorch attention (issue #41, reported on macOS) — [Issue #41](https://github.com/QwenLM/Qwen3-TTS/issues/41)
- Third-party VRAM guide for 1.7B: bf16 at 4 GB min / 6 GB recommended; fp32 at 8 GB min / 12 GB recommended — [DeepWiki troubleshooting](https://deepwiki.com/mu-zi-lee/qwen3-tts-skill/10.2-troubleshooting-guide)
- RTX 5080 + CUDA 13: a user reports that flash-attn and torch builds were unavailable ("probably impossible to install on newer hardware") — [HF discussion](https://huggingface.co/Qwen/Qwen3-TTS-12Hz-1.7B-CustomVoice/discussions/19)
- Fine-tuning script `sft_12hz.py` hard-codes flash_attention_2 and fails without it (ROCm report). This only matters if fine-tuning — [Issue #372](https://github.com/QwenLM/Qwen3-TTS/issues/372)
- A spike issue in another project compares Qwen3-TTS vs VibeVoice-1.5B on an RTX 4070, showing consumer-GPU evaluation is common — [vibepod issue #17](https://github.com/JezzWTF/vibepod/issues/17) (contents not fetched)
- VoxCPM2: ~8 GB VRAM; RTF ~0.30 (standard) and ~0.13 (Nano-vLLM) on an RTX 4090; `pip install voxcpm`; Python ≥3.10, PyTorch ≥2.5, CUDA ≥12. The HF snippet writes at 16000 Hz, but you should use `model.tts_model.sample_rate` to get 48 kHz — [VoxCPM2 card](https://huggingface.co/openbmb/VoxCPM2/blob/main/README.md)
- MOSS-VoiceGenerator: Python 3.12, Transformers 5.0.0, `pyproject.toml` pins `torch==2.9.1+cu128`, flash-attn optional — [MOSS-VoiceGenerator card](https://huggingface.co/OpenMOSS-Team/MOSS-VoiceGenerator/blob/main/README.md)
- Echo-TTS needs a CUDA GPU with ≥8 GB. For 8 GB cards, set `FISH_AE_DTYPE=bfloat16` and `DEFAULT_SAMPLE_LATENT_LENGTH=576` — [jordandare/echo-tts](https://github.com/jordandare/echo-tts)

### Inferences
- In a FastAPI backend on Windows, load Qwen3-TTS with `attn_implementation="sdpa"` (or try FA2 and fall back on import error). Never make flash-attn a setup dependency.
- MOSS's pinned torch 2.9.1/cu128 and Transformers 5 could conflict with an existing app venv that already hosts Echo-TTS and IndexTTS-2. A separate venv or subprocess may be needed. Qwen3-TTS and VoxCPM2 have looser requirements.
- Running the designer and the cloner at once on 8–12 GB is tight. Unload the designer after the clip is saved; the app already has a remote unload endpoint (commit 22b5dd3).

### Gaps
- I found no measured Qwen3-TTS-VD RTF on consumer Windows GPUs with SDPA, and no sox/triton-specific Windows reports for these models.
- I found no Windows-specific reports for VoxCPM2 or MOSS.

## Is "design a voice, then clone it with another engine" common? Reference-clip tips; consistency across seeds/runs

### Takeaway
Yes. Qwen's own docs prescribe design-then-clone, though they clone with Qwen's Base model. MOSS explicitly markets itself as a design layer for other TTS engines. The consistent advice is to generate several candidates, pick one, and freeze it as a saved WAV plus transcript, then reuse that one reference forever. Designed voices are not reliably reproducible across runs (VoxCPM2 says so explicitly). Use clean, ~10–15 s references with accurate transcripts.

### Cited Findings
- Official Qwen workflow: generate a short reference clip with VoiceDesign, pass the clip and its transcript to `create_voice_clone_prompt` on the Base model, then reuse that prompt in `generate_voice_clone` — [QwenLM/Qwen3-TTS](https://github.com/QwenLM/Qwen3-TTS); [mintlify voice-design guide](https://www.mintlify.com/QwenLM/Qwen3-TTS/guides/voice-design)
- Repo guidance: use one ref_audio for all samples to improve speaker consistency and stability — [voice cloning guide](https://mintlify.com/QwenLM/Qwen3-TTS/guides/voice-cloning) (via search summary)
- A third-party guide recommends 10–15 s of clean audio plus an accurate transcript. It says quality scales roughly linearly from 3 to 15 s, then plateaus and degrades. Official docs say 3+ s suffices — [ocdevel blog](https://ocdevel.com/blog/20260302-qwen-tts-voice-cloning) / [Better Stack guide](https://betterstack.com/community/guides/ai/qwen3-tts/) (via search summary; I did not confirm which made the 3–15 s claim)
- VoxCPM2: "Voice Design and Style Control results may vary between runs"; generate 1–3 times to get the desired output — [VoxCPM2 card](https://huggingface.co/openbmb/VoxCPM2/blob/main/README.md)
- A ClawHub "Qwen3-TTS VoiceDesign" package advertises "seed-based timbre fixation" and batch seed-exploration tools. This suggests a fixed seed plus a fixed description can reproduce a timbre on the same setup — [ClawHub](https://clawhub.ai/qwen3-tts-voicedesign) (not verified)
- MOSS-VoiceGenerator can serve as "a voice design layer for other TTS systems" when suitable reference audio is hard to find — [MOSS card](https://huggingface.co/OpenMOSS-Team/MOSS-VoiceGenerator/blob/main/README.md)
- Echo-TTS can condition on up to 5 minutes of reference, and clips of ~10 s or shorter also work well. The repo does not specify format or sample rate. The example loads `speaker.wav` via `load_audio()` and the output is 44.1 kHz — [jordandare/echo-tts](https://github.com/jordandare/echo-tts)
- Echo-TTS preview `inference.py` sets `max_speaker_latent_len=2560` ("pretrained max length") with an AE downsample factor of 2048. My arithmetic gives ≈5.24 M samples, ≈119 s at 44.1 kHz, and longer references get truncated. This conflicts with the README's "up to 5 minutes" and may differ between the preview Space and the release repo — [echo-tts-preview inference.py](https://huggingface.co/spaces/jordand/echo-tts-preview/blob/main/inference.py) (via search summary)
- Echo generates up to ~30 s per call (640 latents). Front-ends chunk longer text — [Echo PyPI / derivatives](https://pypi.org/project/echo-tts/)
- IndexTTS-2: README examples pass a wav path as `spk_audio_prompt` with no stated length. The paper describes zero-shot synthesis from "a few seconds" of prompt audio. IndexTTS-2.5 also exists (Jan 2026) — [IndexTTS2 arXiv](https://arxiv.org/html/2506.21619v2); [IndexTTS-2.5](https://huggingface.co/IndexTeam/IndexTTS-2.5)
- Qwen3-TTS outputs 24 kHz and VoxCPM2 outputs 48 kHz — [Voice Creator Pro](https://voicecreator.pro/blog/voxcpm2-vs-omnivoice-vs-qwen3-tts) (search snippet); [VoxCPM2 card](https://huggingface.co/openbmb/VoxCPM2/blob/main/README.md)

### Inferences
- For this pipeline, have the designer speak a neutral, phonetically varied script of ~10–15 s, avoiding extreme emotion, so Echo and IndexTTS-2 pick up timbre rather than a one-off performance. A 10–20 s clip sits within both engines' comfortable ranges.
- Save the clip losslessly as WAV. Qwen3-TTS produces 24 kHz, and upsampling to 44.1 kHz for Echo adds no detail. VoxCPM2's 48 kHz output may give Echo more high-frequency information.
- Store the description, seed, model version and transcript alongside the WAV. Treat the WAV, not the prompt, as the voice's identity, because regeneration is not guaranteed to match.
- Offering 2–4 candidates per description in the UI matches the vendors' own "generate 1–3 times" advice.

### Gaps
- I found no published guidance on whether Echo-TTS or IndexTTS-2 prefer emotionally neutral references, or on their minimum and optimal reference lengths.
- I found no measured reports of how well a designed voice survives cloning by a different engine (speaker similarity after a cross-engine handoff).
- I could not confirm whether Qwen3-TTS-VD is deterministic under a fixed seed across GPUs or drivers.

## ComfyUI nodes, wrappers and OpenAI-compatible servers (clean inference API)

### Takeaway
Qwen3-TTS has by far the richest ecosystem: an official Python API (`generate_voice_design(text, language, instruct)`), several ComfyUI node packs with VoiceDesign nodes, community OpenAI-compatible FastAPI servers, and vLLM-Omni's `/v1/audio/speech`. The official vLLM-Omni path was offline-only at one point. For an existing FastAPI backend, calling the `qwen_tts` package directly is the cleanest option.

### Cited Findings
- Official API: `Qwen3TTSModel.from_pretrained("Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign", device_map="cuda:0", dtype=torch.bfloat16)`, then `wavs, sr = model.generate_voice_design(text=..., language=..., instruct=...)`. It accepts lists for batching — [QwenLM/Qwen3-TTS](https://github.com/QwenLM/Qwen3-TTS)
- VoiceDesign exists only at 1.7B; there is no 0.6B VoiceDesign — [Qwen3-TTS-Rulate space](https://huggingface.co/spaces/Kentuss/Qwen3-TTS-Rulate/blob/main/README.md) (via search summary)
- The Qwen repo says vLLM-Omni supports Qwen3-TTS offline only, with online serving "later". The vLLM-Omni docs now describe an OpenAI-compatible `POST /v1/audio/speech` endpoint and a Qwen3-TTS online-serving example, so the repo statement may be outdated — [QwenLM/Qwen3-TTS](https://github.com/QwenLM/Qwen3-TTS); [vLLM-Omni speech API](https://docs.vllm.ai/projects/vllm-omni/en/latest/serving/speech_api/); [vLLM-Omni qwen3_tts example](https://docs.vllm.ai/projects/vllm-omni/en/v0.18.0/user_guide/examples/online_serving/qwen3_tts/)
- Community OpenAI-style FastAPI servers: groxaxo/Qwen3-TTS-Openai-Fastapi (defaults to CustomVoice), and a dingausmwald fork that adds streaming, cloning and torch.compile on port 8880 — [groxaxo](https://github.com/groxaxo/Qwen3-TTS-Openai-Fastapi); [dingausmwald](https://github.com/dingausmwald/Qwen3-TTS-Openai-Fastapi)
- ComfyUI nodes with Voice Design: flybirdxx/ComfyUI-Qwen-TTS, DarioFT/ComfyUI-Qwen3-TTS (also fine-tuning), 1038lab/ComfyUI-QwenTTS, ComfyUI-Qwen3-TTS-Fast — [flybirdxx](https://github.com/flybirdxx/ComfyUI-Qwen-TTS); [1038lab](https://github.com/1038lab/ComfyUI-QwenTTS); [comfy.icu Fast node](https://comfy.icu/node/Qwen3TTSFastVoiceDesign); [RunComfy](https://www.runcomfy.com/comfyui-nodes/ComfyUI-Qwen3-TTS/qwen3-voice-design)
- MOSS-VoiceGenerator has a vLLM recipe and a GGUF conversion (Vokra) — [vLLM recipe](https://recipes.vllm.ai/OpenMOSS-Team/MOSS-VoiceGenerator); [vokra GGUF](https://huggingface.co/vokra/moss-voice-generator)
- Hosted Qwen VoiceDesign endpoints (fal.ai at $0.09/1k chars, DeepInfra) show the same instruct+text interface — [fal.ai](https://fal.ai/models/fal-ai/qwen-3-tts/voice-design/1.7b); [DeepInfra](https://deepinfra.com/Qwen/Qwen3-TTS-VoiceDesign/api)

### Inferences
- In-process use of `qwen_tts` (or `voxcpm`) inside the existing FastAPI app avoids an extra server. The ComfyUI node source code is a useful reference for SDPA fallback and model caching.

### Gaps
- I did not verify the maintenance status or licenses of the community wrappers.

## Licensing gotchas for a free desktop app that downloads models at setup

### Takeaway
The voice-design candidates are permissive: Qwen3-TTS, VoxCPM2 and MOSS-VoiceGenerator are Apache-2.0, and Maya1 reportedly is too. The real gotcha is downstream. Echo-TTS weights **and its generated audio** are CC-BY-NC-SA-4.0 (non-commercial, share-alike) because of the Fish Speech S1-DAC autoencoder. A free app is non-commercial, but users must be told their Echo outputs are not commercially usable.

### Cited Findings
- Qwen3-TTS repo, code and weights: Apache-2.0 — [QwenLM/Qwen3-TTS](https://github.com/QwenLM/Qwen3-TTS); [Cekura license check, Nov 2026](https://www.cekura.ai/blogs/open-source-tts)
- VoxCPM2: Apache-2.0, free for commercial use — [VoxCPM2 card](https://huggingface.co/openbmb/VoxCPM2/blob/main/README.md)
- MOSS-VoiceGenerator: Apache-2.0 per the HF metadata. A Gitee mirror says "unspecified" — [MOSS card](https://huggingface.co/OpenMOSS-Team/MOSS-VoiceGenerator/blob/main/README.md)
- Maya1: Apache-2.0 according to a secondary source only — [Medium](https://medium.com/@sonublaze24/maya1-tts-by-mayaresearch-free-voice-model-complete-review-and-guide-63f8f00a5a89)
- Echo-TTS: weights CC-BY-NC-SA-4.0, "research and non-commercial use only". Code is MIT except `autoencoder.py` (Apache-2.0). **Audio outputs are CC-BY-NC-SA-4.0** due to the Fish S1-DAC dependency. Bundled audio prompts have separate terms. The card prohibits mimicking a real person's voice without explicit consent — [jordandare/echo-tts](https://github.com/jordandare/echo-tts); [jordand/echo-tts-base](https://huggingface.co/jordand/echo-tts-base)
- A 2026 roundup warns that several "open source" TTS leaders are actually non-commercial — [Pinggy](https://pinggy.io/blog/best_open_source_self_hosted_text_to_speech_models/) (via search summary)
- StepAudio 3 Gen is governed by StepFun platform/preview API terms, with no open weights — [WaveSpeed guide](https://wavespeed.ai/blog/audio-and-voice-models/stepaudio-3-model-guide/)

### Inferences
- Downloading weights at setup (rather than redistributing them) is still worth paired with showing each model's license in the installer. Apache-2.0 requires preserving the license and NOTICE text if you redistribute. Hugging Face downloads keep the repo's files.
- A designed voice is synthetic, which avoids the consent problem that cloning a real person raises. That is a product benefit to highlight. Echo's NC-SA terms still attach to anything Echo generates from that designed reference.

### Gaps
- I did not verify IndexTTS-2's weight license (bilibili IndexTeam), so it needs checking separately.
- I did not check whether the Qwen3-TTS HF model cards add any acceptable-use terms beyond Apache-2.0.
