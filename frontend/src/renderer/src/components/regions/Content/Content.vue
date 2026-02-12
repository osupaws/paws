<!-- frontend/src/renderer/src/components/regions/Content/Content.vue -->
<script setup lang="ts">
import { openSettings } from "@renderer/state/modal.state";
import { fetchPlugins, pluginState } from "@renderer/state/plugin.state";
import { setLegacyMode, settingsState } from "@renderer/state/settings.state";
import { importTheme, themeState } from "@renderer/state/theme.state";
import { computed, onMounted, onUnmounted, ref, watch } from "vue";

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

const iframeRef = ref<HTMLIFrameElement | null>(null);

// Calculate the iframe source based on the active plugin
const pluginSrc = computed(() => {
	console.log("[Content.vue] Recalculating pluginSrc. ActiveID:", pluginState.activePluginId);
	if (!pluginState.activePluginId) return "";
	const plugin = pluginState.loadedPlugins.find(p => p.id === pluginState.activePluginId);
	console.log("[Content.vue] Found plugin:", plugin);
	if (!plugin || !plugin.ui) {
		console.warn("[Content.vue] Plugin has no UI or not found.");
		return "";
	}

	// Custom protocol paws-plugin://[plugin-id]/[entry]?pluginId=[id]
	// Plugins often expect their own ID in the query params to work with the API
	const src = `paws-plugin://${plugin.id}/${plugin.ui.entry}?pluginId=${plugin.id}`;
	console.log("[Content.vue] Generated SRC:", src);
	return src;
});

function postThemeToIframe(iframe: HTMLIFrameElement, isInitial = false): void {
	// We must send a plain, clonable object, not a Vue Proxy object.
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

// --- Message Sending (Parent -> Iframe) ---
watch(iframeRef, iframe => {
	if (iframe) {
		iframe.addEventListener("load", () => {
			// Once the iframe is loaded, send the initial mode and theme.
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
			postThemeToIframe(iframe, true); // Send initial theme without animation
		});
	}
});

// Watch for changes in the active theme and notify the iframe
watch(
	() => themeState.activeThemeId,
	() => {
		if (iframeRef.value) {
			postThemeToIframe(iframeRef.value, false); // Send theme update with animation
		}
	}
);

// Watch for changes in Legacy Mode and notify the iframe
watch(
	() => settingsState.isLegacyMode,
	isLegacy => {
		if (iframeRef.value) {
			iframeRef.value.contentWindow?.postMessage(
				{
					channel: "notice",
					payload: {
						type: "mode-changed",
						mode: isLegacy ? "stable" : "lazer"
					}
				},
				"*"
			);
		}
	}
);

// --- Message Receiving (Iframe -> Parent) ---
async function handleMessageFromIframe(event: MessageEvent): Promise<void> {
	// Basic validation
	if (event.source !== iframeRef.value?.contentWindow) return;
	const { channel, id, payload } = event.data;
	if (!channel || id === undefined) return;

	try {
		let result: any;
		// Route the request from the iframe to the correct main process IPC handler
		// The `window.api` object is exposed by the preload script.
		if (channel === "post") {
			result = await window.api.backend.post(payload.endpoint, payload.body);
		} else if (channel === "get") {
			result = await window.api.backend.get(payload);
		} else if (channel === "storage") {
			// Routes to the storage API (uploadAsset, uploadTemp, uploadTempPath, processAsset)
			const method = payload.method as keyof typeof window.api.storage;
			if (typeof window.api.storage[method] === "function") {
				// @ts-ignore (dynamic call)
				result = await window.api.storage[method](payload.arg);
			} else {
				throw new Error(`Storage method ${method} not found.`);
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

		// Send the result back to the iframe
		iframeRef.value?.contentWindow?.postMessage({ id, result }, "*");
	} catch (error) {
		// If an error occurs, send it back to the iframe to reject the promise
		const errorMessage = error instanceof Error ? error.message : String(error);
		iframeRef.value?.contentWindow?.postMessage({ id, error: errorMessage }, "*");
	}
}

onMounted(async () => {
	window.addEventListener("message", handleMessageFromIframe);
	// Fetch plugins on mount
	await fetchPlugins();
});

onUnmounted(() => {
	window.removeEventListener("message", handleMessageFromIframe);
});
</script>

<template>
	<div class="content-container">
		<iframe
			v-if="pluginSrc"
			ref="iframeRef"
			:src="pluginSrc"
			sandbox="allow-scripts allow-forms"
			class="content-iframe"
		></iframe>
		<div v-else class="content-placeholder">
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
</style>
