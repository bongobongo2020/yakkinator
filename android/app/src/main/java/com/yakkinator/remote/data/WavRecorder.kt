package com.yakkinator.remote.data

import android.annotation.SuppressLint
import android.media.AudioFormat
import android.media.AudioRecord
import android.media.MediaRecorder
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.isActive
import kotlinx.coroutines.withContext
import java.io.File
import java.io.RandomAccessFile
import kotlin.coroutines.coroutineContext

/**
 * Records mono 16-bit PCM straight to WAV, which both engines read without conversion.
 * Runs until the calling coroutine is cancelled.
 */
class WavRecorder(private val sampleRate: Int = 24_000) {

    /** Records into [dest]; [onLevel] gets a 0..1 loudness for a meter. */
    @SuppressLint("MissingPermission") // the caller requests RECORD_AUDIO first
    suspend fun record(dest: File, onLevel: (Float) -> Unit) = withContext(Dispatchers.IO) {
        val minBuf = AudioRecord.getMinBufferSize(sampleRate, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT)
        val recorder = AudioRecord(
            MediaRecorder.AudioSource.MIC, sampleRate,
            AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT, minBuf * 2,
        )
        if (recorder.state != AudioRecord.STATE_INITIALIZED) {
            recorder.release()
            throw IllegalStateException("Couldn't open the microphone")
        }

        RandomAccessFile(dest, "rw").use { out ->
            out.setLength(0)
            out.write(ByteArray(44)) // header is filled in once the length is known
            val buf = ShortArray(minBuf)
            val bytes = ByteArray(minBuf * 2)
            var dataLen = 0L
            recorder.startRecording()
            try {
                while (coroutineContext.isActive) {
                    val n = recorder.read(buf, 0, buf.size)
                    if (n <= 0) continue
                    var peak = 0
                    for (i in 0 until n) {
                        val s = buf[i].toInt()
                        bytes[i * 2] = (s and 0xFF).toByte()
                        bytes[i * 2 + 1] = (s shr 8 and 0xFF).toByte()
                        peak = maxOf(peak, kotlin.math.abs(s))
                    }
                    out.write(bytes, 0, n * 2)
                    dataLen += n * 2
                    onLevel(peak / 32768f)
                }
            } finally {
                recorder.stop()
                recorder.release()
                out.seek(0)
                out.write(header(dataLen))
            }
        }
    }

    private fun header(dataLen: Long): ByteArray {
        val byteRate = sampleRate * 2
        fun le32(v: Long) = byteArrayOf((v and 0xFF).toByte(), (v shr 8 and 0xFF).toByte(), (v shr 16 and 0xFF).toByte(), (v shr 24 and 0xFF).toByte())
        fun le16(v: Int) = byteArrayOf((v and 0xFF).toByte(), (v shr 8 and 0xFF).toByte())
        return "RIFF".toByteArray() + le32(36 + dataLen) + "WAVE".toByteArray() +
            "fmt ".toByteArray() + le32(16) + le16(1) + le16(1) + le32(sampleRate.toLong()) +
            le32(byteRate.toLong()) + le16(2) + le16(16) +
            "data".toByteArray() + le32(dataLen)
    }
}
