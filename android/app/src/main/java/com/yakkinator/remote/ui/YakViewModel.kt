package com.yakkinator.remote.ui

import android.app.Application
import android.media.MediaPlayer
import android.net.Uri
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.yakkinator.remote.data.ApiException
import com.yakkinator.remote.data.GenerateParams
import com.yakkinator.remote.data.HistoryItem
import com.yakkinator.remote.data.HistoryStore
import com.yakkinator.remote.data.LocalAudio
import com.yakkinator.remote.data.Prefs
import com.yakkinator.remote.data.ServerInfo
import com.yakkinator.remote.data.Voice
import com.yakkinator.remote.data.VoiceSource
import com.yakkinator.remote.data.WavRecorder
import com.yakkinator.remote.data.YakApi
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import java.io.File

sealed interface ConnectionState {
    data object NotConfigured : ConnectionState
    data object Checking : ConnectionState
    data class Connected(val info: ServerInfo) : ConnectionState
    data class Failed(val message: String) : ConnectionState
}

val EmotionNames = listOf("Joy", "Anger", "Sadness", "Fear", "Disgust", "Melancholy", "Surprise", "Calm")

class YakViewModel(app: Application) : AndroidViewModel(app) {
    private val prefs = Prefs(app)
    private val history = HistoryStore(app.filesDir)
    private val resolver = app.contentResolver

    // Connection
    var serverUrl by mutableStateOf(prefs.serverUrl)
    var token by mutableStateOf(prefs.token)
    var connection by mutableStateOf<ConnectionState>(
        if (prefs.serverUrl.isBlank() || prefs.token.isBlank()) ConnectionState.NotConfigured else ConnectionState.Checking
    )
        private set
    var voices by mutableStateOf<List<Voice>>(emptyList())
        private set

    // Generate form
    var engine by mutableStateOf(prefs.engine)
        private set
    var voice by mutableStateOf<VoiceSource?>(null)
        private set
    var saveVoiceToPc by mutableStateOf(true)
    var saveVoiceName by mutableStateOf("")
    var text by mutableStateOf(prefs.text)
        private set
    var emotionMode by mutableStateOf("none")
    var emotionAlpha by mutableFloatStateOf(0.6f)
    val emotionVector = mutableStateListOf(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f)
    var emotionAudio by mutableStateOf<LocalAudio?>(null)

    var temperature by mutableFloatStateOf(1.0f)
    var topP by mutableFloatStateOf(0.8f)
    var topK by mutableIntStateOf(30)
    var maxTokens by mutableIntStateOf(120)
    var echoSteps by mutableIntStateOf(40)
    var echoCfgText by mutableFloatStateOf(3.0f)
    var echoCfgSpeaker by mutableFloatStateOf(8.0f)
    var echoSeed by mutableIntStateOf(0)

    // Generation state
    var generating by mutableStateOf(false)
        private set
    var elapsedSeconds by mutableIntStateOf(0)
        private set
    var error by mutableStateOf<String?>(null)
        private set
    var lastOutput by mutableStateOf<HistoryItem?>(null)
        private set
    private var generateJob: Job? = null

    // History & playback
    var items by mutableStateOf(history.list())
        private set
    var playing by mutableStateOf<File?>(null)
        private set
    private var player: MediaPlayer? = null

    // Recording
    var recording by mutableStateOf(false)
        private set
    var recordLevel by mutableFloatStateOf(0f)
        private set
    var recordSeconds by mutableIntStateOf(0)
        private set
    private var recordJob: Job? = null

    init {
        if (connection == ConnectionState.Checking) refresh()
    }

    private fun api() = YakApi(serverUrl, token)

    /** Saves the connection settings and checks them against the PC. */
    fun connect() {
        serverUrl = normalizeUrl(serverUrl)
        prefs.serverUrl = serverUrl
        prefs.token = token.trim()
        token = token.trim()
        refresh()
    }

    /** Applies a scanned/opened pairing link: yakkinator://pair?url=...&token=... */
    fun applyPairing(link: String): Boolean {
        val uri = runCatching { Uri.parse(link.trim()) }.getOrNull() ?: return false
        if (uri.scheme != "yakkinator" || uri.host != "pair") return false
        val url = uri.getQueryParameter("url") ?: return false
        val tok = uri.getQueryParameter("token") ?: return false
        serverUrl = url
        token = tok
        connect()
        return true
    }

    fun refresh() {
        if (serverUrl.isBlank() || token.isBlank()) {
            connection = ConnectionState.NotConfigured
            return
        }
        connection = ConnectionState.Checking
        viewModelScope.launch {
            connection = try {
                val api = api()
                val info = api.info()
                voices = api.voices()
                if (voice == null) {
                    voices.firstOrNull { it.name == prefs.lastVoice }?.let { selectLibraryVoice(it) }
                }
                // Fall back to an engine the PC actually has
                if (info.engines.none { it.id == engine && it.installed }) {
                    info.engines.firstOrNull { it.installed }?.let { selectEngine(it.id) }
                }
                ConnectionState.Connected(info)
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                ConnectionState.Failed(e.message ?: "Connection failed")
            }
        }
    }

    fun selectEngine(id: String) {
        engine = id
        prefs.engine = id
    }

    fun updateText(value: String) {
        text = value
        prefs.text = value
    }

    fun selectLibraryVoice(v: Voice) {
        voice = VoiceSource.Library(v.name)
        prefs.lastVoice = v.name
        // Like the desktop app, a library voice brings its saved emotion settings
        emotionMode = if (v.emotionMode == "audio") "none" else v.emotionMode
        emotionAlpha = v.emotionAlpha
        v.emotionVector.forEachIndexed { i, f -> if (i < 8) emotionVector[i] = f }
    }

    fun selectLocalVoice(uri: Uri) {
        val name = YakApi.displayName(resolver, uri)
        voice = VoiceSource.Local(LocalAudio(uri, name))
        saveVoiceName = name.substringBeforeLast('.')
    }

    fun selectEmotionAudio(uri: Uri) {
        emotionAudio = LocalAudio(uri, YakApi.displayName(resolver, uri))
    }

    fun startRecording() {
        if (recording) return
        val file = File(getApplication<Application>().cacheDir, "recording_${System.currentTimeMillis()}.wav")
        recording = true
        recordSeconds = 0
        val started = System.currentTimeMillis()
        recordJob = viewModelScope.launch {
            val ticker = launch {
                while (true) {
                    delay(250)
                    recordSeconds = ((System.currentTimeMillis() - started) / 1000).toInt()
                }
            }
            try {
                WavRecorder().record(file) { level -> recordLevel = level }
            } catch (e: CancellationException) {
                // stopped by the user: keep the take
            } catch (e: Exception) {
                error = e.message
                file.delete()
            }
            ticker.cancel()
        }
        recordJob?.invokeOnCompletion {
            recording = false
            recordLevel = 0f
            if (file.length() > 44) {
                voice = VoiceSource.Local(LocalAudio(Uri.fromFile(file), "Recording ${recordSeconds}s.wav"))
                saveVoiceName = "Phone recording"
            }
        }
    }

    fun stopRecording() {
        recordJob?.cancel()
    }

    val canGenerate: Boolean
        get() = !generating && connection is ConnectionState.Connected && text.isNotBlank() && voice != null &&
            !(isIndexTts && emotionMode == "audio" && emotionAudio == null)

    val isIndexTts get() = engine == "indextts"

    fun generate() {
        val v = voice ?: return
        error = null
        generating = true
        elapsedSeconds = 0
        stopPlayback()
        generateJob = viewModelScope.launch {
            val started = System.currentTimeMillis()
            val ticker = launch {
                while (true) {
                    delay(1000)
                    elapsedSeconds = ((System.currentTimeMillis() - started) / 1000).toInt()
                }
            }
            val mode = if (isIndexTts) emotionMode else "none"
            val dest = history.newFile("")
            try {
                api().generate(
                    GenerateParams(
                        engine = engine,
                        text = text.trim(),
                        voice = v,
                        emotionMode = mode,
                        emotionAlpha = emotionAlpha,
                        emotionVector = emotionVector.toList(),
                        emotionAudio = if (mode == "audio") emotionAudio else null,
                        temperature = temperature,
                        topP = topP,
                        topK = topK,
                        maxTokens = maxTokens,
                        echoSteps = echoSteps,
                        echoCfgText = echoCfgText,
                        echoCfgSpeaker = echoCfgSpeaker,
                        echoSeed = echoSeed,
                        saveVoiceAs = if (v is VoiceSource.Local && saveVoiceToPc) saveVoiceName.ifBlank { "Phone voice" } else null,
                    ),
                    resolver, dest,
                )
                val voiceLabel = when (v) {
                    is VoiceSource.Library -> v.name
                    is VoiceSource.Local -> v.audio.displayName
                }
                val item = history.record(dest, text.trim(), voiceLabel, engine)
                lastOutput = item
                items = history.list()
                play(item.file)
                // An uploaded voice may now be in the PC's library
                if (v is VoiceSource.Local && saveVoiceToPc) {
                    runCatching { voices = api().voices() }
                }
            } catch (e: CancellationException) {
                dest.delete()
                throw e
            } catch (e: Exception) {
                dest.delete()
                error = if (e is ApiException && e.code == 0 && connection is ConnectionState.Connected)
                    "Lost the connection to the PC: ${e.message}" else e.message ?: "Generation failed"
            } finally {
                ticker.cancel()
                generating = false
            }
        }
    }

    fun cancelGenerate() {
        generateJob?.cancel()
    }

    fun dismissError() {
        error = null
    }

    fun togglePlay(file: File) {
        if (playing == file) stopPlayback() else play(file)
    }

    private fun play(file: File) {
        stopPlayback()
        runCatching {
            player = MediaPlayer().apply {
                setDataSource(file.path)
                setOnCompletionListener { stopPlayback() }
                prepare()
                start()
            }
            playing = file
        }.onFailure { error = "Playback failed: ${it.message}" }
    }

    fun stopPlayback() {
        player?.runCatching { stop(); release() }
        player = null
        playing = null
    }

    fun delete(item: HistoryItem) {
        if (playing == item.file) stopPlayback()
        history.delete(item)
        if (lastOutput?.file == item.file) lastOutput = null
        items = history.list()
    }

    override fun onCleared() {
        stopPlayback()
    }

    private fun normalizeUrl(raw: String): String {
        var url = raw.trim().trimEnd('/')
        if (url.isEmpty()) return url
        if (!url.contains("://")) url = "http://$url"
        // A bare host/IP means the desktop app's default remote port
        val afterScheme = url.substringAfter("://")
        if (url.startsWith("http://") && !afterScheme.substringBefore('/').contains(':')) {
            url = url.replaceFirst(afterScheme.substringBefore('/'), afterScheme.substringBefore('/') + ":5301")
        }
        return url
    }
}
