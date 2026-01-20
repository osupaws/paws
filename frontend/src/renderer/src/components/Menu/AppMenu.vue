<script setup lang="ts">
import { closeMenu, menuState } from "@renderer/state/menu.state";
import { pluginState, setActivePlugin } from "@renderer/state/plugin.state";
import { themeState } from "@renderer/state/theme.state";

import styles from "./AppMenu.module.css";

const selectPlugin = (id: string): void => {
  setActivePlugin(id);
  closeMenu();
};

const toggleTheme = (): void => {
  // Basic theme switch logic for testing
  const currentIndex = themeState.availableThemes.findIndex(
    (t) => t.id === themeState.activeThemeId,
  );
  const nextIndex = (currentIndex + 1) % themeState.availableThemes.length;
  themeState.activeThemeId = themeState.availableThemes[nextIndex].id;
};
</script>

<template>
  <div
    v-if="menuState.isOpen"
    :class="styles.menuWrapper"
    @click.self="closeMenu"
  >
    <div :class="[styles.menu, { [styles.open]: menuState.isOpen }]">
      <!-- Section: Client Launcher Placeholder -->
      <div :class="styles.section">
        <button :class="styles.launchButton">launch akatsuki</button>
      </div>

      <!-- Section: Plugins -->
      <div :class="styles.section">
        <div :class="styles.pluginList">
          <button
            v-for="plugin in pluginState.loadedPlugins"
            :key="plugin.id"
            :class="[
              styles.pluginItem,
              { [styles.active]: pluginState.activePluginId === plugin.id },
            ]"
            @click="selectPlugin(plugin.id)"
          >
            {{ plugin.name }}
          </button>

          <div
            v-if="pluginState.loadedPlugins.length === 0"
            :class="styles.pluginItem"
          >
            No plugins loaded
          </div>
        </div>
      </div>

      <!-- Footer: Settings, Theme, etc. -->
      <div :class="styles.footer">
        <button :class="styles.footerButton" title="Plugins Management">
          📦
        </button>
        <button
          :class="styles.footerButton"
          title="Switch Theme"
          @click="toggleTheme"
        >
          🌗
        </button>
        <button :class="styles.footerButton" title="Settings">⚙️</button>
      </div>
    </div>
  </div>
</template>
