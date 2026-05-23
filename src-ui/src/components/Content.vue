<script setup lang="ts">
import { ref, watch, onMounted, onUnmounted } from "vue";
import { pluginState } from "../state/plugin.state";
import { themeState } from "../state/theme.state";
import { configState } from "../state/config.state";

// Reference to the active iframe
const iframeRef = ref<HTMLIFrameElement | null>(null);

// State for zero-flash loading overlay
const isLoading = ref(false);
const isFadingOut = ref(false);

const setIframeRef = (el: any) => {
  iframeRef.value = el as HTMLIFrameElement | null;
};

// --- postMessage Bridge ---

function postThemeToIframe(isInitial = false) {
  if (!iframeRef.value?.contentWindow) return;
  const activeTheme = themeState.availableThemes.find(t => t.id === themeState.activeThemeId);
  const baseThemeId = activeTheme?.baseThemeId || (themeState.activeThemeId.startsWith("paws-") ? themeState.activeThemeId : "paws-dark");
  const baseHref = `http://pawsapp.localhost/themes/${baseThemeId}.css`;
  const customHref = activeTheme?.blobHash ? `http://pawstheme.localhost/${activeTheme.blobHash}` : "";

  iframeRef.value.contentWindow.postMessage(
    {
      type: "paws:theme-changed",
      baseHref,
      customHref,
      themeId: themeState.activeThemeId,
      initial: isInitial
    },
    "*"
  );
}

function postModeToIframe() {
  if (!iframeRef.value?.contentWindow) return;
  const mode = configState.isLegacyMode ? "stable" : "lazer";
  iframeRef.value.contentWindow.postMessage(
    {
      channel: "notice",
      payload: {
        type: "mode-changed",
        mode
      }
    },
    "*"
  );
}

// Watchers for theme and mode to sync to running plugin
watch(() => themeState.activeThemeId, () => {
  postThemeToIframe(false);
});

watch(() => configState.isLegacyMode, () => {
  postModeToIframe();
});

// Watch active plugin to handle loading state
watch(() => pluginState.activePluginId, (newId, oldId) => {
  if (newId !== oldId && newId) {
    isLoading.value = true;
    isFadingOut.value = false;
  } else if (!newId) {
    isLoading.value = false;
    isFadingOut.value = false;
  }
});

// Message listener from iframe
async function handleMessageFromIframe(event: MessageEvent) {
  if (!iframeRef.value || iframeRef.value.contentWindow !== event.source) return;

  const { channel, id, payload } = event.data;
  if (!channel) return;

  if (channel === "paws:client-ready") {
    console.log(`[Content.vue] Plugin ready signal received`);
    
    // Sync initial state
    postThemeToIframe(true);
    postModeToIframe();

    // Start fade out overlay
    isFadingOut.value = true;
    setTimeout(() => {
      isLoading.value = false;
      isFadingOut.value = false;
    }, 300); // 300ms fade transition
    return;
  }

  if (channel === "paws:rpc") {
    if (!payload || !payload.action) return;
    
    // Forward to sidecar
    import("../utils/sidecar-bridge").then(({ callSidecar }) => {
      callSidecar(payload.action, payload.params || {}, pluginState.activePluginId || "unknown").then(response => {
        // Send back to iframe
        if (iframeRef.value?.contentWindow) {
          iframeRef.value.contentWindow.postMessage({
            channel: "paws:rpc:response",
            id,
            payload: response
          }, "*");
        }
      });
    });
    return;
  }
}

const handleIframeLoad = () => {
  // Fallback for plugins that don't send paws:client-ready
  setTimeout(() => {
    if (isLoading.value) {
      console.warn('[Content.vue] Plugin did not send paws:client-ready. Hiding spinner via native load event.');
      isFadingOut.value = true;
      setTimeout(() => {
        isLoading.value = false;
        isFadingOut.value = false;
      }, 300);
    }
  }, 500);
};

onMounted(() => {
  window.addEventListener("message", handleMessageFromIframe);
});

onUnmounted(() => {
  window.removeEventListener("message", handleMessageFromIframe);
});
</script>

<template>
  <div class="content-container">
    <!-- Active Plugin IFrame (Destroyed when inactive to save RAM) -->
    <iframe
      v-if="pluginState.activePluginId"
      :ref="setIframeRef"
      :src="`http://pawsplugin.localhost/${pluginState.activePluginId}/index.html`"
      class="content-iframe"
      @load="handleIframeLoad"
    ></iframe>

    <!-- Loading Overlay -->
    <div
      v-if="pluginState.activePluginId && isLoading"
      class="loading-overlay"
      :class="{ 'fade-out': isFadingOut }"
    >
      <div class="spinner"></div>
      <span>Loading Plugin...</span>
    </div>

    <!-- Dashboard/Placeholder -->
    <router-view v-else-if="!pluginState.activePluginId && $route" />
    
    <div v-else-if="!pluginState.activePluginId" class="placeholder">
      <div class="status-badge">system ready</div>
      <p class="status-text">waiting for core...</p>
    </div>
  </div>
</template>

<style scoped>
.content-container {
  width: 100%;
  height: 100%;
  position: relative;
  display: flex;
  flex-direction: column;
}

.content-iframe {
  width: 100%;
  height: 100%;
  border: none;
  background: transparent;
  flex: 1;
}

.placeholder {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
}

.status-badge {
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 1px;
  background: var(--paws-color-bg-tertiary);
  padding: 4px 8px;
  border-radius: 4px;
  color: var(--paws-color-accent-primary);
  font-weight: var(--paws-font-weight-bold);
}

.status-text {
  font-size: 14px;
  color: var(--paws-color-text-secondary);
  font-weight: var(--paws-font-weight-light);
}

.loading-overlay {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background: var(--paws-color-bg-subprimary);
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
  pointer-events: none;
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
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}
</style>
