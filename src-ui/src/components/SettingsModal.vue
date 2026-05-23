<script setup lang="ts">
import { ref, onMounted } from "vue";
import { open } from "@tauri-apps/plugin-dialog";
import { enable, disable } from "@tauri-apps/plugin-autostart";
import { 
  PawsCard, 
  PawsHeading, 
  PawsModal, 
  PawsInput, 
  PawsCheckbox, 
  PawsDropdown, 
  PawsSubButton,
  FolderIcon,
  ThemeIcon,
  CloseIcon,
  DownloadIcon
} from "@osupaws/paws-ui";
import { configState, saveConfig } from "../state/config.state";
import { themeState, initThemes } from "../state/theme.state";
import { modalState, closeSettings } from "../state/modal.state";
import { callSidecar } from "../utils/sidecar-bridge";
import { fetchProfile, clearProfile, profileState } from "../state/profile.state";


// Local state for paths to prevent excessive backend calls if needed, 
// though saveConfig is manual or on change in the old project.
// In the old project it was: @focusout="handleLazerSave", @keydown.enter="handleLazerSave"

const selectPath = async (type: 'stable' | 'lazer' | 'devPlugin') => {
  let titleStr = "Select osu! Directory";
  if (type === 'stable') titleStr = "Select osu! Stable Directory";
  if (type === 'lazer') titleStr = "Select osu!lazer Directory";
  if (type === 'devPlugin') titleStr = "Select Dev Plugin Output Directory (e.g. net8.0)";

  const selected = await open({
    directory: true,
    multiple: false,
    title: titleStr,
  });

  if (selected && typeof selected === 'string') {
    if (type === 'stable') configState.stablePath = selected;
    else if (type === 'lazer') configState.lazerPath = selected;
    else if (type === 'devPlugin') {
      configState.devPluginPath = selected;
      await callSidecar("loadDevPlugin", { path: selected });
    }
    await saveConfig();
  }
};

const handleThemeChange = async (newThemeId: string) => {
  configState.currentThemeId = newThemeId;
  await saveConfig();
};

const handleImportTheme = async () => {
  const selected = await open({
    multiple: false,
    filters: [{ name: "Paws Theme", extensions: ["pawstheme"] }]
  });

  if (selected && typeof selected === 'string') {
    const resp = await callSidecar("importPackage", { path: selected });
    if (resp.success) {
      await initThemes();
    } else {
      console.error("Theme import failed:", resp.error);
    }
  }
};

const handleToggleHideInTray = async (val: boolean) => {
  configState.isHideInTrayOnClose = val;
  await saveConfig();
};

const handleToggleLaunchOnStartup = async (val: boolean) => {
  configState.isLaunchOnStartup = val;
  await saveConfig();
  try {
    if (val) await enable();
    else await disable();
  } catch (e) {
    console.error("[Autostart]", e);
  }
};

const handleToggleInvisibleLaunch = async (val: boolean) => {
  configState.isInvisibleLaunch = val;
  await saveConfig();
};

const handleToggleLegacy = async (val: boolean) => {
  configState.isLegacyMode = val;
  await saveConfig();
};

const handleToggleSwitchOnLogo = async (val: boolean) => {
  configState.isSwitchOnLogoEnabled = val;
  await saveConfig();
};

const handleToggleShowTips = async (val: boolean) => {
  configState.isShowTips = val;
  await saveConfig();
};

let devClickCount = 0;
let lastDevClickTime = 0;
const handleVersionClick = async () => {
  const now = Date.now();
  if (now - lastDevClickTime > 800) {
    // Пауза слишком долгая, сбрасываем счетчик
    devClickCount = 1;
  } else {
    devClickCount++;
  }
  
  lastDevClickTime = now;

  if (devClickCount >= 5) {
    configState.isDeveloperModeEnabled = !configState.isDeveloperModeEnabled;
    await saveConfig();
    devClickCount = 0;
  }
};

import { invoke } from '@tauri-apps/api/core';
import { setActivePlugin } from '../state/plugin.state';

const handleForceReloadDevPlugin = async () => {
  if (configState.devPluginPath) {
    await callSidecar("loadDevPlugin", { path: configState.devPluginPath });
    // Регистрируем путь в Tauri для раздачи статики
    await invoke("register_plugin_path", { 
      pluginId: "org.test.hello", 
      path: configState.devPluginPath 
    });
    // Переключаем интерфейс на этот плагин и закрываем настройки
    setActivePlugin("org.test.hello");
    closeSettings();
  }
};

const handleManualSave = async () => {
  await saveConfig();
};

// Versions (mocked for now as we don't have update logic yet)
const appVersion = ref("0.1.0-tauri");
const schemaVersion = ref("20240412"); // matches current paws-next goal

// osu! Connection Integration
const isOsuConnected = ref(false);
const isConnecting = ref(false);
const osuToken = ref<string | null>(null);

const checkOsuConnection = async () => {
  const resp = await callSidecar<string | null>("getOsuAccessToken");
  if (resp.success && resp.data) {
    isOsuConnected.value = true;
    osuToken.value = resp.data;
    await fetchProfile();
  } else {
    isOsuConnected.value = false;
    osuToken.value = null;
    clearProfile();
  }
};

const handleConnectOsu = async () => {
  isConnecting.value = true;
  const initResp = await callSidecar<string>("initiateOsuLogin");
  if (!initResp.success) {
    console.error("Failed to initiate osu! login:", initResp.error);
    isConnecting.value = false;
    return;
  }
  
  const waitResp = await callSidecar<boolean>("waitForOsuCallback", { timeout: 120 });
  if (waitResp.success && waitResp.data) {
    console.log("osu! connected successfully!");
  } else {
    console.error("osu! connection failed or timed out:", waitResp.error);
  }
  
  isConnecting.value = false;
  await checkOsuConnection();
};

const handleDisconnectOsu = async () => {
  await callSidecar("logoutOsu");
  await checkOsuConnection();
};

const isRefreshing = ref(false);
const handleRefreshProfile = async () => {
  if (isRefreshing.value) return;
  isRefreshing.value = true;
  await fetchProfile(true);
  isRefreshing.value = false;
};

onMounted(() => {
  checkOsuConnection();
});

</script>

<template>
  <PawsModal 
    :is-open="modalState.isSettingsOpen" 
    teleport-to="#paws-modal-root" 
    @close="closeSettings"
  >
    <template #heading>
      <PawsHeading size="lg" font-weight="medium" align="left">settings</PawsHeading>
    </template>

    <template #actions>
      <PawsSubButton size="medium" text="close" @click="closeSettings">
        <template #icon>
          <CloseIcon />
        </template>
      </PawsSubButton>
    </template>

    <div class="cards-container" @mousedown.stop>
      <!-- General -->
      <PawsCard mode="titled" class="settings-card">
        <template #heading>
          <PawsHeading size="lg" font-weight="medium" align="left">general</PawsHeading>
        </template>

        <div class="input-row">
          <PawsInput
            v-model="configState.lazerPath"
            v-paws-tooltip="'The folder where your osu!lazer data is stored (contains client.db)'"
            button-text="lazer path"
            placeholder="select osu!lazer directory"
            :is-icon-clickable="true"
            @icon-click="selectPath('lazer')"
            @focusout="handleManualSave"
            @keydown.enter="handleManualSave"
          >
            <template #icon>
              <FolderIcon />
            </template>
          </PawsInput>
        </div>

        <div class="input-row">
          <PawsInput
            v-model="configState.stablePath"
            v-paws-tooltip="'The folder where your osu! Stable is installed (contains osu!.db)'"
            button-text="stable path"
            placeholder="select osu!stable directory"
            :is-icon-clickable="true"
            @icon-click="selectPath('stable')"
            @focusout="handleManualSave"
            @keydown.enter="handleManualSave"
          >
            <template #icon>
              <FolderIcon />
            </template>
          </PawsInput>
        </div>

        <div class="input-row theme-row">
          <PawsDropdown
            v-paws-tooltip="'Choose the visual style of the application'"
            :options="themeState.availableThemes.map(t => t.id)"
            :model-value="themeState.activeThemeId"
            button-text="theme"
            placeholder="select theme"
            @update:model-value="handleThemeChange"
            class="flex-1"
          >
            <template #icon>
              <ThemeIcon />
            </template>
          </PawsDropdown>

          <PawsSubButton 
            v-paws-tooltip="'Import a custom .pawstheme package'"
            size="large"
            @click="handleImportTheme"
            class="import-btn"
          >
            <template #icon>
              <DownloadIcon />
            </template>
          </PawsSubButton>
        </div>
      </PawsCard>

      <!-- osu! Account Connection -->
      <PawsCard mode="titled" class="settings-card">
        <template #heading>
          <PawsHeading size="lg" font-weight="medium" align="left">osu! profile</PawsHeading>
        </template>

        <div class="profile-container">
          <div class="profile-info">
            <img :src="profileState.avatarUrl" class="profile-avatar" />
            <div class="profile-details">
              <span class="profile-username">{{ profileState.username }}</span>
              <span class="profile-status" :class="{ 'connected': profileState.isConnected }">
                {{ profileState.isConnected ? 'connected' : 'not connected' }}
              </span>
            </div>
          </div>
          <div class="profile-actions">
            <PawsSubButton 
              v-if="profileState.isConnected"
              v-paws-tooltip="'Refresh profile data'"
              size="medium"
              :disabled="isRefreshing"
              @click="handleRefreshProfile"
              class="refresh-btn"
            >
              <template #icon>
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" :class="{ 'spinning': isRefreshing }">
                  <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67"/>
                </svg>
              </template>
            </PawsSubButton>
            <PawsSubButton 
              v-if="!profileState.isConnected"
              v-paws-tooltip="'Link your osu! account via OAuth'"
              size="medium"
              :disabled="isConnecting"
              @click="handleConnectOsu"
              class="connect-btn"
              :text="isConnecting ? 'connecting...' : 'connect'"
            />
            <PawsSubButton 
              v-else
              v-paws-tooltip="'Disconnect your osu! account'"
              size="medium"
              @click="handleDisconnectOsu"
              class="connect-btn"
              text="disconnect"
            />
          </div>
        </div>
      </PawsCard>

      <!-- Advanced -->
      <PawsCard mode="titled" class="settings-card">
        <template #heading>
          <PawsHeading size="lg" font-weight="medium" align="left">advanced</PawsHeading>
        </template>

        <div class="input-row checkbox-row checkbox-group">
          <PawsCheckbox
            v-paws-tooltip="'Enable this to work with osu! Stable client data'"
            label="legacy mode"
            :model-value="configState.isLegacyMode"
            @update:model-value="handleToggleLegacy"
          />
          <PawsCheckbox
            v-paws-tooltip="'Allows you to double-click the Paws logo to toggle between Stable and Lazer mode'"
            label="switch on logo"
            :model-value="configState.isSwitchOnLogoEnabled"
            @update:model-value="handleToggleSwitchOnLogo"
          />
        </div>

        <div class="input-row checkbox-row checkbox-group">
          <PawsCheckbox
            v-paws-tooltip="'Automatically start Paws when you log into Windows'"
            label="launch on startup"
            :model-value="configState.isLaunchOnStartup"
            @update:model-value="handleToggleLaunchOnStartup"
          />
          <PawsCheckbox
            v-if="configState.isLaunchOnStartup"
            v-paws-tooltip="'Start Paws silently in the background (tray) without showing the main window'"
            label="invisible launch"
            :model-value="configState.isInvisibleLaunch"
            @update:model-value="handleToggleInvisibleLaunch"
          />
        </div>

        <div class="input-row checkbox-row checkbox-group">
          <PawsCheckbox
            v-paws-tooltip="'When you click the close button, hide Paws to the system tray instead of exiting entirely'"
            label="hide in tray on close"
            :model-value="configState.isHideInTrayOnClose"
            @update:model-value="handleToggleHideInTray"
          />
        </div>

        <div class="input-row checkbox-row">
          <PawsCheckbox
            v-paws-tooltip="'If checked, informative task descriptions will appear when you hover over UI elements'"
            label="show tips on hover"
            :model-value="configState.isShowTips"
            @update:model-value="handleToggleShowTips"
          />
        </div>
      </PawsCard>

      <!-- Developers (Hidden Default) -->
      <PawsCard v-if="configState.isDeveloperModeEnabled" mode="titled" class="settings-card">
        <template #heading>
          <PawsHeading size="lg" font-weight="medium" align="left">developer mode</PawsHeading>
        </template>
        
        <div class="input-row theme-row">
          <PawsInput
            v-model="configState.devPluginPath"
            v-paws-tooltip="'The folder where your compiled plugin DLL is saved (e.g. bin\\Debug\\net8.0)'"
            button-text="dev plugin path"
            placeholder="select hotplug directory"
            :is-icon-clickable="true"
            @icon-click="selectPath('devPlugin')"
            @focusout="handleManualSave"
            @keydown.enter="handleManualSave"
            class="flex-1"
          >
            <template #icon>
              <FolderIcon />
            </template>
          </PawsInput>

          <PawsSubButton 
            v-paws-tooltip="'Force reload plugin from current path'"
            size="large"
            @click="handleForceReloadDevPlugin"
            class="import-btn"
          >
            <template #icon>
              <DownloadIcon />
            </template>
          </PawsSubButton>
        </div>
      </PawsCard>

      <!-- About -->
      <PawsCard mode="titled" class="settings-card about-card">
        <template #heading>
          <PawsHeading size="lg" font-weight="medium" align="left">about</PawsHeading>
        </template>

        <div class="about-content">
          <div class="about-row">
            <span class="label">paws version:</span>
            <span class="value secret-version" @click="handleVersionClick">{{ appVersion }}</span>
          </div>
          <div class="about-row">
            <span class="label">lazer schema v:</span>
            <span class="value">{{ schemaVersion }}</span>
          </div>
          <div class="about-links">
            <a href="https://github.com/osupaws/paws" target="_blank" class="about-link">github</a>
            <span class="sep">•</span>
            <a href="https://t.me/osupaws" target="_blank" class="about-link">telegram</a>
            <span class="sep">•</span>
            <a href="https://osu.ppy.sh/teams/23155" target="_blank" class="about-link">osu! team</a>
          </div>
        </div>
      </PawsCard>
    </div>
  </PawsModal>
</template>

<style scoped>
.cards-container {
  display: flex;
  flex-direction: column;
  gap: 10px;
  box-sizing: border-box;
}

.settings-card {
  width: 100%;
  box-sizing: border-box;
}

.input-row {
  margin-bottom: 10px;
  display: flex;
  flex-direction: column;
  width: 100%;
  box-sizing: border-box;
}

.input-row:last-child {
  margin-bottom: 0;
}

.theme-row {
  display: flex;
  flex-direction: row;
  gap: 10px;
  align-items: center;
}

.theme-row .flex-1 {
  flex: 1;
}

.theme-row .import-btn {
  align-self: center;
}

/* Force dropdown to full width */
.input-row :deep(.paws-dropdown) {
  width: 100%;
}

.checkbox-row {
  align-items: flex-start;
}

.checkbox-group {
  flex-direction: row;
  align-items: center;
  gap: 20px;
}

.about-content {
  display: flex;
  flex-direction: column;
  gap: 5px;
  opacity: 0.8;
}

.about-row {
  display: flex;
  justify-content: space-between;
  font-size: 0.9rem;
}

.about-row .label {
  color: var(--paws-color-text-secondary);
}

.about-links {
  margin-top: 10px;
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 10px;
}

.about-link {
  color: var(--paws-color-accent-primary);
  text-decoration: none;
  font-size: 0.85rem;
  transition: opacity 0.2s ease;
}

.about-link:hover {
  opacity: 0.7;
  text-decoration: underline;
}

.sep {
  opacity: 0.3;
  font-size: 0.8rem;
  color: var(--paws-color-text-secondary);
}

.secret-version {
  user-select: none;
}

.full-width-btn {
  width: 100%;
}

.profile-container {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  gap: 12px;
  box-sizing: border-box;
  background-color: var(--paws-color-bg-subprimary);
  padding: 8px 12px;
  border-radius: var(--paws-rounding-medium);
}

.profile-info {
  display: flex;
  align-items: center;
  gap: 12px;
}

.profile-avatar {
  width: 40px;
  height: 40px;
  border-radius: var(--paws-rounding-medium-inner);
  object-fit: cover;
  flex-shrink: 0;
}

.profile-details {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.profile-username {
  font-family: "Fredoka", var(--paws-font-primary);
  font-size: 15px;
  font-weight: 500;
  color: var(--paws-color-text-primary);
  line-height: 1.2;
}

.profile-status {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--paws-color-text-secondary);
}

.profile-status::before {
  content: "";
  display: inline-block;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background-color: var(--paws-color-text-secondary);
  opacity: 0.5;
}

.profile-status.connected::before {
  background-color: var(--paws-color-success, #22c55e);
  opacity: 1;
}

.profile-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.connect-btn {
  height: 32px;
  padding: 0 12px !important;
  border-radius: var(--paws-rounding-medium-inner);
}

.refresh-btn {
  height: 32px;
  width: 32px;
  padding: 0 !important;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: var(--paws-rounding-medium-inner);
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.spinning {
  animation: spin 1s linear infinite;
}
</style>
