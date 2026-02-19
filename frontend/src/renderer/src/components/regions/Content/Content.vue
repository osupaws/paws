<!-- frontend/src/renderer/src/components/regions/Content/Content.vue -->
<script setup lang="ts">
import { openSettings } from "@renderer/state/modal.state";
import {
	ensurePluginRunning,
	fetchPlugins,
	pluginState,
	setPluginReady,
	setPluginUiState
} from "@renderer/state/plugin.state";
import { setLegacyMode, settingsState } from "@renderer/state/settings.state";
import { importTheme, themeState } from "@renderer/state/theme.state";
import { onMounted, onUnmounted, ref, watch } from "vue";

const handleImportTheme = async (): Promise<void> => {
	const result = await window.api.electron.showOpenDialog({
		properties: ["openFile"],
		filters: [{ name: "Paws Theme", extensions: ["pawstheme", "zip"] }]
	});

	if (!result.canceled && result.filePaths.length > 0) {
		await importTheme(result.filePaths[0]);
	}
};

const handleInstallPlugin = async (): Promise<void> => {
	const result = await window.api.electron.showOpenDialog({
		properties: ["openFile"],
		filters: [{ name: "Paws Plugins", extensions: ["pawsplugin", "zip"] }]
	});

	if (result && result.filePaths.length > 0) {
		const filePath = result.filePaths[0];
		try {
			await window.api.backend.post("/api/plugins/install", { filePath });
			await fetchPlugins();
		} catch (e) {
			console.error("Plugin install failed", e);
		}
	}
};

const toggleTheme = (): void => {
	const currentIndex = themeState.availableThemes.findIndex(t => t.id === themeState.activeThemeId);
	const nextIndex = (currentIndex + 1) % themeState.availableThemes.length;
	themeState.activeThemeId = themeState.availableThemes[nextIndex].id;
	// Persist changes
	window.api.backend.post("/api/settings", {
		key: "activeThemeId",
		value: themeState.activeThemeId
	});
};

const toggleMode = (): void => {
	setLegacyMode(!settingsState.isLegacyMode);
};

const handleOpenSettings = (): void => {
	openSettings();
};

// Map of iframe refs: pluginId -> HTMLIFrameElement
const iframeRefs = ref<Map<string, HTMLIFrameElement>>(new Map());
// Set of plugins currently fading out their spinner
const fadingPlugins = ref(new Set<string>());

const setIframeRef = (el: any, id: string): void => {
	if (el) {
		iframeRefs.value.set(id, el as HTMLIFrameElement);
	} else {
		iframeRefs.value.delete(id);
	}
};

// ... (postThemeToIframe, postModeToIframe, notifyLifecycle unchanged) ...

function postThemeToIframe(iframe: HTMLIFrameElement, isInitial = false): void {
	const plainThemeState = JSON.parse(JSON.stringify(themeState));
	iframe.contentWindow?.postMessage(
		{
			channel: "notice",
			payload: {
				type: "theme-changed",
				themeState: plainThemeState,
				initial: isInitial
			}
		},
		"*"
	);
}

function postModeToIframe(iframe: HTMLIFrameElement): void {
	iframe.contentWindow?.postMessage(
		{
			channel: "notice",
			payload: {
				type: "mode-changed",
				mode: settingsState.isLegacyMode ? "stable" : "lazer"
			}
		},
		"*"
	);
}

// Lifecycle: Focus/Blur
function notifyLifecycle(pluginId: string, event: "focus" | "blur"): void {
	const iframe = iframeRefs.value.get(pluginId);
	if (iframe) {
		iframe.contentWindow?.postMessage(
			{
				channel: "lifecycle",
				payload: { event }
			},
			"*"
		);
	}
	// Notify Backend
	setPluginUiState(pluginId, event === "focus");
}

// ... (watchers unchanged) ...

// Watch active plugin to trigger focus/blur and ensure running
watch(
	() => pluginState.activePluginId,
	(newId, oldId) => {
		console.log(`[Content.vue] Active Plugin Changed: ${oldId} -> ${newId}`);

		if (oldId) {
			notifyLifecycle(oldId, "blur");
		}

		if (newId) {
			ensurePluginRunning(newId);
			// Focus will be sent after iframe loads if it's new,
			// or immediately if already running.
			// We delay slightly to ensure v-show has updated if needed,
			// though for 'focus' event logic it's less critical than rendering.
			setTimeout(() => {
				notifyLifecycle(newId, "focus");
			}, 0);
		}
	},
	{ immediate: true }
);

// Watch for changes in theme/mode to broadcast to ALL running plugins
watch(
	() => themeState.activeThemeId,
	() => {
		iframeRefs.value.forEach(iframe => postThemeToIframe(iframe, false));
	}
);

watch(
	() => settingsState.isLegacyMode,
	() => {
		iframeRefs.value.forEach(iframe => postModeToIframe(iframe));
	}
);

// --- Message Receiving (Iframe -> Parent) ---
async function handleMessageFromIframe(event: MessageEvent): Promise<void> {
	// Find which plugin sent this message
	const senderEntry = Array.from(iframeRefs.value.entries()).find(
		// eslint-disable-next-line @typescript-eslint/no-unused-vars
		([_id, frame]) => frame.contentWindow === event.source
	);

	if (!senderEntry) return; // Message not from our plugins

	const [senderId, senderFrame] = senderEntry;
	const { channel, id, payload } = event.data;

	if (!channel) return;

	// Handle "Ready" signal
	if (channel === "paws:client-ready") {
		console.log(`[Content.vue] Plugin ${senderId} is READY`);

		// Initialize state for the new plugin immediately so it can render while hidden
		postThemeToIframe(senderFrame, true);
		postModeToIframe(senderFrame);

		// Start fade-out effect immediately
		fadingPlugins.value.add(senderId);

		// Delay showing the plugin to allow styles/animations to settle (Zero-Flash)
		// and to allow the fade-out to complete
		setTimeout(() => {
			setPluginReady(senderId);
			fadingPlugins.value.delete(senderId);

			// If it's the active one, ensure it gets focus
			if (pluginState.activePluginId === senderId) {
				notifyLifecycle(senderId, "focus");
			}
		}, 300);
		return;
	}

	if (id === undefined) return; // Expecting RPC style for other channels
	// ... (rest of RPC logic unchanged) ...
	try {
		let result: any;
		if (channel === "post") {
			result = await window.api.backend.post(payload.endpoint, payload.body);
		} else if (channel === "get") {
			result = await window.api.backend.get(payload);
		} else if (channel === "storage") {
			const method = payload.method as keyof typeof window.api.storage;
			if (typeof window.api.storage[method] === "function") {
				// @ts-ignore: Dynamic method call on storage API
				result = await window.api.storage[method](payload.arg);
			} else {
				throw new Error(`Storage method ${String(method)} not found.`);
			}
		} else if (channel === "show-open-dialog") {
			result = await window.api.electron.showOpenDialog(payload);
		} else if (channel === "restart-app") {
			result = await window.api.electron.restartApp();
		} else if (channel === "resize-window") {
			result = await window.api.electron.resizeWindow(payload.isCompact);
		} else {
			console.warn(`[Content.vue] Unknown channel from iframe: ${channel}`, payload);
			throw new Error(`Unknown channel: ${channel}`);
		}

		senderFrame.contentWindow?.postMessage({ id, result }, "*");
	} catch (error) {
		const errorMessage = error instanceof Error ? error.message : String(error);
		// Ensure id is a string to avoid symbol conversion errors
		senderFrame.contentWindow?.postMessage({ id: String(id), error: errorMessage }, "*");
	}
}

onMounted(async () => {
	window.addEventListener("message", handleMessageFromIframe);
	await fetchPlugins();
});

onUnmounted(() => {
	window.removeEventListener("message", handleMessageFromIframe);
});
</script>

<template>
	<div class="content-container">
		<!-- Running Plugins (Keep-Alive Iframes) -->
		<template v-for="[id, plugin] in pluginState.runningPlugins" :key="id">
			<iframe
				v-show="pluginState.activePluginId === id && (plugin.isReady || fadingPlugins.has(id))"
				:ref="el => setIframeRef(el, id)"
				:src="plugin.src"
				sandbox="allow-scripts allow-forms"
				class="content-iframe"
			></iframe>
		</template>

		<!-- Loading Overlay for Active Plugin -->
		<div
			v-if="
				pluginState.activePluginId &&
				pluginState.runningPlugins.get(pluginState.activePluginId) &&
				!pluginState.runningPlugins.get(pluginState.activePluginId)?.isReady
			"
			class="loading-overlay"
			:class="{ 'fade-out': fadingPlugins.has(pluginState.activePluginId!) }"
		>
			<div class="spinner"></div>
			<span>Loading Plugin...</span>
		</div>

		<!-- Placeholder (Home) -->
		<div v-show="!pluginState.activePluginId" class="content-placeholder">
			<div class="placeholder-wrapper">
				<span>Select a plugin</span>
				<span>(or press the buttons below)</span>
				<div class="actions-column">
					<button class="action-btn" @click="handleImportTheme">Import Theme (Debug)</button>
					<button class="action-btn" @click="handleInstallPlugin">Install Plugin (Debug)</button>
					<button class="action-btn" @click="toggleTheme">
						Switch Theme: {{ themeState.activeThemeId }}
					</button>
					<button class="action-btn" @click="toggleMode">
						Switch Mode: {{ settingsState.isLegacyMode ? "Stable" : "Lazer" }}
					</button>
					<button class="action-btn" @click="handleOpenSettings">Open Settings</button>
				</div>
			</div>
		</div>
	</div>
</template>

<style scoped>
.content-container {
	flex: 1;
	height: 100%;
	background-color: var(--paws-color-bg-primary);
	border-radius: var(--paws-rounding-big, 16px) var(--paws-rounding-big, 16px) 0 0;
	overflow: hidden;
}
.content-iframe {
	width: 100%;
	height: 100%;
	border: none;
}
.content-placeholder {
	height: 100%;
	color: var(--paws-color-text-secondary);
}
.placeholder-wrapper {
	width: 100%;
	height: 100%;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	gap: 12px;
}
.actions-column {
	display: flex;
	flex-direction: column;
	gap: 8px;
	width: 250px;
}
.action-btn {
	padding: 10px 16px;
	background: var(--paws-color-bg-secondary);
	border: 1px solid var(--paws-color-bg-tertiary);
	color: var(--paws-color-text-primary);
	border-radius: 6px;
	cursor: pointer;
	text-align: center;
	transition: background 0.2s;
}
.action-btn:hover {
	background: var(--paws-color-interactive-hover);
}

.loading-overlay {
	position: absolute;
	top: 0;
	left: 0;
	width: 100%;
	height: 100%;
	background: var(--paws-color-bg-primary);
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	gap: 16px;
	z-index: 10;
	color: var(--paws-color-text-secondary);
	transition: opacity 0.3s ease-out;
}

.loading-overlay.fade-out {
	opacity: 0;
}

.spinner {
	width: 40px;
	height: 40px;
	border: 4px solid var(--paws-color-bg-tertiary);
	border-top: 4px solid var(--paws-color-accent-primary);
	border-radius: 50%;
	animation: spin 1s linear infinite;
}

@keyframes spin {
	0% {
		transform: rotate(0deg);
	}
	100% {
		transform: rotate(360deg);
	}
}
</style>
