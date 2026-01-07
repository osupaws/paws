<script setup lang="ts">
import Layout from "@renderer/components/Layout/Layout.vue";
import { getActiveThemeInfo, themeState } from "@renderer/state/theme.state";
import { computed, watch } from "vue";
// This watcher handles all side effects of a theme change.
watch(
  () => themeState.activeThemeId,
  (newThemeId) => {
    if (!newThemeId) return;

    // 1. Save to store
    window.api.store.set("activeThemeId", newThemeId);

    // 2. Update the stylesheet <link>s in the DOM
    const themeInfo = getActiveThemeInfo();
    // Base theme ID will now be like 'paws-dark', so 'dark' will be 'themeInfo.base'
    const baseThemeId = `paws-${themeInfo.base}`;
    const baseThemeInfo = themeState.availableThemes.find(
      (t) => t.id === baseThemeId,
    );

    const baseLink = document.getElementById(
      "app-theme-base-link",
    ) as HTMLLinkElement;
    const customLink = document.getElementById(
      "app-theme-custom-link",
    ) as HTMLLinkElement;

    if (baseLink && baseThemeInfo) {
      // Core themes are always served from the app's internal assets
      baseLink.href = `paws-app://${baseThemeInfo.file}`;
    }

    if (customLink && themeInfo.isCustom) {
      // Custom themes are served from the user's data directory
      customLink.href = `paws-theme://${themeInfo.file}`; // Renamed protocol
    } else {
      // It's a core theme, so no custom styles are needed
      customLink.href = "";
    }
  },
  { immediate: true },
);

// TEMPORARY THEME SWITCH
const toggleTheme = (): void => {
  // Add transition class to body for animation
  document.body.classList.add("paws-theme-transitioning");

  const currentIndex = themeState.availableThemes.findIndex(
    (t) => t.id === themeState.activeThemeId,
  );
  const nextIndex = (currentIndex + 1) % themeState.availableThemes.length;
  themeState.activeThemeId = themeState.availableThemes[nextIndex].id;

  // Remove transition class after animation duration
  setTimeout(() => {
    document.body.classList.remove("paws-theme-transitioning");
  }, 300); // Match CSS transition duration
};

const nextThemeName = computed(() => {
  const currentIndex = themeState.availableThemes.findIndex(
    (t) => t.id === themeState.activeThemeId,
  );
  const nextIndex = (currentIndex + 1) % themeState.availableThemes.length;
  return themeState.availableThemes[nextIndex]?.name || "Unknown";
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
