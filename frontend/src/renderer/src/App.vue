<script setup lang="ts">
import Layout from "@renderer/components/Layout/Layout.vue";
import { themeState, getActiveThemeInfo } from "@renderer/state/theme.state";
import { computed, watch } from "vue";

// This watcher handles all side-effects of a theme change.
watch(() => themeState.activeThemeId, (newThemeId) => {
  if (!newThemeId) return;

  // 1. Save to store
  window.api.store.set('activeThemeId', newThemeId);

  // 2. Update the stylesheet <link> in the DOM
  const themeInfo = getActiveThemeInfo();
  const themeLink = document.getElementById('app-theme-link') as HTMLLinkElement;
  if (themeLink) {
    themeLink.href = `paws-app://${themeInfo.file}`;
  }
}, { immediate: true });


const currentTheme = computed(() => themeState.activeThemeId);
// TEMPORARY THEME SWITCH
const toggleTheme = (): void => {
  // Add transition class to body for animation
  document.body.classList.add('paws-theme-transitioning');

  // Perform state change, which will be picked up by the watcher
  themeState.activeThemeId = themeState.activeThemeId === 'dark' ? 'light' : 'dark';

  // Remove transition class after animation duration
  setTimeout(() => {
    document.body.classList.remove('paws-theme-transitioning');
  }, 300); // Match CSS transition duration
};
</script>

<template>
  <div class="app-container">
    <Layout />
    <button
      style="
        position: absolute;
        bottom: 10px;
        right: 10px;
        padding: 10px;
        z-index: 1000;
      "
      @click="toggleTheme"
    >
      Switch to {{ currentTheme === "dark" ? "Light" : "Dark" }} Theme
    </button>
  </div>
</template>

<style scoped>
.app-container {
  width: 100vw;
  height: 100vh;
  overflow: hidden;
}
</style>
