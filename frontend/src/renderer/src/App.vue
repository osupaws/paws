<script setup lang="ts">
import Layout from "@renderer/components/Layout/Layout.vue";
import { themeState, getActiveThemeInfo } from "@renderer/state/theme.state";
import { computed, watch } from "vue";

// This watcher handles all side-effects of a theme change.
watch(() => themeState.activeThemeId, (newThemeId) => {
  if (!newThemeId) return;

  // 1. Save to store
  window.api.store.set('activeThemeId', newThemeId);

  // 2. Update the stylesheet <link>s in the DOM
  const themeInfo = getActiveThemeInfo();
  const baseThemeInfo = themeState.availableThemes.find(t => t.id === themeInfo.base);
  
  const baseLink = document.getElementById('app-theme-base-link') as HTMLLinkElement;
  const customLink = document.getElementById('app-theme-custom-link') as HTMLLinkElement;

  if (baseLink && baseThemeInfo) {
    baseLink.href = `paws-app://${baseThemeInfo.file}`;
  }

  if (customLink && themeInfo.id !== themeInfo.base) { // It's a custom theme
    customLink.href = `paws-app://${themeInfo.file}`;
  } else { // It's a base theme
    customLink.href = ''; // Clear custom styles
  }
}, { immediate: true });


const currentTheme = computed(() => getActiveThemeInfo());

// TEMPORARY THEME SWITCH
const toggleTheme = (): void => {
  // Add transition class to body for animation
  document.body.classList.add('paws-theme-transitioning');

  const currentIndex = themeState.availableThemes.findIndex(t => t.id === themeState.activeThemeId);
  const nextIndex = (currentIndex + 1) % themeState.availableThemes.length;
  themeState.activeThemeId = themeState.availableThemes[nextIndex].id;

  // Remove transition class after animation duration
  setTimeout(() => {
    document.body.classList.remove('paws-theme-transitioning');
  }, 300); // Match CSS transition duration
};

const nextThemeName = computed(() => {
  const currentIndex = themeState.availableThemes.findIndex(t => t.id === themeState.activeThemeId);
  const nextIndex = (currentIndex + 1) % themeState.availableThemes.length;
  return themeState.availableThemes[nextIndex]?.name || 'Unknown';
});
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
      Switch to {{ nextThemeName }}
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
