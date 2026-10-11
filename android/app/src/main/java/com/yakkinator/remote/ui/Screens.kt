package com.yakkinator.remote.ui

import android.Manifest
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AudioFile
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.ErrorOutline
import androidx.compose.material.icons.filled.ExpandLess
import androidx.compose.material.icons.filled.ExpandMore
import androidx.compose.material.icons.filled.Flag
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material.icons.filled.Mic
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Share
import androidx.compose.material.icons.filled.Stop
import androidx.compose.material.icons.filled.Visibility
import androidx.compose.material.icons.filled.VisibilityOff
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ExposedDropdownMenuAnchorType
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.SegmentedButton
import androidx.compose.material3.SegmentedButtonDefaults
import androidx.compose.material3.SingleChoiceSegmentedButtonRow
import androidx.compose.material3.Slider
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import androidx.core.content.FileProvider
import com.journeyapps.barcodescanner.ScanContract
import com.journeyapps.barcodescanner.ScanOptions
import com.yakkinator.remote.data.VoiceSource
import java.io.File
import java.text.DateFormat
import java.util.Date
import java.util.Locale
import kotlin.math.roundToInt

// ---------- shared bits ----------

@Composable
private fun SectionLabel(text: String) {
    Text(
        text.uppercase(),
        color = RetroCyan,
        fontFamily = FontFamily.Monospace,
        fontWeight = FontWeight.Bold,
        fontSize = 12.sp,
        letterSpacing = 1.5.sp,
        modifier = Modifier.padding(top = 14.dp, bottom = 6.dp),
    )
}

@Composable
private fun YakCard(modifier: Modifier = Modifier, content: @Composable ColumnScope.() -> Unit) {
    Card(
        modifier = modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = RetroCard),
        border = BorderStroke(1.5.dp, RetroBorder),
    ) {
        Column(Modifier.padding(12.dp), content = content)
    }
}

@Composable
private fun LabeledSlider(
    label: String,
    value: Float,
    onChange: (Float) -> Unit,
    range: ClosedFloatingPointRange<Float>,
    steps: Int = 0,
    format: (Float) -> String = { String.format(Locale.US, "%.2f", it) },
) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Text(label, Modifier.width(96.dp), fontSize = 13.sp)
        Slider(value, onChange, Modifier.weight(1f), valueRange = range, steps = steps)
        Text(format(value), Modifier.width(44.dp), fontSize = 13.sp, fontFamily = FontFamily.Monospace)
    }
}

private fun shareAudio(context: Context, file: File) {
    val uri = FileProvider.getUriForFile(context, "${context.packageName}.files", file)
    val send = Intent(Intent.ACTION_SEND).apply {
        type = "audio/wav"
        putExtra(Intent.EXTRA_STREAM, uri)
        addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
    }
    context.startActivity(Intent.createChooser(send, "Share audio"))
}

/** Google Play requires a way to flag offensive AI-generated content; reports go to the project's issue tracker. */
private fun reportClip(context: Context, item: com.yakkinator.remote.data.HistoryItem) {
    val body = "**What's wrong with this clip?**\n\n\n" +
        "---\nEngine: ${item.engine}\nVoice: ${item.voice}\nText: ${item.text.take(500)}"
    val uri = Uri.parse(Links.ISSUES).buildUpon()
        .appendQueryParameter("title", "Content report")
        .appendQueryParameter("labels", "content-report")
        .appendQueryParameter("body", body)
        .build()
    openLink(context, uri.toString())
}

private fun openLink(context: Context, url: String) {
    runCatching { context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url))) }
}

object Links {
    const val ISSUES = "https://github.com/bongobongo2020/yakkinator/issues/new"
    const val PRIVACY = "https://github.com/bongobongo2020/yakkinator/blob/main/PRIVACY.md"
}

@Composable
private fun ConsentNote() {
    Text(
        "Only use voices you have permission to use. Don't impersonate real people or create deceptive content.",
        fontSize = 12.sp,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = Modifier.padding(top = 6.dp),
    )
}

private fun formatSeconds(s: Int) = "%d:%02d".format(s / 60, s % 60)

// ---------- Connect ----------

@Composable
fun ConnectScreen(vm: YakViewModel, onConnected: () -> Unit) {
    val context = LocalContext.current
    var showToken by remember { mutableStateOf(false) }
    val scan = rememberLauncherForActivityResult(ScanContract()) { result ->
        val text = result.contents ?: return@rememberLauncherForActivityResult
        if (!vm.applyPairing(text)) vm.serverUrl = text
    }

    Column(
        Modifier
            .verticalScroll(rememberScrollState())
            .padding(16.dp)
    ) {
        Text("Connect to your PC", style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.Bold)
        Text(
            "On the PC: Yakkinator > Settings > Remote Access. Turn it on, then scan the QR code it shows.",
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 4.dp),
        )

        Button(
            onClick = {
                scan.launch(
                    ScanOptions()
                        .setDesiredBarcodeFormats(ScanOptions.QR_CODE)
                        .setPrompt("Scan the QR code in Yakkinator's Settings")
                        .setBeepEnabled(false)
                        .setOrientationLocked(false)
                )
            },
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 16.dp),
        ) {
            Icon(Icons.Default.QrCodeScanner, null)
            Spacer(Modifier.width(8.dp))
            Text("Scan pairing QR code")
        }

        SectionLabel("Or enter it manually")
        OutlinedTextField(
            value = vm.serverUrl,
            onValueChange = { vm.serverUrl = it },
            label = { Text("PC address") },
            placeholder = { Text("100.x.x.x or https://tts.example.com") },
            singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri),
            modifier = Modifier.fillMaxWidth(),
        )
        Spacer(Modifier.height(8.dp))
        OutlinedTextField(
            value = vm.token,
            onValueChange = { vm.token = it },
            label = { Text("Access token") },
            singleLine = true,
            visualTransformation = if (showToken) VisualTransformation.None else PasswordVisualTransformation(),
            trailingIcon = {
                IconButton(onClick = { showToken = !showToken }) {
                    Icon(if (showToken) Icons.Default.VisibilityOff else Icons.Default.Visibility, "Show token")
                }
            },
            modifier = Modifier.fillMaxWidth(),
        )
        OutlinedButton(
            onClick = vm::connect,
            enabled = vm.serverUrl.isNotBlank() && vm.token.isNotBlank() && vm.connection != ConnectionState.Checking,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 12.dp),
        ) { Text("Connect") }

        SectionLabel("Status")
        YakCard {
            when (val c = vm.connection) {
                ConnectionState.NotConfigured -> Text("Not set up yet.")
                ConnectionState.Checking -> Row(verticalAlignment = Alignment.CenterVertically) {
                    CircularProgressIndicator(Modifier.size(18.dp), strokeWidth = 2.dp)
                    Spacer(Modifier.width(10.dp))
                    Text("Contacting the PC…")
                }
                is ConnectionState.Failed -> {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Default.ErrorOutline, null, tint = MaterialTheme.colorScheme.error)
                        Spacer(Modifier.width(8.dp))
                        Text(c.message, color = MaterialTheme.colorScheme.error)
                    }
                    TextButton(onClick = vm::refresh) { Text("Retry") }
                }
                is ConnectionState.Connected -> {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Default.CheckCircle, null, tint = RetroCyan)
                        Spacer(Modifier.width(8.dp))
                        Text("Connected to Yakkinator ${c.info.version}", fontWeight = FontWeight.SemiBold)
                    }
                    Spacer(Modifier.height(6.dp))
                    c.info.engines.forEach { e ->
                        val state = when {
                            !e.installed -> "not installed"
                            e.ready -> "loaded"
                            else -> "loads on first use"
                        }
                        Text("• ${e.name}: $state", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    Text("• ${vm.voices.size} voices in the PC library", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    TextButton(onClick = onConnected) { Text("Start generating →") }
                }
            }
        }

        SectionLabel("Reaching your PC from anywhere")
        Text(
            "Install Tailscale on the PC and this phone and sign both into the same account; then use the PC's " +
                "Tailscale address (100.x.x.x). Traffic is encrypted end to end and no ports need opening. " +
                "On home Wi-Fi the PC's LAN address works too.",
            fontSize = 13.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Text(
            "Your text and voice samples go only to your own PC. Nothing is sent to us.",
            fontSize = 13.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 8.dp),
        )
        TextButton(onClick = { openLink(context, Links.PRIVACY) }) { Text("Privacy policy") }
    }
}

// ---------- Generate ----------

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun GenerateScreen(vm: YakViewModel, onOpenConnect: () -> Unit) {
    val context = LocalContext.current
    val pickVoice = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri ->
        uri?.let(vm::selectLocalVoice)
    }
    val pickEmotion = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri ->
        uri?.let(vm::selectEmotionAudio)
    }
    var showRecorder by remember { mutableStateOf(false) }
    val micPermission = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        if (granted) showRecorder = true
    }
    var advanced by rememberSaveable { mutableStateOf(false) }

    val connected = vm.connection as? ConnectionState.Connected

    Column(
        Modifier
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 16.dp, vertical = 8.dp)
    ) {
        // Connection banner
        if (connected == null) {
            YakCard(Modifier.clickable(onClick = onOpenConnect)) {
                Text(
                    when (val c = vm.connection) {
                        ConnectionState.Checking -> "Connecting to your PC…"
                        is ConnectionState.Failed -> c.message
                        else -> "Not connected. Tap to pair with your PC."
                    },
                    color = if (vm.connection is ConnectionState.Failed) MaterialTheme.colorScheme.error else Color.Unspecified,
                )
                if (vm.connection is ConnectionState.Failed) {
                    TextButton(onClick = vm::refresh) { Text("Retry") }
                }
            }
        }

        // Engine
        SectionLabel("Engine")
        val engines = connected?.info?.engines ?: listOf(
            com.yakkinator.remote.data.EngineInfo("indextts", "IndexTTS-2", true, false, true),
            com.yakkinator.remote.data.EngineInfo("echo", "Echo-TTS", true, false, false),
        )
        SingleChoiceSegmentedButtonRow(Modifier.fillMaxWidth()) {
            engines.forEachIndexed { i, e ->
                SegmentedButton(
                    selected = vm.engine == e.id,
                    onClick = { vm.selectEngine(e.id) },
                    enabled = e.installed && !vm.generating,
                    shape = SegmentedButtonDefaults.itemShape(i, engines.size),
                ) { Text(e.name) }
            }
        }
        if (connected != null && connected.info.engine != vm.engine) {
            Text(
                "The PC is using the other engine; it will switch (and load the model) when you generate.",
                fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 4.dp),
            )
        }

        // Voice
        SectionLabel("Voice")
        var voiceMenu by remember { mutableStateOf(false) }
        val voiceLabel = when (val v = vm.voice) {
            is VoiceSource.Library -> v.name
            is VoiceSource.Local -> "📱 ${v.audio.displayName}"
            null -> ""
        }
        ExposedDropdownMenuBox(expanded = voiceMenu, onExpandedChange = { voiceMenu = it && vm.voices.isNotEmpty() }) {
            OutlinedTextField(
                value = voiceLabel,
                onValueChange = {},
                readOnly = true,
                label = { Text("PC voice library") },
                placeholder = { Text(if (vm.voices.isEmpty()) "No saved voices on the PC" else "Choose a voice") },
                trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(voiceMenu) },
                singleLine = true,
                modifier = Modifier
                    .menuAnchor(ExposedDropdownMenuAnchorType.PrimaryNotEditable)
                    .fillMaxWidth(),
            )
            ExposedDropdownMenu(expanded = voiceMenu, onDismissRequest = { voiceMenu = false }) {
                vm.voices.forEach { v ->
                    DropdownMenuItem(
                        text = { Text(v.name) },
                        onClick = {
                            vm.selectLibraryVoice(v)
                            voiceMenu = false
                        },
                    )
                }
            }
        }
        Row(Modifier.padding(top = 8.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            OutlinedButton(onClick = { pickVoice.launch("audio/*") }, modifier = Modifier.weight(1f)) {
                Icon(Icons.Default.AudioFile, null)
                Spacer(Modifier.width(6.dp))
                Text("From phone")
            }
            OutlinedButton(
                onClick = {
                    if (ContextCompat.checkSelfPermission(context, Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED)
                        showRecorder = true
                    else micPermission.launch(Manifest.permission.RECORD_AUDIO)
                },
                modifier = Modifier.weight(1f),
            ) {
                Icon(Icons.Default.Mic, null)
                Spacer(Modifier.width(6.dp))
                Text("Record")
            }
        }
        ConsentNote()
        if (vm.voice is VoiceSource.Local) {
            Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.padding(top = 4.dp)) {
                Checkbox(checked = vm.saveVoiceToPc, onCheckedChange = { vm.saveVoiceToPc = it })
                Text("Save to the PC's voice library as", fontSize = 13.sp)
            }
            if (vm.saveVoiceToPc) {
                OutlinedTextField(
                    value = vm.saveVoiceName,
                    onValueChange = { vm.saveVoiceName = it },
                    singleLine = true,
                    label = { Text("Voice name") },
                    modifier = Modifier.fillMaxWidth(),
                )
            }
        }

        // Text
        SectionLabel("Text")
        OutlinedTextField(
            value = vm.text,
            onValueChange = vm::updateText,
            placeholder = { Text("What should the Yakkinator say?") },
            minLines = 4,
            maxLines = 10,
            modifier = Modifier.fillMaxWidth(),
            supportingText = { Text("${vm.text.length} characters") },
        )

        // Emotion (IndexTTS-2 only)
        if (vm.isIndexTts) {
            SectionLabel("Emotion")
            Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                listOf("none" to "None", "vector" to "Sliders", "text" to "From text", "audio" to "Audio").forEach { (id, label) ->
                    FilterChip(
                        selected = vm.emotionMode == id,
                        onClick = { vm.emotionMode = id },
                        label = { Text(label) },
                    )
                }
            }
            AnimatedVisibility(vm.emotionMode != "none") {
                YakCard(Modifier.padding(top = 6.dp)) {
                    when (vm.emotionMode) {
                        "vector" -> EmotionNames.forEachIndexed { i, name ->
                            LabeledSlider(name, vm.emotionVector[i], { vm.emotionVector[i] = it }, 0f..1f)
                        }
                        "text" -> {
                            Text("The emotion is inferred from the text itself.", fontSize = 13.sp)
                            LabeledSlider("Strength", vm.emotionAlpha, { vm.emotionAlpha = it }, 0f..1f)
                        }
                        "audio" -> {
                            OutlinedButton(onClick = { pickEmotion.launch("audio/*") }, modifier = Modifier.fillMaxWidth()) {
                                Text(vm.emotionAudio?.displayName ?: "Pick an emotion reference clip", maxLines = 1, overflow = TextOverflow.Ellipsis)
                            }
                            LabeledSlider("Strength", vm.emotionAlpha, { vm.emotionAlpha = it }, 0f..1f)
                        }
                    }
                }
            }
        } else {
            Text(
                "Echo-TTS takes its delivery from the voice sample; emotion controls are IndexTTS-2 only.",
                fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 10.dp),
            )
        }

        // Advanced
        Row(
            Modifier
                .fillMaxWidth()
                .clickable { advanced = !advanced }
                .padding(top = 8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            SectionLabel("Advanced")
            Icon(if (advanced) Icons.Default.ExpandLess else Icons.Default.ExpandMore, null, tint = RetroCyan)
        }
        AnimatedVisibility(advanced) {
            YakCard {
                val int: (Float) -> String = { it.roundToInt().toString() }
                if (vm.isIndexTts) {
                    LabeledSlider("Temperature", vm.temperature, { vm.temperature = it }, 0.1f..2f)
                    LabeledSlider("Top P", vm.topP, { vm.topP = it }, 0.1f..1f)
                    LabeledSlider("Top K", vm.topK.toFloat(), { vm.topK = it.roundToInt() }, 1f..100f, format = int)
                    LabeledSlider("Max tokens", vm.maxTokens.toFloat(), { vm.maxTokens = it.roundToInt() }, 20f..300f, format = int)
                } else {
                    Text("Changing these makes the PC reload Echo-TTS.", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    LabeledSlider("Steps", vm.echoSteps.toFloat(), { vm.echoSteps = it.roundToInt() }, 10f..80f, format = int)
                    LabeledSlider("CFG text", vm.echoCfgText, { vm.echoCfgText = it }, 1f..10f, format = { "%.1f".format(it) })
                    LabeledSlider("CFG speaker", vm.echoCfgSpeaker, { vm.echoCfgSpeaker = it }, 1f..15f, format = { "%.1f".format(it) })
                    OutlinedTextField(
                        value = vm.echoSeed.toString(),
                        onValueChange = { s -> s.toIntOrNull()?.let { vm.echoSeed = it } ?: run { if (s.isEmpty()) vm.echoSeed = 0 } },
                        label = { Text("Seed") },
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        modifier = Modifier.fillMaxWidth(),
                    )
                }
            }
        }

        // Generate
        Spacer(Modifier.height(16.dp))
        if (vm.generating) {
            YakCard {
                Text(
                    "Generating on your PC… ${formatSeconds(vm.elapsedSeconds)}",
                    fontFamily = FontFamily.Monospace,
                )
                LinearProgressIndicator(
                    Modifier
                        .fillMaxWidth()
                        .padding(vertical = 8.dp)
                )
                Text(
                    "Long texts (or loading a model) can take a few minutes. Keep the app open.",
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                TextButton(onClick = vm::cancelGenerate) { Text("Cancel") }
            }
        } else {
            Button(
                onClick = vm::generate,
                enabled = vm.canGenerate,
                modifier = Modifier
                    .fillMaxWidth()
                    .height(52.dp),
            ) { Text("YAK IT!", fontWeight = FontWeight.Bold, letterSpacing = 2.sp) }
        }

        vm.error?.let { err ->
            Card(
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.error.copy(alpha = 0.15f)),
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 10.dp),
            ) {
                Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
                    Text(err, Modifier.weight(1f), color = MaterialTheme.colorScheme.error)
                    TextButton(onClick = vm::dismissError) { Text("OK") }
                }
            }
        }

        vm.lastOutput?.let { item ->
            SectionLabel("Result")
            HistoryRow(vm, item, showDelete = false)
        }
        Spacer(Modifier.height(24.dp))
    }

    if (showRecorder) {
        RecorderDialog(vm, onDismiss = {
            vm.stopRecording()
            showRecorder = false
        })
    }
}

@Composable
private fun RecorderDialog(vm: YakViewModel, onDismiss: () -> Unit) {
    AlertDialog(
        onDismissRequest = { if (!vm.recording) onDismiss() },
        title = { Text("Record a voice sample") },
        text = {
            Column {
                Text(
                    "Speak naturally for 5–15 seconds in a quiet room. The voice you record is the voice the Yakkinator will use.",
                    fontSize = 13.sp,
                )
                ConsentNote()
                Spacer(Modifier.height(12.dp))
                Text(formatSeconds(vm.recordSeconds), fontFamily = FontFamily.Monospace, fontSize = 28.sp)
                LinearProgressIndicator(
                    progress = { vm.recordLevel },
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = 8.dp),
                )
            }
        },
        confirmButton = {
            if (vm.recording) {
                Button(onClick = onDismiss) {
                    Icon(Icons.Default.Stop, null)
                    Spacer(Modifier.width(6.dp))
                    Text("Stop & use")
                }
            } else {
                Button(onClick = vm::startRecording) {
                    Icon(Icons.Default.Mic, null)
                    Spacer(Modifier.width(6.dp))
                    Text("Start")
                }
            }
        },
        dismissButton = {
            if (!vm.recording) TextButton(onClick = onDismiss) { Text("Close") }
        },
    )
}

// ---------- History ----------

@Composable
fun HistoryScreen(vm: YakViewModel) {
    if (vm.items.isEmpty()) {
        Column(Modifier.padding(24.dp)) {
            Text("Nothing here yet.", style = MaterialTheme.typography.titleMedium)
            Text(
                "Clips you generate are kept on this phone.",
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        return
    }
    LazyColumn(
        contentPadding = androidx.compose.foundation.layout.PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        items(vm.items, key = { it.file.path }) { item -> HistoryRow(vm, item, showDelete = true) }
    }
}

@Composable
private fun HistoryRow(vm: YakViewModel, item: com.yakkinator.remote.data.HistoryItem, showDelete: Boolean) {
    val context = LocalContext.current
    val isPlaying = vm.playing == item.file
    YakCard {
        Row(verticalAlignment = Alignment.CenterVertically) {
            IconButton(onClick = { vm.togglePlay(item.file) }) {
                Icon(
                    if (isPlaying) Icons.Default.Stop else Icons.Default.PlayArrow,
                    if (isPlaying) "Stop" else "Play",
                    tint = RetroCyan,
                    modifier = Modifier.size(32.dp),
                )
            }
            Column(Modifier.weight(1f)) {
                Text(item.text.ifBlank { item.file.name }, maxLines = 2, overflow = TextOverflow.Ellipsis)
                Text(
                    listOf(
                        item.voice,
                        if (item.engine == "echo") "Echo-TTS" else "IndexTTS-2",
                        DateFormat.getDateTimeInstance(DateFormat.SHORT, DateFormat.SHORT).format(Date(item.created)),
                    ).filter { it.isNotBlank() }.joinToString(" · "),
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
            }
            IconButton(onClick = { shareAudio(context, item.file) }) { Icon(Icons.Default.Share, "Share") }
            Box {
                var menu by remember { mutableStateOf(false) }
                IconButton(onClick = { menu = true }) { Icon(Icons.Default.MoreVert, "More") }
                DropdownMenu(expanded = menu, onDismissRequest = { menu = false }) {
                    DropdownMenuItem(
                        text = { Text("Report this clip") },
                        leadingIcon = { Icon(Icons.Default.Flag, null) },
                        onClick = {
                            menu = false
                            reportClip(context, item)
                        },
                    )
                    if (showDelete) {
                        DropdownMenuItem(
                            text = { Text("Delete") },
                            leadingIcon = { Icon(Icons.Default.Delete, null) },
                            onClick = {
                                menu = false
                                vm.delete(item)
                            },
                        )
                    }
                }
            }
        }
    }
}

@Composable
fun RefreshButton(vm: YakViewModel) {
    IconButton(onClick = vm::refresh, enabled = vm.connection != ConnectionState.Checking) {
        Icon(Icons.Default.Refresh, "Refresh")
    }
}
