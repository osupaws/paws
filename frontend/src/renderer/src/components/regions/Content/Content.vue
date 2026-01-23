<!-- frontend/src/renderer/src/components/regions/Content/Content.vue -->
<script setup lang="ts">
import { fetchPlugins, pluginState } from "@renderer/state/plugin.state";
import { themeState } from "@renderer/state/theme.state";
import { computed, onMounted, onUnmounted, ref, watch } from "vue";

const iframeRef = ref<HTMLIFrameElement | null>(null);

// Calculate the iframe source based on the active plugin
const pluginSrc = computed(() => {
	if (!pluginState.activePluginId) return "";
	const plugin = pluginState.loadedPlugins.find(p => p.id === pluginState.activePluginId);
	if (!plugin || !plugin.ui) return "";

	// Custom protocol paws-plugin://[plugin-id]/[entry]?pluginId=[id]
	// Plugins often expect their own ID in the query params to work with the API
	return `paws-plugin://${plugin.id}/${plugin.ui.entry}?pluginId=${plugin.id}`;
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
						mode: "stable"
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
		} else {
			// Handle other channels if needed in the future
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
		<div v-else class="content-placeholder">Select a plugin</div>
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
	display: flex;
	align-items: center;
	justify-content: center;
	height: 100%;
	color: var(--paws-color-text-secondary);
}
</style>
