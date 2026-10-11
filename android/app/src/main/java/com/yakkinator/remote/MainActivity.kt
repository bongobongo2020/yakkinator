package com.yakkinator.remote

import android.content.Intent
import android.os.Bundle
import android.graphics.Color
import androidx.activity.ComponentActivity
import androidx.activity.SystemBarStyle
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.consumeWindowInsets
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.GraphicEq
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.Link
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import com.yakkinator.remote.ui.ConnectScreen
import com.yakkinator.remote.ui.ConnectionState
import com.yakkinator.remote.ui.GenerateScreen
import com.yakkinator.remote.ui.HistoryScreen
import com.yakkinator.remote.ui.RefreshButton
import com.yakkinator.remote.ui.RetroBg
import com.yakkinator.remote.ui.RetroCyan
import com.yakkinator.remote.ui.YakTheme
import com.yakkinator.remote.ui.YakViewModel

class MainActivity : ComponentActivity() {
    private val vm: YakViewModel by viewModels()

    @OptIn(ExperimentalMaterial3Api::class)
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        // Light icons: the app is always dark
        enableEdgeToEdge(SystemBarStyle.dark(Color.TRANSPARENT), SystemBarStyle.dark(Color.TRANSPARENT))
        if (savedInstanceState == null) handlePairingIntent(intent)

        setContent {
            YakTheme {
                val startTab = if (vm.connection == ConnectionState.NotConfigured) 2 else 0
                var tab by rememberSaveable { mutableIntStateOf(startTab) }
                Scaffold(
                    containerColor = RetroBg,
                    topBar = {
                        TopAppBar(
                            title = {
                                Text("THE YAKKINATOR", fontFamily = FontFamily.Monospace, fontWeight = FontWeight.Bold, color = RetroCyan)
                            },
                            actions = { if (tab == 0) RefreshButton(vm) },
                            colors = TopAppBarDefaults.topAppBarColors(containerColor = RetroBg),
                        )
                    },
                    bottomBar = {
                        NavigationBar {
                            NavigationBarItem(tab == 0, { tab = 0 }, { Icon(Icons.Default.GraphicEq, null) }, label = { Text("Generate") })
                            NavigationBarItem(tab == 1, { tab = 1 }, { Icon(Icons.Default.History, null) }, label = { Text("History") })
                            NavigationBarItem(tab == 2, { tab = 2 }, { Icon(Icons.Default.Link, null) }, label = { Text("Connect") })
                        }
                    },
                ) { padding ->
                    Box(Modifier.padding(padding).consumeWindowInsets(padding).imePadding()) {
                        when (tab) {
                            0 -> GenerateScreen(vm, onOpenConnect = { tab = 2 })
                            1 -> HistoryScreen(vm)
                            else -> ConnectScreen(vm, onConnected = { tab = 0 })
                        }
                    }
                }
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        handlePairingIntent(intent)
    }

    /** A pairing QR scanned with the phone's camera app opens yakkinator://pair?... here. */
    private fun handlePairingIntent(intent: Intent?) {
        val data = intent?.data ?: return
        vm.applyPairing(data.toString())
    }
}
