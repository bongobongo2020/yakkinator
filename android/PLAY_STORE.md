# Publishing Yakkinator on Google Play

Everything that can be prepared in the repo is done. This is the checklist for the parts that happen in [Play Console](https://play.google.com/console).

## 0. Before you start

- **Release the desktop app first.** The phone app only works with a desktop build that has Settings → Remote Access, so publish that build (e.g. v1.2.0) on GitHub before the Play listing goes live.
- **Back up the upload key.** Keep `android/upload-keystore.jks` and `android/keystore.properties` (it holds the password) somewhere safe, such as a password manager. Both are git-ignored. With Play App Signing, a lost upload key can be reset through Google support, but that takes days.

## 1. Build the bundle

```
cd android
gradlew bundleRelease
```

Upload `android/app/build/outputs/bundle/release/app-release.aab`. For every later upload, bump `versionCode` (and `versionName`) in `app/build.gradle.kts`.

| | |
|---|---|
| Package name | `com.yakkinator.remote` (permanent once uploaded) |
| Target SDK | 36 (Android 16), as Play requires for new apps from Aug 31, 2026 |
| Min SDK | 26 (Android 8.0) |
| 16 KB page sizes | Supported (native libs verified 16 KB-aligned) |
| Signing | Upload key `upload` in `upload-keystore.jks`; enrol in Play App Signing (the default) |

## 2. Store listing

Assets are in `android/play-store/`:
- `icon-512.png`: app icon (512×512)
- `feature-graphic.png`: feature graphic (1024×500)
- `screenshots/`: 4 phone screenshots (1080×1920)

**App name:** The Yakkinator

**Short description (max 80 characters):**
> Remote control for The Yakkinator text-to-speech studio on your Windows PC.

**Full description:**
> The Yakkinator for Android is a companion to The Yakkinator, a free text-to-speech studio for Windows powered by IndexTTS-2 and Echo-TTS. Type on your phone, and your PC's GPU does the talking.
>
> ⚠️ REQUIRES THE YAKKINATOR DESKTOP APP running on a Windows PC with an NVIDIA GPU. Get it free at https://github.com/bongobongo2020/yakkinator. This app does not generate speech by itself.
>
> WHAT YOU CAN DO
> • Pair with your PC in seconds by scanning a QR code
> • Use any voice from your PC's voice library, or use a sample from your phone
> • Record a new voice sample with your phone's microphone
> • Choose the engine: IndexTTS-2 or Echo-TTS
> • Shape delivery with emotion sliders, emotion-from-text or an emotion reference clip
> • Fine-tune temperature, top-p, top-k and Echo-TTS sampler settings
> • Play, share and manage every clip you generate
>
> WORKS FROM ANYWHERE
> Connect over your home Wi-Fi, or from anywhere with Tailscale (free), which encrypts traffic between your phone and PC with no port forwarding.
>
> PRIVATE BY DESIGN
> Your text and voice samples go only to your own computer. No accounts, no cloud, no analytics, no ads.
>
> Please use voices responsibly: only clone voices you have permission to use, and never use generated speech to deceive or impersonate.

**Category:** Music & Audio (or Productivity)
**Contact email:** required by Play; use the address you want to receive users' email at
**Website:** https://github.com/bongobongo2020/yakkinator
**Privacy policy:** https://github.com/bongobongo2020/yakkinator/blob/main/PRIVACY.md. Push `PRIVACY.md` to `main` first so this URL resolves.

## 3. App content (Policy → App content)

**Privacy policy:** URL above.

**Ads:** No ads.

**App access:** choose *"All or some functionality is restricted"* and add instructions along these lines:
> This is a companion app for The Yakkinator desktop app (free, open source: https://github.com/bongobongo2020/yakkinator). Generating speech requires the desktop app running on a Windows PC with an NVIDIA GPU, which the app connects to over the user's own network or Tailscale. No login or account exists. Without a paired PC, the Connect, History and Generate screens can be explored, but generation reports that the PC can't be reached.

Add a short screen recording of a full pairing and generation (unlisted YouTube link) to the instructions. Reviewers can't run your PC, and a video is the most reliable way to avoid a "broken functionality" rejection.

**Content rating:** fill in the IARC questionnaire. Category: Utility/Productivity. Answer *yes* to "user-generated content / users can create content" if asked (users generate audio). There's no violence, no sexual content, no gambling and no in-app purchases.

**Target audience:** 18+ (or 13+). Don't include children; voice cloning features aren't appropriate for a children's audience.

**Data safety:** the app sends user data only to a server the *user* controls (their own PC). Google treats data that leaves the device as "collected" unless it's processed only on-device or is end-to-end encrypted to a destination the developer can't access. The conservative answers:

| Question | Answer |
|---|---|
| Does your app collect or share any of the required user data types? | **Yes** |
| Is all of the user data encrypted in transit? | **No.** Plain HTTP is allowed for LAN connections; Tailscale or an HTTPS tunnel encrypts it, but the app can't guarantee that |
| Do you provide a way for users to request that their data is deleted? | **Yes.** All data lives on the user's own phone and PC; clips are deleted in-app, and the voice library is managed in the desktop app |
| Data types collected | **Audio → Voice or sound recordings** (voice samples), **App activity → Other user-generated content** (the text to speak). For each, mark: *Collected* yes, *Shared* no, *Processed ephemerally* no, *Required* yes, purpose **App functionality** |
| Data shared with third parties | None |

**AI-generated content:** the app sends prompts to a generative voice model (on the user's PC). Play's AI-Generated Content policy requires a way for users to report offensive output. That's the **⋮ → Report this clip** action on every clip; it opens a pre-filled issue on the GitHub tracker. Keep an eye on issues labelled `content-report` (create the label on GitHub so the pre-fill applies it).

**Government apps, financial features, health, news:** No.

## 4. Testing track (new personal developer accounts)

Personal Play developer accounts created after Nov 13, 2023 must run a **closed test with at least 12 testers who stay opted in for 14 days in a row** before they can apply for production access. Create a closed testing track, upload the `.aab` there, add testers by email (or a Google Group), and share the opt-in link. Organisation accounts can skip this.

## 5. Release

Internal testing → closed testing → production. Roll out to production gradually (e.g. 20%) and watch Android vitals for crashes.
