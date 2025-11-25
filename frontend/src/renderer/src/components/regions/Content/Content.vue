<!-- frontend/src/renderer/src/components/regions/Content/Content.vue -->
<script setup lang="ts">
import { getActiveThemeInfo, themeState } from "@renderer/state/theme.state";
import { onMounted, onUnmounted, ref, watch } from "vue";

defineProps<{
  pluginSrc: string;
}>();

const iframeRef = ref<HTMLIFrameElement | null>(null);

function postThemeToIframe(iframe: HTMLIFrameElement): void {
  const themeInfo = getActiveThemeInfo();
  // We must send a plain, clonable object, not a Vue Proxy object.
  const plainThemeInfo = JSON.parse(JSON.stringify(themeInfo));

  iframe.contentWindow?.postMessage(
    {
      channel: "notice",
      payload: {
        type: "theme-changed",
        theme: plainThemeInfo,
      },
    },
    "*",
  );
}

// --- Message Sending (Parent -> Iframe) ---
watch(iframeRef, (iframe) => {
  if (iframe) {
    iframe.addEventListener("load", () => {
      // Once the iframe is loaded, send the initial mode and theme.
      iframe.contentWindow?.postMessage(
        {
          channel: "notice",
          payload: {
            type: "mode-changed",
            mode: "stable",
          },
        },
        "*",
      );
      postThemeToIframe(iframe);
    });
  }
});

// Watch for changes in the active theme and notify the iframe
watch(
  () => themeState.activeThemeId,
  () => {
    if (iframeRef.value) {
      postThemeToIframe(iframeRef.value);
    }
  },
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
    iframeRef.value?.contentWindow?.postMessage(
      { id, error: errorMessage },
      "*",
    );
  }
}

onMounted(() => {
  window.addEventListener("message", handleMessageFromIframe);
});

onUnmounted(() => {
  window.removeEventListener("message", handleMessageFromIframe);
});
</script>

<template>
  <!-- Linter Fix: The parent now handles the class and styling -->
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

<!-- Linter Fix: Added scoped styles directly, as the module file was unused -->
<style scoped>
.content-container {
  flex: 1;
  height: 100%;
  background-color: var(--paws-color-bg-dark);
  border-radius: 16px 0 0 0;
  overflow: hidden;
}
.content-iframe {
  width: 100%;
  height: 100%;
  border: none;
}
.content-placeholder {
  margin: 12px;
}
</style>
