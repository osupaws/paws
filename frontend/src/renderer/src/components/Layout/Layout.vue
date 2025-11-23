<script setup lang="ts">
import Content from "@renderer/components/regions/Content/Content.vue";
import Header from "@renderer/components/regions/Header/Header.vue";
import Sidebar from "@renderer/components/regions/Sidebar/Sidebar.vue";
import { ref } from "vue";

import styles from "./Layout.module.css";

const selectedPluginSrc = ref("");

function handlePluginSelected(plugin) {
  if (plugin.ui) {
    selectedPluginSrc.value = `paws-plugin://${plugin.id}/${plugin.ui.entry}?pluginId=${plugin.id}`;
  }
}
</script>

<template>
  <div :class="styles.layout">
    <Header />
    <div :class="styles.content">
      <Sidebar @plugin-selected="handlePluginSelected" />
      <Content :plugin-src="selectedPluginSrc" />
    </div>
  </div>
</template>
