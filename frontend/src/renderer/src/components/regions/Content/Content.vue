<!-- frontend/src/renderer/src/components/regions/Content/Content.vue -->
<script setup lang="ts">
// Linter Fix: Removed unused 'styles' import
defineProps<{
  pluginSrc: string;
}>();
</script>

<template>
  <!-- Linter Fix: The parent now handles the class and styling -->
  <div class="content-container">
    <iframe
      v-if="pluginSrc"
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
