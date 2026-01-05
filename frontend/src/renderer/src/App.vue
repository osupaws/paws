<script setup lang="ts">
import Layout from "@renderer/components/Layout/Layout.vue";
import { themeState } from "@renderer/state/theme.state";
import { computed } from "vue";

const currentTheme = computed(() => themeState.activeThemeId);
// TEMPORARY THEME SWITCH
const toggleTheme = (): void => {
  // Add transition class to body
  document.body.classList.add('paws-theme-transitioning');

  // Perform theme change
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
