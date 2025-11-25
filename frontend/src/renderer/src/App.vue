<script setup lang="ts">
import Layout from "@renderer/components/Layout/Layout.vue";
import { themeState } from '@renderer/state/theme.state';
import { computed } from 'vue';

const currentTheme = computed(() => themeState.activeThemeId);

const toggleTheme = () => {
  themeState.activeThemeId = themeState.activeThemeId === 'dark' ? 'light' : 'dark';
};
</script>

<template>
  <div class="app-container">
    <Layout />
    <button @click="toggleTheme" style="position: absolute; bottom: 10px; right: 10px; padding: 10px; z-index: 1000;">
      Switch to {{ currentTheme === 'dark' ? 'Light' : 'Dark' }} Theme
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
