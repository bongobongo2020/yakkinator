package com.yakkinator.remote.ui

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

// The desktop app's retro palette
val RetroBg = Color(0xFF0E0820)
val RetroCard = Color(0xFF1D1140)
val RetroBorder = Color(0xFF7B3FE4)
val RetroCyan = Color(0xFF00E5FF)
val RetroMuted = Color(0xFF7B6A9E)
val RetroError = Color(0xFFFF5C8A)

private val Colors = darkColorScheme(
    primary = RetroCyan,
    onPrimary = RetroBg,
    secondary = RetroBorder,
    onSecondary = Color.White,
    background = RetroBg,
    onBackground = Color.White,
    surface = RetroBg,
    onSurface = Color.White,
    surfaceVariant = RetroCard,
    onSurfaceVariant = Color(0xFFC9BEE6),
    surfaceContainer = RetroCard,
    surfaceContainerHigh = Color(0xFF261754),
    surfaceContainerHighest = Color(0xFF2E1C63),
    outline = RetroBorder,
    outlineVariant = RetroMuted,
    error = RetroError,
    secondaryContainer = Color(0xFF3A2380),
    onSecondaryContainer = Color.White,
)

@Composable
fun YakTheme(content: @Composable () -> Unit) {
    MaterialTheme(colorScheme = Colors, content = content)
}
