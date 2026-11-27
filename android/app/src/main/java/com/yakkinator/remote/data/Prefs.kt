package com.yakkinator.remote.data

import android.content.Context

class Prefs(context: Context) {
    private val sp = context.getSharedPreferences("yakkinator", Context.MODE_PRIVATE)

    var serverUrl: String
        get() = sp.getString("server_url", "") ?: ""
        set(value) = sp.edit().putString("server_url", value).apply()

    var token: String
        get() = sp.getString("token", "") ?: ""
        set(value) = sp.edit().putString("token", value).apply()

    var engine: String
        get() = sp.getString("engine", "indextts") ?: "indextts"
        set(value) = sp.edit().putString("engine", value).apply()

    var lastVoice: String
        get() = sp.getString("last_voice", "") ?: ""
        set(value) = sp.edit().putString("last_voice", value).apply()

    var text: String
        get() = sp.getString("text", "") ?: ""
        set(value) = sp.edit().putString("text", value).apply()

    var indicLanguage: String
        get() = sp.getString("indic_language", "") ?: ""
        set(value) = sp.edit().putString("indic_language", value).apply()

    var indicSpeaker: String
        get() = sp.getString("indic_speaker", "") ?: ""
        set(value) = sp.edit().putString("indic_speaker", value).apply()

    var indicStyle: String
        get() = sp.getString("indic_style", "") ?: ""
        set(value) = sp.edit().putString("indic_style", value).apply()
}
