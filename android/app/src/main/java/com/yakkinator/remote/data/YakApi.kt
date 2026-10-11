package com.yakkinator.remote.data

import android.content.ContentResolver
import android.net.Uri
import android.provider.OpenableColumns
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withContext
import okhttp3.Call
import okhttp3.Callback
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MultipartBody
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.Response
import org.json.JSONObject
import java.io.File
import java.io.IOException
import java.util.Locale
import java.util.concurrent.TimeUnit
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException

class ApiException(message: String, val code: Int = 0) : Exception(message)

data class EngineInfo(
    val id: String,
    val name: String,
    val installed: Boolean,
    val ready: Boolean,
    val supportsEmotion: Boolean,
)

data class ServerInfo(
    val version: String,
    val engine: String,
    val busy: Boolean,
    val engines: List<EngineInfo>,
)

data class Voice(
    val name: String,
    val size: Long,
    val emotionMode: String,
    val emotionAlpha: Float,
    val emotionVector: List<Float>,
)

/** A voice sample on the phone (picked file or recording). */
data class LocalAudio(val uri: Uri, val displayName: String)

sealed interface VoiceSource {
    data class Library(val name: String) : VoiceSource
    data class Local(val audio: LocalAudio) : VoiceSource
}

data class GenerateParams(
    val engine: String,
    val text: String,
    val voice: VoiceSource,
    val emotionMode: String,
    val emotionAlpha: Float,
    val emotionVector: List<Float>,
    val emotionAudio: LocalAudio?,
    val temperature: Float,
    val topP: Float,
    val topK: Int,
    val maxTokens: Int,
    val echoSteps: Int,
    val echoCfgText: Float,
    val echoCfgSpeaker: Float,
    val echoSeed: Int,
    /** Also keep an uploaded sample in the PC's voice library under this name. */
    val saveVoiceAs: String?,
)

/** Client for the desktop app's remote API (Settings > Remote Access on the PC). */
class YakApi(baseUrl: String, private val token: String) {
    private val base = baseUrl.trim().trimEnd('/').toHttpUrlOrNull()
        ?: throw ApiException("\"$baseUrl\" isn't a valid address")

    private val quick = shared.newBuilder()
        .callTimeout(20, TimeUnit.SECONDS)
        .build()

    // Generation returns only when the audio is done, which can take many minutes
    private val slow = shared.newBuilder()
        .readTimeout(35, TimeUnit.MINUTES)
        .writeTimeout(5, TimeUnit.MINUTES)
        .callTimeout(40, TimeUnit.MINUTES)
        .build()

    private fun url(path: String) = base.newBuilder()
        .addPathSegments("remote/v1/$path")
        .build()

    private fun request(path: String) = Request.Builder()
        .url(url(path))
        .header("Authorization", "Bearer $token")

    suspend fun info(): ServerInfo {
        val json = JSONObject(fetchText(quick, request("info").build()))
        val engines = json.getJSONArray("engines")
        return ServerInfo(
            version = json.optString("version"),
            engine = json.optString("engine"),
            busy = json.optBoolean("busy"),
            engines = (0 until engines.length()).map { i ->
                val e = engines.getJSONObject(i)
                EngineInfo(
                    id = e.getString("id"),
                    name = e.getString("name"),
                    installed = e.optBoolean("installed"),
                    ready = e.optBoolean("ready"),
                    supportsEmotion = e.optBoolean("supportsEmotion"),
                )
            },
        )
    }

    suspend fun voices(): List<Voice> {
        val arr = JSONObject(fetchText(quick, request("voices").build())).getJSONArray("voices")
        return (0 until arr.length()).map { i ->
            val v = arr.getJSONObject(i)
            val vec = v.optJSONArray("emotionVector")
            Voice(
                name = v.getString("name"),
                size = v.optLong("size"),
                emotionMode = v.optString("emotionMode", "none"),
                emotionAlpha = v.optDouble("emotionAlpha", 0.6).toFloat(),
                emotionVector = (0 until 8).map { idx -> vec?.optDouble(idx, 0.0)?.toFloat() ?: 0f },
            )
        }
    }

    /** Generates speech and writes the WAV to [dest]. Returns the PC's job id. */
    suspend fun generate(params: GenerateParams, resolver: ContentResolver, dest: File): String {
        val form = MultipartBody.Builder().setType(MultipartBody.FORM)
            .addFormDataPart("engine", params.engine)
            .addFormDataPart("text", params.text)
            .addFormDataPart("emotion_mode", params.emotionMode)
            .addFormDataPart("emotion_alpha", params.emotionAlpha.fmt())
            .addFormDataPart("emotion_vector", params.emotionVector.joinToString(",") { it.fmt() })
            .addFormDataPart("temperature", params.temperature.fmt())
            .addFormDataPart("top_p", params.topP.fmt())
            .addFormDataPart("top_k", params.topK.toString())
            .addFormDataPart("max_tokens", params.maxTokens.toString())
            .addFormDataPart("echo_steps", params.echoSteps.toString())
            .addFormDataPart("echo_cfg_text", params.echoCfgText.fmt())
            .addFormDataPart("echo_cfg_speaker", params.echoCfgSpeaker.fmt())
            .addFormDataPart("echo_seed", params.echoSeed.toString())

        when (val voice = params.voice) {
            is VoiceSource.Library -> form.addFormDataPart("voice_name", voice.name)
            is VoiceSource.Local -> {
                addAudio(form, "voice", voice.audio, resolver)
                params.saveVoiceAs?.takeIf { it.isNotBlank() }?.let { form.addFormDataPart("save_voice_as", it) }
            }
        }
        if (params.emotionMode == "audio") {
            val emo = params.emotionAudio ?: throw ApiException("Pick an emotion reference sample")
            addAudio(form, "emotion_audio", emo, resolver)
        }

        val response = execute(slow, request("generate").post(form.build()).build())
        return response.use { resp ->
            if (!resp.isSuccessful) throw errorFrom(resp)
            withContext(Dispatchers.IO) {
                dest.parentFile?.mkdirs()
                val tmp = File(dest.path + ".part")
                resp.body!!.byteStream().use { input -> tmp.outputStream().use { input.copyTo(it) } }
                if (!tmp.renameTo(dest)) throw IOException("Couldn't save the audio")
            }
            resp.header("X-Job-Id") ?: ""
        }
    }

    private suspend fun addAudio(form: MultipartBody.Builder, field: String, audio: LocalAudio, resolver: ContentResolver) {
        val bytes = withContext(Dispatchers.IO) {
            resolver.openInputStream(audio.uri)?.use { it.readBytes() }
        } ?: throw ApiException("Couldn't read ${audio.displayName}")
        val type = resolver.getType(audio.uri) ?: "application/octet-stream"
        form.addFormDataPart(field, audio.displayName, bytes.toRequestBody(type.toMediaType()))
    }

    private suspend fun fetchText(client: OkHttpClient, req: Request): String =
        execute(client, req).use { resp ->
            if (!resp.isSuccessful) throw errorFrom(resp)
            withContext(Dispatchers.IO) { resp.body!!.string() }
        }

    private suspend fun errorFrom(resp: Response): ApiException {
        val body = withContext(Dispatchers.IO) { runCatching { resp.body?.string() }.getOrNull() }
        val message = runCatching { JSONObject(body!!).getString("error") }.getOrNull()
            ?: runCatching { JSONObject(body!!).getString("detail") }.getOrNull()
            ?: when (resp.code) {
                401 -> "The PC rejected the access token. Pair again from Settings > Remote Access."
                404 -> "No Yakkinator remote API at this address."
                else -> body?.take(300)?.ifBlank { null } ?: "HTTP ${resp.code}"
            }
        return ApiException(message, resp.code)
    }

    /** Runs a call, cancelling it if the coroutine is cancelled. */
    private suspend fun execute(client: OkHttpClient, req: Request): Response =
        suspendCancellableCoroutine { cont ->
            val call = client.newCall(req)
            cont.invokeOnCancellation { call.cancel() }
            call.enqueue(object : Callback {
                override fun onFailure(call: Call, e: IOException) {
                    cont.resumeWithException(ApiException(friendly(e)))
                }

                override fun onResponse(call: Call, response: Response) {
                    cont.resume(response)
                }
            })
        }

    private fun friendly(e: IOException): String = when (e) {
        is java.net.UnknownHostException -> "Can't find ${base.host}. Check the address."
        is java.net.ConnectException -> "Can't reach the PC at ${base.host}:${base.port}. Is Yakkinator running with Remote Access on (and Tailscale connected)?"
        is java.net.SocketTimeoutException -> "The PC took too long to answer."
        else -> e.message ?: "Network error"
    }

    companion object {
        private val shared = OkHttpClient.Builder()
            .connectTimeout(15, TimeUnit.SECONDS)
            .build()

        private fun Float.fmt() = String.format(Locale.US, "%.4f", this)

        fun displayName(resolver: ContentResolver, uri: Uri): String =
            resolver.query(uri, arrayOf(OpenableColumns.DISPLAY_NAME), null, null, null)?.use { c ->
                if (c.moveToFirst()) c.getString(0) else null
            } ?: uri.lastPathSegment ?: "audio"
    }
}
