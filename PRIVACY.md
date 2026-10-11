# Yakkinator Privacy Policy

_Last updated: October 11, 2026_

This policy covers the Yakkinator Android app (`com.yakkinator.remote`) and the Yakkinator desktop app.

## The short version

Yakkinator does not collect, store or share your data with us or anyone else. The Android app talks only to the Yakkinator desktop app running on **your own computer**, at the address you enter or scan.

## What the Android app handles, and where it goes

| Data | Why | Where it goes |
|---|---|---|
| Text you type | To be turned into speech | Sent to your own PC, at the address you paired with |
| Voice samples you pick or record | To give the generated speech that voice | Sent to your own PC. If you choose "Save to the PC's voice library", it is stored there |
| Generated audio clips | So you can play and share them | Stored in the app's private storage on your phone until you delete them or uninstall the app |
| PC address and access token | To connect to your PC | Stored in the app's private storage on your phone |

None of this data is sent to the developer, to analytics or advertising services, or to any other third party. The app contains no analytics, advertising or tracking SDKs.

## Permissions

- **Microphone:** used only while you record a voice sample in the recorder dialog.
- **Camera:** used only while you scan the pairing QR code shown by the desktop app. Camera images are processed on the device and are not saved or sent anywhere.
- **Internet:** used to reach your own PC.

## Security

Traffic between the phone and your PC is protected by an access token that only you have. We recommend connecting over Tailscale or an HTTPS tunnel, both of which encrypt the connection. You can revoke a phone's access at any time by generating a new token in the desktop app (Settings → Remote Access).

## Sharing and reporting

If you share a clip, it goes to whichever app you pick in Android's share sheet. If you use **Report this clip**, a GitHub issue form opens with the clip's details pre-filled. Nothing is submitted until you review the form and post it yourself.

## Children

Yakkinator is not directed at children under 13.

## Changes

If this policy changes, the new version will be published at this address, with an updated date.

## Contact

Questions or concerns: open an issue at https://github.com/bongobongo2020/yakkinator/issues
