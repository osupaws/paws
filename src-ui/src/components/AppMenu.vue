<script setup lang="ts">
import { computed } from "vue";
import {
  DarkModeIcon,
  LightModeIcon,
  PawsMenuButton,
  PawsMultiSwitch,
  PluginIcon,
  SettingsIcon
} from "@osupaws/paws-ui";
import { menuState, closeMenu } from "../state/menu.state";
import { openSettings } from "../state/modal.state";
import { pluginState, setActivePlugin } from "../state/plugin.state";
import { configState } from "../state/config.state";
import { themeState, setThemeBase } from "../state/theme.state";

const goHome = () => {
  setActivePlugin(null);
  closeMenu();
};

const selectPlugin = (id: string) => {
  setActivePlugin(id);
  closeMenu();
};

const themeModel = computed({
  get: () => themeState.activeThemeId.includes("dark") ? "dark" : "light",
  set: (val: "dark" | "light") => {
    setThemeBase(val);
  }
});

const handleOpenSettings = () => {
  openSettings();
  closeMenu();
};

const handleOpenPlugins = () => {
  // Пока у нас нет PluginsModal, поэтому ничего не делаем или открываем плагины
  closeMenu();
};

// Формируем список доступных плагинов (из стейта + DevPlugin, если включен)
const availablePlugins = computed(() => {
  const list = [...pluginState.loadedPlugins];
  // Временно хардкодим DevPlugin в список, пока бекенд не присылает реальный список плагинов
  if (configState.isDeveloperModeEnabled && configState.devPluginPath) {
    if (!list.find(p => p.id === "org.test.hello")) {
      list.push({
        id: "org.test.hello",
        name: "Test Dev Plugin",
        version: "1.0.0",
        author: "Dev",
        description: "Hotplug Test Plugin"
      });
    }
  }
  return list;
});
</script>

<template>
  <Transition name="menu">
    <div v-if="menuState.isOpen" class="menu-wrapper" @click.self="closeMenu">
      <div class="menu-panel">
        <div class="menu-section padded-section">
          <PawsMenuButton label="home" @click="goHome" :active="!pluginState.activePluginId">
            home
          </PawsMenuButton>
        </div>

        <div class="menu-section">
          <div class="plugin-list">
            <PawsMenuButton
              v-for="plugin in availablePlugins"
              :key="plugin.id"
              :label="plugin.name"
              :active="pluginState.activePluginId === plugin.id"
              @click="selectPlugin(plugin.id)"
            >
              {{ plugin.name }}
            </PawsMenuButton>

            <div v-if="availablePlugins.length === 0" class="plugin-item">
              No plugins loaded
            </div>
          </div>
        </div>

        <div class="menu-footer">
          <PawsMenuButton
            class="footer-btn"
            label="Plugins"
            tooltip="Plugins"
            @click="handleOpenPlugins"
          >
            <template #icon>
              <PluginIcon />
            </template>
          </PawsMenuButton>

          <PawsMultiSwitch v-model="themeModel" :options="['light', 'dark']">
            <template #light>
              <LightModeIcon />
            </template>
            <template #dark>
              <DarkModeIcon />
            </template>
          </PawsMultiSwitch>

          <PawsMenuButton
            class="footer-btn"
            label="Settings"
            tooltip="Settings"
            @click="handleOpenSettings"
          >
            <template #icon>
              <SettingsIcon />
            </template>
          </PawsMenuButton>
        </div>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
.menu-wrapper {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  z-index: 2000;
  background-color: transparent;
  pointer-events: auto;
}

.menu-panel {
  position: absolute;
  top: 12px;
  left: 12px;
  width: 260px;
  background-color: var(--paws-color-bg-secondary);
  border-radius: var(--paws-rounding-medium);
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  box-shadow: 0 10px 30px rgba(0,0,0,0.3);
  transition: all 0.2s ease;
}

.menu-section {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.plugin-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.plugin-item {
  font-size: 14px;
  color: var(--paws-color-text-secondary);
  padding: 8px;
  text-align: center;
}

.menu-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 4px;
  gap: 8px;
}

.footer-btn {
  flex: 0 0 auto;
}

.menu-enter-active,
.menu-leave-active {
  transition: opacity 0.2s ease, transform 0.2s cubic-bezier(0.34, 1.35, 0.64, 1);
}

.menu-enter-from,
.menu-leave-to {
  opacity: 0;
  transform: translateY(-10px) scale(0.98);
}

.menu-enter-to,
.menu-leave-from {
  opacity: 1;
  transform: translateY(0);
}
</style>
