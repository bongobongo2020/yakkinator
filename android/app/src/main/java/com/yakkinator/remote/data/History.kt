package com.yakkinator.remote.data

import org.json.JSONObject
import java.io.File

data class HistoryItem(
    val file: File,
    val text: String,
    val voice: String,
    val engine: String,
    val created: Long,
)

/** Generated clips live in filesDir/outputs as WAV + a JSON sidecar describing them. */
class HistoryStore(filesDir: File) {
    val dir = File(filesDir, "outputs").apply { mkdirs() }

    fun newFile(jobId: String): File =
        File(dir, "yak_${System.currentTimeMillis()}${if (jobId.isNotBlank()) "_$jobId" else ""}.wav")

    fun record(file: File, text: String, voice: String, engine: String): HistoryItem {
        val item = HistoryItem(file, text, voice, engine, System.currentTimeMillis())
        sidecar(file).writeText(
            JSONObject()
                .put("text", text)
                .put("voice", voice)
                .put("engine", engine)
                .put("created", item.created)
                .toString()
        )
        return item
    }

    fun list(): List<HistoryItem> =
        (dir.listFiles { f -> f.extension == "wav" } ?: emptyArray())
            .map { f ->
                val meta = runCatching { JSONObject(sidecar(f).readText()) }.getOrNull()
                HistoryItem(
                    file = f,
                    text = meta?.optString("text") ?: "",
                    voice = meta?.optString("voice") ?: "",
                    engine = meta?.optString("engine") ?: "",
                    created = meta?.optLong("created") ?: f.lastModified(),
                )
            }
            .sortedByDescending { it.created }

    fun delete(item: HistoryItem) {
        item.file.delete()
        sidecar(item.file).delete()
    }

    private fun sidecar(wav: File) = File(wav.parentFile, wav.nameWithoutExtension + ".json")
}
