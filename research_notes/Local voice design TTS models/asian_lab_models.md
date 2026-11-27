# Open-weight text-prompted "voice design" TTS models from Chinese/Asian labs (as of late Nov 2026)

## Which models truly support creating an original voice from a free-text description, and how fine-grained is the control?

### Takeaway
Six open-weight models from Chinese labs build a new voice from a free-text description with no reference audio: Qwen3-TTS-12Hz-1.7B-VoiceDesign (Alibaba), VoxCPM2 (OpenBMB), MOSS-VoiceGenerator (OpenMOSS), FireRedTTS3-Instruct (Xiaohongshu), and Ming-3.0-tts-0.5B / Ming-Omni-TTS (Ant Group). Spark-TTS offers only coarse attribute knobs (gender, pitch, rate). Several models named in the brief do NOT do description-based voice creation: Step-Audio-EditX, IndexTTS-2/2.5, CosyVoice 3, and FireRedTTS-2. Those models clone from reference audio and/or add emotion or style instructions on top of a cloned voice.

### Cited Findings
**Qwen3-TTS VoiceDesign (Alibaba Qwen), released 2026-01-22**
- The VoiceDesign variant exists only at 1.7B. The 0.6B models are CustomVoice and Base only. The release table marks VoiceDesign and 1.7B-CustomVoice as instruction-controlled; 0.6B-CustomVoice and the Base models are not. — [QwenLM/Qwen3-TTS GitHub](https://github.com/QwenLM/Qwen3-TTS)
- It does voice design from natural-language descriptions and "can define acoustic attributes, persona, and background." The model card says instructions control timbre, emotion, and prosody. — [Qwen blog](https://qwen.ai/blog?id=qwen3tts-0115); [HF model card](https://huggingface.co/Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign)
- The series was released 2026-01-22. I found no newer open Qwen TTS or Omni release with voice design (no Qwen3.5-Omni TTS found). Alibaba Cloud's hosted Qwen3-TTS-Flash updates are API-only. — [QwenLM GitHub](https://github.com/QwenLM/Qwen3-TTS); [Alibaba Cloud notice](https://www.alibabacloud.com/en/notice/model_studio_update_of_qwen3ttsflash_and_qwen3ttsflashrealtime_models_645)

**VoxCPM2 (OpenBMB), released 2026-04, supersedes VoxCPM1.5 / 0.5B**
- You do voice design by putting the description in parentheses at the start of the text: `"(your voice description)The text to synthesize."` It also supports controllable cloning. — [OpenBMB/VoxCPM GitHub](https://github.com/OpenBMB/VoxCPM)
- The model card says the description controls "gender, age, tone, emotion, pace…". Accent is not explicitly listed. — [HF openbmb/VoxCPM2](https://huggingface.co/openbmb/VoxCPM2)
- VoxCPM1.5 and VoxCPM-0.5B are older and are not described as having voice design. VoxCPM2 is marked "Latest". — [OpenBMB/VoxCPM GitHub](https://github.com/OpenBMB/VoxCPM)

**MOSS-VoiceGenerator (OpenMOSS / Fudan)**
- This is a dedicated model for "creation of custom speaker timbres from free-form textual descriptions," with no reference speech needed. It can also act as a voice-design front end for other TTS systems. The flagship MOSS-TTS is a cloning model; voice design lives in the separate MOSS-VoiceGenerator. — [HF MOSS-VoiceGenerator](https://huggingface.co/OpenMOSS-Team/MOSS-VoiceGenerator); [OpenMOSS/MOSS-TTS GitHub](https://github.com/OpenMOSS/MOSS-TTS)
- MOSS-TTSD is the dialogue model and is not a voice-design model. — [OpenMOSS/MOSS-TTS GitHub](https://github.com/OpenMOSS/MOSS-TTS)

**FireRedTTS3-Instruct (FireRedTeam / Xiaohongshu), released 2026-08-13, supersedes FireRedTTS-2**
- One unified model does cloning, "instruction-controlled voice design," and speech editing. It first generates a text "voice-attribute plan" and then synthesizes. The example instruction asks for a young woman's gentle voice, slightly slow, a bit playful. — [HF FireRedTeam/FireRedTTS3](https://huggingface.co/FireRedTeam/FireRedTTS3); [alphaxiv paper 2608.17492](https://www.alphaxiv.org/abs/2608.17492)
- FireRedTTS (2024), FireRedTTS-1S, and FireRedTTS-2 (podcast and dialogue, 2025) are cloning systems without instruction-driven voice design. — [FireRedTTS-2 arXiv](https://arxiv.org/html/2509.02020v1); [FireRedTTS-1S arXiv](https://arxiv.org/html/2503.20499v3)
- Xinference lists fireredtts3-instruct with the ability "text2audio_voice_design". — [Xinference docs](https://inference.readthedocs.io/en/latest/models/builtin/audio/fireredtts3-instruct.html)

**Ming-3.0-tts / Ming-Omni-TTS (Ant Group, inclusionAI)**
- Ming-Omni-TTS offers zero-shot voice design from natural-language descriptions plus 100+ built-in voices. — [inclusionAI blog](https://inclusionai.github.io/blog)
- Ming-3.0-tts-0.5B (dense, Qwen2.5-0.5B backbone with a DiT acoustic head) does TTS, cloning, natural-language voice design, and 14 paralinguistic tags. Release dates are inconsistent: Sept 22 vs Oct 20, 2026. — [AlphaSignal news](https://alphasignal.ai/news/ant-group-s-ming-3-0-tops-open-source-voice-charts-with-five-speech-models)
- Naming is inconsistent across sources (Ming-omni-tts-0.5B, Ming-omni-tts-16.8B-A3B, Ming-3.0-tts-0.5B). — [HF inclusionAI Ming collection](https://huggingface.co/collections/inclusionAI/ming); [vLLM recipe](https://recipes.vllm.ai/inclusionAI/Ming-omni-tts-0.5B)

**Spark-TTS (SparkAudio), released 2025-03**
- It creates "virtual speakers" by adjusting gender, pitch, and speaking rate. These are coarse attributes, not free-text descriptions. — [HF SparkAudio/Spark-TTS-0.5B](https://huggingface.co/SparkAudio/Spark-TTS-0.5B)

**Models that do NOT support description-based voice creation**
- Step-Audio-EditX (StepFun, 3B) does zero-shot cloning from prompt audio plus editing of emotion, style, paralinguistics, speed, denoise, and VAD. It has no text-described voice creation. The 2026-01-29 update added paralinguistic tags and vLLM support. — [stepfun-ai/Step-Audio-EditX GitHub](https://github.com/stepfun-ai/Step-Audio-EditX)
- StepFun's newer "StepAudio 3 Gen" reportedly has voice design but is API-only, with no weights. — [awesome-text-to-speech list](https://github.com/ai4s-research/awesome-text-to-speech) (community list, unverified)
- IndexTTS 2.5 (Bilibili, Jan 2026; arXiv 2601.03888) is zero-shot cloning with emotion control disentangled from timbre. Its text-based emotion control uses a separate QwenEmotion model (`use_qwen_emo=True`). It does not create timbre from text. — [IndexTTS 2.5 report](https://arxiv.org/html/2601.03888); [HF indexteam/indextts-2.5](https://huggingface.co/indexteam/indextts-2.5)
- CosyVoice 3 (Fun-CosyVoice3-0.5B-2512) has instruct support for "languages, dialects, emotions, speed, volume". Its documented route is reference-based zero-shot cloning, and the latest roadmap entry is 2025/12. — [FunAudioLLM/CosyVoice GitHub](https://github.com/FunAudioLLM/CosyVoice)

**Other 2026 candidates (lightly sourced)**
- Breeze TTS 2 (3B) reportedly does cloning plus text-described voice design. Its code is Apache-2.0 but its weights are non-commercial. — [awesome-text-to-speech](https://github.com/ai4s-research/awesome-text-to-speech); [web roundup](https://www.cekura.ai/blogs/open-source-tts)
- VoiceSculptor (arXiv 2601.10629) is an academic voice-design model that appears as a baseline in Qwen's and FireRed's tables. It is Chinese-only in those tables. — [VoiceSculptor arXiv](https://arxiv.org/pdf/2601.10629)
- MiMo-Audio-7B-Instruct (Xiaomi) appears as a voice-design baseline in Qwen's InstructTTSEval table. — [QwenLM GitHub](https://github.com/QwenLM/Qwen3-TTS)

### Inferences
- For "raspy middle-aged man with a warm British accent" style prompts, the strongest candidates are Qwen3-TTS-VD, FireRedTTS3-Instruct, VoxCPM2, and MOSS-VoiceGenerator. Ming-3.0 is a lighter 0.5B option.
- The "VoxCPM 1.5" and "FireRedTTS-2" entries in the brief are superseded by VoxCPM2 and FireRedTTS3 respectively. MOSS-TTSD is the wrong MOSS model; MOSS-VoiceGenerator is the right one.

### Gaps
- No primary source confirms whether accent control (e.g. "British accent" in English) works reliably for any model. Model cards list gender, age, timbre, emotion, and pace, but accent is rarely explicit.
- I did not fetch Breeze TTS 2's org or repo, so its lab origin and details are unverified.
- I could not verify the official Ming-3.0 GitHub/HF voice-design API.

## Quality reports and benchmarks

### Takeaway
On InstructTTSEval-style metrics (APS/DSD/RP, Gemini-judged), FireRedTTS3-Instruct claims to beat Qwen3-TTS-VD, especially on English RP (72.0 vs 64.2). Qwen3-TTS-VD in turn beats MiMo-Audio, VoiceSculptor, Hume, and Parler in its own table. VoxCPM2 and MOSS-VoiceGenerator publish no comparable numbers. Ming-3.0 ranks first among open weights on Artificial Analysis "Controlled Voice" (1,069 Elo), but one source calls open-ended voice design a weak point relative to Qwen3-TTS.

### Cited Findings
- **Qwen's own InstructTTSEval Voice Design table (APS / DSD / RP):**
  - Qwen3TTS-12Hz-1.7B-VD: ZH 85.2 / 81.1 / 65.1; EN 82.9 / 82.4 / 68.4.
  - MiMo-Audio-7B-Instruct: ZH 75.7 / 74.3 / 61.5; EN 80.6 / 77.6 / 59.5.
  - Hume: EN 83.0 / 75.3 / 54.3.
  - VoiceSculptor: ZH 75.7 / 64.7 / 61.5.
  - Parler-tts-large: EN 60.0 / 45.9 / 31.2.
  - — [QwenLM/Qwen3-TTS GitHub](https://github.com/QwenLM/Qwen3-TTS)
- **FireRed's Instruct-TTS table (Gemini-2.5-pro judge, APS / DSD / RP):**
  - FireRedTTS3-Instruct: ZH 85.8 / 82.0 / 69.7; EN 80.7 / 82.3 / 72.0.
  - Qwen3-TTS-VD: ZH 83.7 / 81.7 / 65.8; EN 76.4 / 81.4 / 64.2.
  - The table also includes MOSS-VoiceGenerator, Ming-Omni-TTS-16B-A3B, and VoiceSculptor-VD (the latter two ZH only).
  - Note that FireRed's Qwen scores differ from Qwen's self-reported ones (EN APS 76.4 vs 82.9), so this is a re-evaluation by a competitor.
  - — [HF FireRedTeam/FireRedTTS3](https://huggingface.co/FireRedTeam/FireRedTTS3)
- Ming-Omni-TTS is "on par with Qwen3-TTS" on Instruct-TTS-Eval-zh. — [inclusionAI blog](https://inclusionai.github.io/blog)
- Ming-3.0-tts ranks first on the Artificial Analysis Controlled Voice open-weights leaderboard (1,069 Elo), with Pronunciation Robustness of 93.2%. — [AlphaSignal](https://alphasignal.ai/news/ant-group-s-ming-3-0-tops-open-source-voice-charts-with-five-speech-models)
- A search summary of a news piece said open-ended voice design is a Ming weak spot relative to Qwen3-TTS. — (search-result summary of [AlphaSignal](https://alphasignal.ai/news/ant-group-s-ming-3-0-tops-open-source-voice-charts-with-five-speech-models); the fetched page did not repeat this, so treat it as unconfirmed)
- MOSS-VoiceGenerator reports only an internal subjective eval: 160 samples, rated on overall preference, instruction following, and naturalness. It claims to outperform all TTS systems with no predefined voices. There are no InstructTTSEval numbers. — [HF MOSS-VoiceGenerator](https://huggingface.co/OpenMOSS-Team/MOSS-VoiceGenerator)
- The VoxCPM2 model card warns that "Voice Design and Style Control results may vary between runs" and recommends generating 1–3 times. Quality varies by language, and very long or expressive inputs can be unstable. — [HF openbmb/VoxCPM2](https://huggingface.co/openbmb/VoxCPM2)
- Qwen3-TTS Base reports Seed-TTS WER of 0.77 (zh) and 1.24 (en). — [HF Qwen3-TTS card](https://huggingface.co/Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign)

### Inferences
- On published numbers, the top tier for English prompt adherence is FireRedTTS3-Instruct, then Qwen3-TTS-VD. VoxCPM2 is unbenchmarked for voice design but has the highest output fidelity (48 kHz).
- All benchmark numbers are self-reported by the labs. No independent head-to-head was found.

### Gaps
- I found no Reddit r/LocalLLaMA or r/StableDiffusion threads comparing these models; searches did not surface them.
- No VoxCPM2 InstructTTSEval numbers were found. The model card points to the GitHub performance tables, which I did not fetch in full.

## License, HF repo, gating, size, VRAM, latency

### Takeaway
Qwen3-TTS, VoxCPM2, MOSS-VoiceGenerator, and FireRedTTS3 are Apache-2.0, and Ming-3.0 is MIT, so all allow commercial use. Spark-TTS is CC BY-NC-SA 4.0 (non-commercial). Models are 0.5B–3B parameters and need roughly 6–16 GB of VRAM. None were reported as gated.

### Cited Findings
- **Qwen3-TTS VoiceDesign**
  - License: Apache-2.0. Repo: `Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign`. About 2B params (BF16), with tokenizer `Qwen/Qwen3-TTS-Tokenizer-12Hz`. Streaming latency is as low as 97 ms. — [HF card](https://huggingface.co/Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign)
  - VRAM: Qwen's FAQ gives 8 GB+ minimum and recommends 16 GB+. One Windows user measured about 6 GB for 1.7B. — [Mintlify Qwen3-TTS FAQ](https://www.mintlify.com/QwenLM/Qwen3-TTS/resources/faq); [dev.to Windows RTX post](https://dev.to/genelab_999/run-qwen3-tts-on-windows-with-rtx-5090-voice-cloning-in-3-seconds-elc)
- **VoxCPM2**
  - License: Apache-2.0 (code and weights). Repo: `openbmb/VoxCPM2`, 2B params. GGUF is available at `DennisHuang648/VoxCPM2-GGUF`.
  - VRAM is about 8 GB. RTF on RTX 4090 is about 0.30 in PyTorch and about 0.13 with Nano-vLLM.
  - For comparison, VoxCPM1.5 is 0.6B, about 6 GB, 44.1 kHz.
  - — [OpenBMB/VoxCPM GitHub](https://github.com/OpenBMB/VoxCPM)
- **MOSS-VoiceGenerator**
  - License: Apache-2.0. Repo: `OpenMOSS-Team/MOSS-VoiceGenerator`. 1.7B per the family table (the HF Hub shows 2B BF16). VRAM is not stated. — [HF card](https://huggingface.co/OpenMOSS-Team/MOSS-VoiceGenerator)
  - A GGUF exists for MOSS-TTS (`OpenMOSS-Team/MOSS-TTS-GGUF`), but it is not confirmed for VoiceGenerator. — [search result](https://huggingface.co/OpenMOSS-Team/MOSS-TTS-GGUF)
- **FireRedTTS3**
  - License: Apache-2.0. Repo: `FireRedTeam/FireRedTTS3` (Base and Instruct in one repo).
  - Parameter count, VRAM, and RTF are not stated on the card.
  - — [HF card](https://huggingface.co/FireRedTeam/FireRedTTS3)
- **Ming-3.0-tts-0.5B**
  - License: MIT. Repo: `inclusionAI/Ming-3.0-tts-0.5B`, collection `inclusionAI/ming-30`.
  - The sibling Ming-3.0-tts-tiny is 7.9B total / 1.3B active MoE.
  - No VRAM figure is given.
  - — [AlphaSignal](https://alphasignal.ai/news/ant-group-s-ming-3-0-tops-open-source-voice-charts-with-five-speech-models)
- **Spark-TTS-0.5B**
  - License: CC BY-NC-SA 4.0, "can only be used for non-commercial purposes". It was previously Apache 2.0. — [HF card](https://huggingface.co/SparkAudio/Spark-TTS-0.5B)
- **Step-Audio-EditX** (not voice design)
  - 3B params. VRAM: 12 GB optimal, 16 GB safer, 8–10 GB with AWQ 4-bit. Tested on Linux only. — [GitHub](https://github.com/stepfun-ai/Step-Audio-EditX)

### Inferences
- A 12 GB+ NVIDIA card should comfortably run any of the voice-design models in BF16. Exact download sizes are not stated, but a 2B BF16 model is about 4–5 GB plus its tokenizer/codec (an estimate).

### Gaps
- Exact download sizes were not stated on any card fetched.
- VRAM and RTF figures were not published for FireRedTTS3, MOSS-VoiceGenerator, or Ming-3.0.
- Gating: none of the fetched cards mentioned gating, but I did not explicitly verify the access flags.

## Install requirements and Windows support

### Takeaway
All of these models are developed Linux-first. Qwen3-TTS has the most community Windows evidence: it works if you skip flash-attn (use SDPA via `--no-flash-attn`) or use prebuilt Windows flash-attn wheels. VoxCPM2 is a plain `pip install voxcpm` with no flash-attn requirement mentioned. MOSS needs a pinned torch 2.9.1+cu128 and transformers 5.0.0. FireRedTTS3 installs from its repo's requirements.txt with no version pins documented.

### Cited Findings
- **Qwen3-TTS**
  - Install: `pip install -U qwen-tts`, Python 3.12 recommended. flash-attn is optional and only works with fp16/bf16 (`pip install -U flash-attn --no-build-isolation`). There is no Windows note in the README. — [QwenLM GitHub](https://github.com/QwenLM/Qwen3-TTS); [PyPI qwen-tts](https://pypi.org/project/qwen-tts/)
  - Windows reports say FlashAttention 2 does not compile on Windows, so use PyTorch SDPA instead. The demo accepts `--no-flash-attn`, e.g. `qwen-tts-demo Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign --device cuda:0 --dtype float16 --no-flash-attn`. Prebuilt wheels such as `flash_attn-2.8.3+cu128torch2.8.0-cp312-cp312-win_amd64.whl` are used by community guides. — [dev.to Windows guide](https://dev.to/genelab_999/run-qwen3-tts-on-windows-with-rtx-5090-voice-cloning-in-3-seconds-elc); [dev.to GUI post](https://dev.to/genelab_999/i-built-a-voice-cloning-gui-that-supports-10-languages-heres-what-i-learned-wrestling-with-cuda-30gp); [sup3rmass1ve/qwen3-tts](https://github.com/sup3rmass1ve/qwen3-tts)
- **VoxCPM2**
  - Install: `pip install voxcpm`. Requires Python ≥3.10 and <3.13, PyTorch ≥2.5.0, CUDA ≥12.0. Optional `nano-vllm-voxcpm` for serving. No Windows notes. — [OpenBMB/VoxCPM GitHub](https://github.com/OpenBMB/VoxCPM)
- **MOSS-VoiceGenerator**
  - Setup steps:
    1. `git clone https://github.com/OpenMOSS/MOSS-TTS`
    2. `pip install --extra-index-url https://download.pytorch.org/whl/cu128 -e .`
  - Python 3.12, transformers 5.0.0, torch/torchaudio 2.9.1+cu128. flash-attn is an optional extra; the `sdpa` default works. Requires `trust_remote_code=True`. — [HF card](https://huggingface.co/OpenMOSS-Team/MOSS-VoiceGenerator)
- **FireRedTTS3**
  - Setup steps:
    1. `git clone https://github.com/FireRedTeam/FireRedTTS3`
    2. `pip install -r requirements.txt`
    3. `hf download FireRedTeam/FireRedTTS3 --local-dir pretrained_models/`
  - No Python/torch pins and no Windows notes on the card. — [HF card](https://huggingface.co/FireRedTeam/FireRedTTS3)
- **CosyVoice**
  - Python 3.10. The ttsfrd wheel is Linux x86_64 only, and sox install instructions are given only for Ubuntu/CentOS. — [CosyVoice GitHub](https://github.com/FunAudioLLM/CosyVoice)
- **Step-Audio-EditX**
  - Python ≥3.12, torch ≥2.9.1, uv-based install, Linux tested only. — [GitHub](https://github.com/stepfun-ai/Step-Audio-EditX)

### Inferences
- The easiest Windows path is probably VoxCPM2 (pip package, Python 3.10–3.12, no flash-attn) or Qwen3-TTS with SDPA. MOSS and FireRedTTS3 are repo-install projects with less Windows evidence.
- FireRedTTS3's `use_wetext=True` text normalization may pull in WeTextProcessing/pynini, which is often painful on Windows. This is unverified.

### Gaps
- No Windows-specific issue reports were found for VoxCPM2, MOSS-VoiceGenerator, FireRedTTS3, or Ming-3.0.

## Minimal Python inference API and output sample rate

### Takeaway
Each model has a short official API: Qwen `Qwen3TTSModel.generate_voice_design`, VoxCPM `VoxCPM.generate` with a parenthesized description, MOSS `processor.build_user_message(text, instruction)` plus `model.generate`, and FireRed `FireRedTTS3Instruct.generate_voice_design`. Output rates are 48 kHz (VoxCPM2) and 44.1 kHz (Ming). Qwen, MOSS, and FireRed return the rate at runtime.

### Cited Findings
- **Qwen3-TTS** — [QwenLM GitHub](https://github.com/QwenLM/Qwen3-TTS)
  ```python
  from qwen_tts import Qwen3TTSModel  # module name per package qwen-tts
  model = Qwen3TTSModel.from_pretrained("Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign",
      device_map="cuda:0", dtype=torch.bfloat16, attn_implementation="flash_attention_2")  # or "sdpa" on Windows
  wavs, sr = model.generate_voice_design(text="...", language="English", instruct="<voice description>")
  sf.write("out.wav", wavs[0], sr)
  ```
  - The call supports batch lists. The sample rate is returned as `sr`; the README does not state the number.
- **VoxCPM2** — [OpenBMB/VoxCPM GitHub](https://github.com/OpenBMB/VoxCPM)
  ```python
  from voxcpm import VoxCPM
  tts = VoxCPM.from_pretrained("openbmb/VoxCPM2", load_denoiser=False)
  audio = tts.generate(text="(A calm, low-pitched narrator voice)Welcome.", cfg_value=2.0, inference_timesteps=10, seed=42)
  sf.write("out.wav", audio, tts.tts_model.sample_rate)  # 48 kHz
  ```
- **MOSS-VoiceGenerator** — [HF card](https://huggingface.co/OpenMOSS-Team/MOSS-VoiceGenerator)
  - Load `AutoProcessor` and `AutoModel` via `from_pretrained(..., trust_remote_code=True)`, then build the input with `processor.build_user_message(text=..., instruction=...)`.
  - Generate with `processor(conv, mode="generation")` → `model.generate(input_ids, attention_mask)` → `processor.decode(out)`.
  - Read the audio from `msg.audio_codes_list[0]` and the rate from `processor.model_config.sampling_rate`.
  - Recommended sampling: audio_temperature 1.5, top_p 0.6, top_k 50, repetition_penalty 1.1. The card warns the model is "sensitive to decoding hyperparameters."
- **FireRedTTS3-Instruct** — [HF card](https://huggingface.co/FireRedTeam/FireRedTTS3)
  ```python
  from fireredtts3.core import FireRedTTS3Instruct
  instruct = FireRedTTS3Instruct("pretrained_models", use_wetext=True, use_llm_tn=False)
  gen_audio, gen_audio_sr, gen_text = instruct.generate_voice_design(instruction="...", text="...")
  torchaudio.save("design.wav", gen_audio.cpu(), gen_audio_sr)
  ```
- **Ming-3.0-tts-0.5B**
  - Outputs 44.1 kHz. A vLLM recipe serves Ming-omni-tts-0.5B through an OpenAI-compatible speech API (44.1 kHz mono). — [AlphaSignal](https://alphasignal.ai/news/ant-group-s-ming-3-0-tops-open-source-voice-charts-with-five-speech-models); [vLLM recipe](https://recipes.vllm.ai/inclusionAI/Ming-omni-tts-0.5B)
- **Spark-TTS**
  - There is no Python API on the card, only the CLI `python -m cli.inference` and `webui.py`. — [HF card](https://huggingface.co/SparkAudio/Spark-TTS-0.5B)

### Inferences
- The Qwen and FireRed APIs are near drop-in equivalents (`generate_voice_design(text, instruction)`), and both return (audio, sr). VoxCPM2's single `generate()` with a prefix is the simplest to integrate.

### Gaps
- The numeric sample rates for Qwen3-TTS, MOSS-VoiceGenerator, and FireRedTTS3 are not stated in the fetched docs; all are read at runtime. Qwen3-TTS is commonly believed to be 24 kHz, but I found no source for this.
- I could not confirm the Ming-3.0 native Python API.

## Languages supported (English quality)

### Takeaway
All the voice-design models support English. Language counts are Qwen3-TTS 10, VoxCPM2 30, FireRedTTS3 24 plus 21 Chinese dialects, Ming-3.0 7, and MOSS-VoiceGenerator zh/en only. On English voice design, FireRedTTS3 and Qwen3-TTS-VD have the best published scores.

### Cited Findings
- **Qwen3-TTS:** zh, en, ja, ko, de, fr, ru, pt, es, it, plus dialect voice profiles. — [HF card](https://huggingface.co/Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign)
- **VoxCPM2:** 30 languages, including English, Hindi, Arabic, Thai, and Vietnamese, plus Chinese dialects. "Quality varies by language". — [VoxCPM GitHub](https://github.com/OpenBMB/VoxCPM); [HF card](https://huggingface.co/openbmb/VoxCPM2)
- **FireRedTTS3:** 24 languages and 21 Chinese dialects. — [HF card](https://huggingface.co/FireRedTeam/FireRedTTS3)
- **Ming-3.0-tts:** zh, en, de, es, fr, ko, ja, plus Sichuanese and Cantonese. — [AlphaSignal](https://alphasignal.ai/news/ant-group-s-ming-3-0-tops-open-source-voice-charts-with-five-speech-models)
- **MOSS-VoiceGenerator:** "High-quality synthesis in Chinese and English." — [HF card](https://huggingface.co/OpenMOSS-Team/MOSS-VoiceGenerator)
- **Spark-TTS:** English and Chinese. — [HF card](https://huggingface.co/SparkAudio/Spark-TTS-0.5B)
- **English voice-design scores (EN RP):** FireRedTTS3-Instruct 72.0 and Qwen3-TTS-VD 64.2 in FireRed's table; Qwen3-TTS-VD 68.4 in Qwen's own table. — [FireRedTTS3](https://huggingface.co/FireRedTeam/FireRedTTS3); [Qwen3-TTS](https://github.com/QwenLM/Qwen3-TTS)

### Inferences
- Because every lab trains Chinese-first, English accent rendering (British, Australian, and so on) is the likeliest weak spot. Test it directly with your target prompts.

### Gaps
- I found no independent English-only listening comparisons of these voice-design models.
