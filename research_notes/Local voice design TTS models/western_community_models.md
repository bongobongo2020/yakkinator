# Open-weight text-prompted "voice design" TTS models (Western, independent and community labs), as of late Nov 2026

Scope note: this covers models that might create an original voice from a free-text description, for local use on Windows with an NVIDIA GPU. Chinese-lab models (Qwen3-TTS VoiceDesign, VoxCPM2, VoiceSculptor, OmniVoice from k2-fsa) appear only where sources compared against them; another researcher covers them. I used about 15 search and fetch calls. Several models in the brief (Orpheus, Sesame CSM, Kyutai TTS, NeuTTS, Kokoro) were not verified this session, and they are listed under Gaps. Items marked "background knowledge, unverified" come from my prior knowledge, not from a source fetched in this session.

## 1. Which models truly support voice creation from a free-text description, and how fine-grained is it?

### Takeaway
Three open-weight models in scope have confirmed free-text voice design: **Breeze TTS 2** (BreezeBlue, Aug 2026, the strongest by far, but non-commercial weights), **Maya1** (Maya Research, Apache 2.0, 3B), and **Parler-TTS** (Hugging Face, Apache 2.0, 2024, older and lower quality). Chatterbox, Dia/Dia2, Higgs Audio v2/v3, VibeVoice and Kani-TTS-2 have no documented description-to-voice feature. They rely on cloning from reference audio, preset speakers, random or "smart" voices, or tags.

### Cited Findings
**Breeze TTS 2 (BreezeBlue/Breeze-TTS-2): true voice design**
- It generates a voice from a free-text `--instruction` with no reference audio. Model card example: "A warm, thoughtful young woman with a clear voice and a calm, reflective delivery". Inline paralinguistic cues such as "(sigh)" can go in the text. — [HF model card](https://huggingface.co/BreezeBlue/Breeze-TTS-2)
- `--cfg-scale` controls how closely the voice follows the description: 4 is recommended to strengthen instruction-following, the default is 6, and the range is 0.5–10. Docs advise matching the instruction language to the target text. — [HF model card](https://huggingface.co/BreezeBlue/Breeze-TTS-2); [local-voice repo](https://github.com/vl3c/local-voice)
- It also supports voice cloning (reference audio plus exact transcript) and "voice direction" (keep a speaker's identity while changing tone, emotion and pace). In the local-voice project, a previous take's voice can be reused with a new description to change delivery while keeping the voice. — [search summary of model card](https://huggingface.co/BreezeBlue/Breeze-TTS-2); [local-voice](https://github.com/vl3c/local-voice)
- Granularity: descriptions in the local-voice project cover age, scale, timbre and delivery, and pace, pauses and emphasis are controlled from the description. Example: "Ancient male, massive scale". — [local-voice](https://github.com/vl3c/local-voice)
- Observed quirk: in that project, most seeds gave very deep voices (65–70 Hz) for deep-voice prompts, and some seeds landed near 95 Hz. Seed variance matters. — [local-voice](https://github.com/vl3c/local-voice)

**Maya1 (maya-research/maya1): true voice design**
- Prompt format: `<description="VOICE DESCRIPTION"> UTTERANCE TEXT`, wrapped in special tokens (SOH/BOS/EOT/EOH/SOA/SOS) in the Transformers example. — [HF model card](https://huggingface.co/maya-research/maya1)
- It has 20 inline emotion and vocal-style tags: angry, appalled, chuckle, cry, curious, disappointed, excited, exhale, gasp, giggle, gulp, laugh, laugh_harder, mischievous, sarcastic, scream, sigh, sing, snort, whisper. — [HF model card](https://huggingface.co/maya-research/maya1)
- The card notes that pronunciation and control can vary by language and accent, and that data curation had an "India-first" focus. — [HF model card](https://huggingface.co/maya-research/maya1)
- A 2026 roundup says Maya1's voice-design feature "is no longer unique" because other models now offer promptable voice design, so Apache 2.0 licensing is its main remaining advantage. — [search summary of 2026 roundups (cekura / scriptbyai / pinggy)](https://www.cekura.ai/blogs/open-source-tts)

**Parler-TTS (parler-tts/parler-tts-mini-v1, parler-tts-large-v1): true but coarse voice design**
- The text description controls gender, pitch, speaking rate, speaking style, reverberation and background noise. "very clear audio" gives the best quality and "very noisy audio" adds noise. Punctuation adds pauses. — [GitHub README](https://github.com/huggingface/parler-tts)
- For consistency, 34 named speakers (e.g. Jon, Laura, Gary) can be called by name in the description. This is a preset-like mode on top of free description. — [GitHub README](https://github.com/huggingface/parler-tts)
- Trained on 45k hours of audiobook data. It is still listed in 2026 comparisons as a description-prompted option but described as older (2024). — [GitHub README](https://github.com/huggingface/parler-tts); [2026 roundup summary](https://www.scriptbyai.com/best-open-source-tts-models/)

**Higgs Audio v2 / v2.5 / v3 (Boson AI): not true voice design**
- In the v2 README, the system prompt takes a `<|scene_desc_start|> ... <|scene_desc_end|>` block, but it is shown only for acoustic setting (e.g. "Audio is recorded from a quiet room."), not speaker traits. Without a reference, the model picks voices itself from the transcript ("Smart Voice"). Cloning uses reference audio. — [README_V2](https://github.com/boson-ai/higgs-audio/blob/main/README_V2.md)
- v2.5 claims "finer-grained style control" and improved cloning, condensed to 1B parameters with GRPO alignment. — [README_V2](https://github.com/boson-ai/higgs-audio/blob/main/README_V2.md)
- The Higgs Audio v3 TTS (4B/5B) model card documents only zero-shot synthesis and cloning, with no text-description voice design. — [HF bosonai/higgs-audio-v3-tts-4b](https://huggingface.co/bosonai/higgs-audio-v3-tts-4b)

**Chatterbox (Resemble AI): no voice design**
- It clones from a 5–20 s reference clip. The NVIDIA ACE model card says voices come from about 5 s of reference audio. Nothing indicates prompt-only voice creation. — [Resemble Chatterbox page](https://www.resemble.ai/chatterbox/?id=ChatterboxTTS); [NVIDIA model card](https://docs.nvidia.com/ace-for-games/chatterbox-tts/nvigi-chatterbox-model-card.html)

**Dia2 (Nari Labs): no voice design**
- It is a streaming dialogue model using [S1]/[S2] speaker tags. It has no fixed voice and "does not generate consistent voices unless guided by an audio prompt or fixed seed". A third-party listing gives Apache 2.0 (not verified with an official source). — [theresanaiforthat Dia2 listing](https://theresanaiforthat.com/model/dia-2/)

**VibeVoice (Microsoft): no voice design, partly withdrawn**
- TTS is driven by voice prompts and presets. Realtime-0.5B ships "11 distinct English style voices" plus experimental voices in 9 other languages. On 2025-09-05 Microsoft "removed the VibeVoice-TTS code from this repository" after misuse, and the TTS-1.5B demo is marked Disabled. 2026 updates are ASR-only (ASR-7B, ASR-Streaming, ASR-BitNet). The README says research and development use only. — [GitHub microsoft/VibeVoice](https://github.com/microsoft/VibeVoice)

**Kani-TTS-2 (nineninesix): no voice design**
- It uses speaker embeddings extracted from reference audio ("speaker embeddings for true voice control") and built-in voices. A maintainer acknowledged in an HF discussion that the cloning behavior is limited. No free-text voice design was found. — [PyPI kani-tts-2](https://pypi.org/project/kani-tts-2/); [HF discussion](https://huggingface.co/nineninesix/kani-tts-2-en/discussions/2)

**NeuTTS (Neuphonic) and Kyutai TTS**
- The search found nothing showing text-description voice design for NeuTTS-Air or Kyutai. — [search result summary](https://origin.bentoml.com/blog/exploring-the-world-of-open-source-text-to-speech-models)

**Other 2026 releases found**
- **VoxCPM2** (OpenBMB, a Chinese lab and out of scope) is described as generating "entirely new voices from free-form text descriptions without any reference audio". — [search summary (2026 roundups)](https://pinggy.io/blog/best_open_source_self_hosted_text_to_speech_models/)
- **VoiceSculptor** (arXiv 2601.10629) does instruction-based voice design using LLaSA-3B and then renders through CosyVoice2 cloning. It is a two-stage pipeline and reports SOTA on the Chinese InstructTTSEval-Zh. — [arXiv](https://arxiv.org/pdf/2601.10629)
- **OmniVoice** (k2-fsa, CC-BY-NC) does attribute-style design ("male, elderly, very low pitch"). It is coarser than free text. — [local-voice](https://github.com/vl3c/local-voice)
- **Mistral open-weight TTS** is positioned around short-clip voice cloning and accent preservation, not text-described design. — [MindStudio blog](https://www.mindstudio.ai/blog/mistral-open-weight-tts-voice-cloning-local)

### Inferences
- For true "describe a raspy middle-aged British man" design under open weights, the realistic local candidates in scope are Breeze TTS 2 (best quality, non-commercial), Maya1 (commercial OK) and Parler-TTS (commercial OK, weakest). The non-design models can still be used in a "design with A, clone with B" pipeline: design a voice with Breeze or Maya1, then clone it with Chatterbox for speed or licensing reasons. This mirrors VoiceSculptor's approach. Note that a voice produced by Breeze may itself carry the non-commercial output restriction (see Section 3).
- Kokoro (background knowledge, unverified this session) has only fixed preset voice packs and voice blending, with no text-description design.

### Gaps
- No fine-grained public evaluation of accent adherence (e.g. "British") was found for Breeze, Maya1 or Parler.
- I did not fetch the Orpheus, Sesame CSM, Kyutai TTS/Pocket-TTS, NeuTTS or Kokoro READMEs. Background knowledge, unverified: Orpheus has 8 preset English voices plus emotion tags, CSM-1B is a conversational model without description prompting, Kyutai TTS conditions on reference-audio embeddings, and NeuTTS-Air clones from about 3 s of reference audio. None offers free-text design.
- I found no "voice design" release from Resemble, Nari, Sesame or Kyutai dated 2026.

## 2. Quality reports: naturalness, prompt adherence, benchmarks and arenas

### Takeaway
On the Artificial Analysis TTS arena (fetched Nov 2026), Breeze TTS 2 is by a wide margin the top open-weight model (rank 15, Elo 1230), and Maya1 sits mid-pack (rank 73, Elo 1050). I found no blind test that compares prompt adherence across voice-design models.

### Cited Findings
- Artificial Analysis TTS leaderboard: Breeze TTS 2 rank 15 (Elo 1230), Kokoro 82M v1.0 rank 70 (1060), Maya1 rank 73 (1050), Higgs Audio V3 rank 81 (1035), Chatterbox rank 87 (1021), VibeVoice 1.5B rank 92 (954). Chatterbox HD (API, not open) is at 1112. Qwen3 TTS is at 932 (rank 96), although that may be the API variant. Dia, Orpheus, CSM, Kani, NeuTTS and VoxCPM were not listed. — [Artificial Analysis TTS leaderboard](https://artificialanalysis.ai/text-to-speech/leaderboard)
- The Breeze model card claims #1 among open-weight models on Artificial Analysis, "outperforming frontier proprietary systems". It links a voice-design benchmark suite released 2026-08-07, but shows no head-to-head numbers. — [HF model card](https://huggingface.co/BreezeBlue/Breeze-TTS-2)
- The local-voice author calls Breeze TTS 2 "the top open-weights voice-design TTS model as of autumn 2026". This is the author's opinion. — [local-voice](https://github.com/vl3c/local-voice)
- Higgs Audio v2 EmergentTTS-Eval win rates vs gpt-4o-mini-tts ("alloy"): Emotions 75.71%, Questions 55.71%. Higgs v3 has an EmergentTTS overall win rate of 53.65% and multilingual WER 1.11 on SeedTTS. — [README_V2](https://github.com/boson-ai/higgs-audio/blob/main/README_V2.md); [Higgs v3 card](https://huggingface.co/bosonai/higgs-audio-v3-tts-4b)
- A secondary 2026 blog headline claims Chatterbox reaches a "65.3%" preference over ElevenLabs. This is Resemble's own blind test as relayed by the blog, not independently verified. Community consensus summarized there: Kokoro for speed and low VRAM, Chatterbox-Turbo for cloning quality. — [findskill.ai](https://findskill.ai/blog/best-open-source-tts-2026/)
- TTS Arena V2 (TTS-AGI) leaderboard data did not load when fetched. — [HF Space](https://huggingface.co/spaces/TTS-AGI/TTS-Arena-V2)

### Inferences
- An Elo gap of about 180 between Breeze and Maya1 is large. Expect clearly better naturalness from Breeze. Parler-TTS is absent from current arenas and is likely well below both.

### Gaps
- No prompt-adherence metrics for Maya1 or Parler were found, and no independent voice-design benchmark (e.g. InstructTTSEval-En) results were retrieved for these models.

## 3. License, HF repo id, gating, download size, VRAM, latency/RTF

### Takeaway
Maya1 and Parler-TTS are Apache 2.0 and commercial-safe. Breeze TTS 2 weights and their self-hosted outputs are research and non-commercial only. Breeze needs about 8 GB VRAM (eager) and Maya1 recommends 16 GB or more.

### Cited Findings
**Breeze TTS 2: `BreezeBlue/Breeze-TTS-2`**
- License: the "BreezeBlue Research and Non-Commercial License" covers the weights, derivatives and self-hosted outputs. Code is Apache 2.0. Commercial use is only via BreezeBlue's paid hosted API. — [HF card](https://huggingface.co/BreezeBlue/Breeze-TTS-2)
- Size: 3B parameters, with weights of about 7.7 GB. — [HF card](https://huggingface.co/BreezeBlue/Breeze-TTS-2); [local-voice](https://github.com/vl3c/local-voice)
- VRAM: about 7.7 GiB eager and about 14.4 GiB with `--fast-all`. The card recommends a 12 GB GPU minimum for eager and 24 GB for the fast path. local-voice measured about 8.4 GB with fast paths. — [HF card](https://huggingface.co/BreezeBlue/Breeze-TTS-2); [local-voice](https://github.com/vl3c/local-voice)
- Latency: under 40 ms TTFA and RTF 0.32 on an H100 (warm fast path). On an RTX 5080 (Windows, warm) it runs about 2–2.5x faster than real time with fast paths, and about 2.5x slower than real time without them. Output is capped at about 120 s per take. — [HF card](https://huggingface.co/BreezeBlue/Breeze-TTS-2); [local-voice](https://github.com/vl3c/local-voice)
- Gating is not stated on the card.

**Maya1: `maya-research/maya1`**
- License Apache 2.0, with 3.30B parameters in BF16 (about 6.6 GB by inference from 3.3B × 2 bytes). It recommends 16 GB or more VRAM (RTX 4090/A100/H100 class). Streaming latency is reported as under 100 ms with vLLM. — [HF card](https://huggingface.co/maya-research/maya1)

**Parler-TTS: `parler-tts/parler-tts-mini-v1` (about 880M) and `parler-tts/parler-tts-large-v1` (about 2.3B)**
- License Apache-2.0. It supports SDPA, Flash Attention 2 and torch.compile, plus a streaming guide. — [GitHub](https://github.com/huggingface/parler-tts)

**Others (non-design), for reference**
- Higgs v2 needs 24 GB or more VRAM per README_V2. The v3 license is the "Boson Higgs TTS 3 Research and Non-Commercial License", with a Creator Use Grant that allows monetized content with credit. — [README_V2](https://github.com/boson-ai/higgs-audio/blob/main/README_V2.md); [v3 card](https://huggingface.co/bosonai/higgs-audio-v3-tts-4b)
- VibeVoice is MIT but labelled research-only, and TTS code was removed. — [GitHub](https://github.com/microsoft/VibeVoice)
- Kani-TTS-2 is about 400M parameters (LFM2 backbone plus NVIDIA NanoCodec) in about 3 GB VRAM, according to a third-party listing. — [kani search summary / tts.ai](https://tts.ai/voices/kani-tts-default/)

### Inferences
- On a 12 GB card, Breeze in eager mode runs at about real time or slower. A 16 GB or larger card is needed for its fast paths. Maya1 at BF16 should fit in about 8–10 GB for short clips despite the 16 GB recommendation, but this is untested.

### Gaps
- Parler mini and large VRAM and RTF figures were not retrieved. Background knowledge, unverified: mini runs on about 4 GB and large on about 8–10 GB.
- Exact Breeze download size including the codec, and gating status, were not confirmed.

## 4. Install: pip package, Python, torch/transformers versions, Windows support

### Takeaway
Maya1 and Parler install cleanly from pip and Git on any OS with standard transformers. Breeze officially lists Linux only, but a community project (local-voice) runs it on Windows 10/11 with one Inductor workaround.

### Cited Findings
- **Breeze:** `git clone https://github.com/breezeblue-ai/breeze-tts.git` then `pip install -r requirements.txt`. No pip package is published. It requires Linux, Python 3.10 or newer, and an NVIDIA CUDA GPU, with Docker available (`docker/build.sh`). — [HF card](https://huggingface.co/BreezeBlue/Breeze-TTS-2)
- **Breeze on Windows (community):** local-voice supports Windows 10/11, NVIDIA driver 570 or newer, CUDA 12.8 wheels (RTX 50-series) and Python 3.10+ (3.13 default). It installs with `install.ps1 -AcceptModelLicenses` (about 23 GB total). A depth-decoder fast path is disabled on Windows because of a 32-bit overflow in Inductor's static CUDA launcher. Breeze pins an older `transformers` than OmniVoice, so the project uses separate venvs. — [local-voice](https://github.com/vl3c/local-voice)
- **Maya1:** `pip install torch transformers accelerate snac soundfile numpy`. vLLM streaming is offered via a reference script, but vLLM is Linux-oriented (inference). — [HF card](https://huggingface.co/maya-research/maya1)
- **Parler-TTS:** `pip install git+https://github.com/huggingface/parler-tts.git`. — [GitHub](https://github.com/huggingface/parler-tts)
- Community ports of Breeze exist for ComfyUI (`drbaph/Breeze-TTS-2-comfyui`) and MLX. The ComfyUI docs mention a multilingual variant covering 51 languages and Voice Design nodes. — [search summary](https://docs.comfy.org/tutorials/partner-nodes/breeze/breeze-tts-2.md)

### Inferences
- The ComfyUI "Breeze" doc is under "partner-nodes", which likely means the hosted API, not local weights. The 51-language variant may be API-only.
- On Windows, Maya1 with plain transformers (no vLLM) is the lowest-friction commercial-safe option. Breeze requires replicating local-voice's workarounds or using WSL2.

### Gaps
- Exact torch and transformers pins for Breeze and Parler were not retrieved. Parler is known to pin an older transformers (background knowledge, unverified), which may conflict with other engines in the same venv.

## 5. Minimal Python inference API and output sample rate

### Takeaway
Parler has a clean class API. Maya1 uses a raw `AutoModelForCausalLM` plus SNAC decoding (about 40 lines of glue). Breeze is CLI and server-first, with no documented Python class.

### Cited Findings
- **Parler:** `from parler_tts import ParlerTTSForConditionalGeneration`, `from transformers import AutoTokenizer`. Tokenize the description into `input_ids` and the text into `prompt_input_ids`, call `model.generate(input_ids=..., prompt_input_ids=...)`, then `sf.write(path, audio, model.config.sampling_rate)`. The sample rate is read from the config. — [GitHub](https://github.com/huggingface/parler-tts)
- **Maya1:** `AutoModelForCausalLM` and `AutoTokenizer`. The helper functions in `maya1_example.py` are `build_prompt(tokenizer, description, text)`, `extract_snac_codes(token_ids)` and `unpack_snac_from_7(snac_tokens)`. Generate with `temperature=0.4, top_p=0.9, repetition_penalty=1.1, max_new_tokens=2048, eos_token_id=128258`, then decode with `snac` (`hubertsiuzdak/snac_24khz`, 7 tokens per frame, 3 code levels). Output is **24 kHz mono**. The CLI script is `maya1_inference.py --description --text --output`. — [HF card](https://huggingface.co/maya-research/maya1)
- **Breeze:** CLI `python infer.py <model_dir> --text ... --instruction ... --cfg-scale 4 --output out.wav`. The streaming server is `python -m breeze_infer.api`. Output is **24 kHz mono 16-bit PCM**. — [HF card](https://huggingface.co/BreezeBlue/Breeze-TTS-2)
- **Higgs v2 (reference):** `HiggsAudioServeEngine` and `HiggsAudioResponse` from `boson_multimodal.serve.serve_engine`, and `ChatMLSample`, `Message`, `AudioContent` from `boson_multimodal.data_types`. Call `engine.generate(chat_ml_sample=..., max_new_tokens, temperature, top_p, top_k, stop_strings)`. Higgs v3 is 24 kHz and served via `sgl-omni serve` or `vllm-omni serve`. — [README_V2](https://github.com/boson-ai/higgs-audio/blob/main/README_V2.md); [v3 card](https://huggingface.co/bosonai/higgs-audio-v3-tts-4b)

### Inferences
- To integrate Breeze into a Python app, either wrap `infer.py` or the `breeze_infer` module internals, or call its local streaming server.

### Gaps
- Parler's actual sample rate value was not confirmed. Background knowledge, unverified: 44.1 kHz via the DAC codec.

## 6. Languages supported

### Takeaway
All three true voice-design models in scope are effectively English-first. Breeze adds Chinese, and Higgs v3 (no design) covers 100+ languages.

### Cited Findings
- Breeze TTS 2 supports English and Chinese (local weights). — [HF card](https://huggingface.co/BreezeBlue/Breeze-TTS-2)
- Maya1's card lists no languages explicitly. It mentions "India-first" data curation and that accent control varies by language. 2026 roundups call it English. — [HF card](https://huggingface.co/maya-research/maya1); [roundup summary](https://www.cekura.ai/blogs/open-source-tts)
- Parler v1 checkpoints list no languages on the README and are trained on English audiobook data. Multilingual training appears only as a contribution idea. — [GitHub](https://github.com/huggingface/parler-tts)
- Higgs v3 supports 100+ languages, with single-digit WER/CER on 102 (85 below 5). — [v3 card](https://huggingface.co/bosonai/higgs-audio-v3-tts-4b)
- VibeVoice Realtime supports English plus 9 experimental languages. — [GitHub](https://github.com/microsoft/VibeVoice)

### Inferences
- Users who need non-English voice design from open weights will likely need to look at Chinese-lab models (Qwen3-TTS VoiceDesign, VoxCPM2), which another researcher covers.

### Gaps
- I could not confirm whether a separate "Parler-TTS Multilingual" checkpoint exists (background knowledge suggests `parler-tts-mini-multilingual-v1.1` covering about 8 European languages, but this is unverified).
- **Superseded or withdrawn flags:** Parler-TTS (2024) is superseded in quality. Higgs v2 has been superseded by v2.5 and then v3, which moved to a non-commercial license. VibeVoice TTS code was pulled. Dia (1.6B) has been superseded by Dia2. Maya1's uniqueness has been superseded by Breeze TTS 2 in quality.
