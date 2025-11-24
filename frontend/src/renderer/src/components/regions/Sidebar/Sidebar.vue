<!-- frontend/src/renderer/src/components/regions/Sidebar/Sidebar.vue -->
<script setup lang="ts">
import SidebarButton from "@renderer/components/SidebarButton/SidebarButton.vue";
import SettingsIcon from "@renderer/components/UI/Icon/SettingsIcon.vue";
import { onMounted, ref } from "vue";

import styles from "./Sidebar.module.css";

interface Plugin {
  id: string;
  name: string;
  version: string;
  description: string;
  ui: {
    entry: string;
  } | null;
}

const emit = defineEmits(["plugin-selected"]);

const plugins = ref<Plugin[]>([]);

// Linter Fix: Add async return type Promise<void>
onMounted(async (): Promise<void> => {
  try {
    const loadedPlugins = await window.api.backend.get("/api/plugins/loaded");
    console.log("Loaded plugins:", loadedPlugins);
    plugins.value = loadedPlugins;
  } catch (error) {
    console.error("Failed to load plugins:", error);
  }
});

// Linter Fix: Add explicit parameter type and void return type
const selectPlugin = (plugin: Plugin): void => {
  emit("plugin-selected", plugin);
};
</script>

<template>
  <div :class="styles.sidebar">
    <div :class="styles.pluginList">
      <SidebarButton
        v-for="plugin in plugins"
        :key="plugin.id"
        @click="selectPlugin(plugin)"
      >
        {{ plugin.name }}
      </SidebarButton>
    </div>

    <div :class="styles.settings">
      <SidebarButton>
        <SettingsIcon width="40px" height="40px" />
      </SidebarButton>
    </div>
  </div>
</template>
