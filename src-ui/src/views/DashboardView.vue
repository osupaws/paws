<script setup lang="ts">
import { onMounted } from "vue";
import { configState, toggleLegacyMode, saveConfig } from "../state/config.state";
import { themeState, applyTheme } from "../state/theme.state";
import { openSettings } from "../state/modal.state";
import { profileState } from "../state/profile.state";
import { pluginState, fetchPlugins } from "../state/plugin.state";
import { openPlugins } from "../state/modal.state";
import { PawsCard } from "@osupaws/paws-ui";

const cycleTheme = async () => {
  const themes = themeState.availableThemes;
  if (!themes || themes.length <= 1) return;
  const currentIndex = themes.findIndex(t => t.id === themeState.activeThemeId);
  const nextIndex = (currentIndex + 1) % themes.length;
  const nextTheme = themes[nextIndex].id;
  
  themeState.activeThemeId = nextTheme;
  configState.currentThemeId = nextTheme;
  applyTheme(nextTheme, configState.isShowTips);
  await saveConfig();
};

onMounted(() => {
  fetchPlugins();
});
</script>

<template>
  <div class="dashboard">
    <header class="view-header">
      <h1 class="view-title">
        welcome back, <span class="username">{{ profileState.username }}</span>
      </h1>
    </header>

    <div class="dashboard-grid">
      <!-- Active Mode Card (Clickable to Toggle) -->
      <PawsCard
        class="interactive-card mode-card"
        :class="{ 'lazer-active': !configState.isLegacyMode }"
        @click="toggleLegacyMode"
      >
        <div class="card-layout">
          <div class="card-icon">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 24 24"
              width="20"
              height="20"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <rect x="2" y="6" width="20" height="12" rx="3" />
              <path d="M6 12h4m-2-2v4m7-2h.01m2.99-2h.01" />
            </svg>
          </div>
          <div class="card-content">
            <h3 class="card-label">Active Mode</h3>
            <p class="card-value">
              {{ configState.isLegacyMode ? "Stable Client" : "Lazer Client" }}
            </p>
          </div>
        </div>
      </PawsCard>

      <!-- App Theme Card (Clickable to Cycle Theme) -->
      <PawsCard class="interactive-card theme-card" @click="cycleTheme">
        <div class="card-layout">
          <div class="card-icon">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 24 24"
              width="20"
              height="20"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path
                d="M12 22C17.5228 22 22 17.5228 22 12C22 6.47715 17.5228 2 12 2C6.47715 2 2 6.47715 2 12C2 14.7255 3.09032 17.1962 4.85857 19C5.03455 19.176 5.06155 19.458 4.97557 19.689C4.69324 20.449 4.14801 21.082 3.42857 21.439C3.12513 21.589 3.06455 21.966 3.32243 22.146C4.46467 22.946 5.86467 23 7 23C7.61867 23 8.21867 22.753 8.65333 22.318C9.5 21.471 10.5 22 12 22Z"
              />
              <circle cx="7.5" cy="10.5" r="1" fill="currentColor" />
              <circle cx="11.5" cy="7.5" r="1" fill="currentColor" />
              <circle cx="16.5" cy="9.5" r="1" fill="currentColor" />
            </svg>
          </div>
          <div class="card-content">
            <h3 class="card-label">App Theme</h3>
            <p class="card-value">{{ themeState.activeThemeId }}</p>
          </div>
        </div>
      </PawsCard>

      <!-- Active Plugins Card (Clickable to Open Plugins List) -->
      <PawsCard class="interactive-card plugins-card" @click="openPlugins">
        <div class="card-layout">
          <div class="card-icon">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 24 24"
              width="20"
              height="20"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
            </svg>
          </div>
          <div class="card-content">
            <h3 class="card-label">Active Plugins</h3>
            <p class="card-value">
              {{ pluginState.loadedPlugins.length }} Running
            </p>
          </div>
        </div>
      </PawsCard>
    </div>

    <!-- Minimalist Welcome Block -->
    <div class="welcome-section">
      <div class="welcome-logo">paws</div>
      <p class="welcome-text">what are you staring at?</p>
    </div>
  </div>
</template>

<style scoped>
.dashboard {
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 24px;
  height: 100%;
  box-sizing: border-box;
}

.view-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.view-title {
  font-family: "Fredoka", var(--paws-font-primary);
  font-size: 28px;
  color: var(--paws-color-text-secondary);
  line-height: 1.2;
}

.view-title .username {
  color: var(--paws-color-text-primary);
}

.dashboard-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 12px;
}

.interactive-card {
  cursor: pointer;
  transition:
    border-color 0.2s ease,
    background-color 0.2s ease;
  user-select: none;
}

.interactive-card:hover {
  border-color: var(--paws-color-accent-primary) !important;
}

.card-layout {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
}

.card-icon {
  width: 36px;
  height: 36px;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: var(--paws-color-bg-tertiary);
  border-radius: var(--paws-rounding-medium-inner);
  color: var(--paws-color-text-secondary);
  flex-shrink: 0;
  transition:
    background-color 0.2s ease,
    color 0.2s ease;
}

.interactive-card:hover .card-icon {
  background-color: var(--paws-color-accent-primary);
  color: var(--paws-color-accent-secondary);
}

.card-content {
  flex: 1;
  min-width: 0;
}

.card-label {
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--paws-color-text-secondary);
  font-weight: 500;
  margin-bottom: 2px;
}

.card-value {
  font-family: var(--paws-font-primary);
  font-size: 15px;
  color: var(--paws-color-text-primary);
  font-weight: 600;
}

/* Lazer active mode accent styling */
.mode-card.lazer-active {
  border-color: var(--paws-color-accent-primary) !important;
}

/* Welcome Center Section */
.welcome-section {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  user-select: none;
  opacity: 0.15;
  transition: opacity 0.3s ease;
  margin-top: 40px;
}

.welcome-section:hover {
  opacity: 0.3;
}

.welcome-logo {
  font-family: "Fredoka", var(--paws-font-primary);
  font-size: 72px;
  font-weight: 900;
  letter-spacing: -3px;
  color: var(--paws-color-text-primary);
  margin-bottom: 12px;
}

.welcome-text {
  font-size: 14px;
  color: var(--paws-color-text-secondary);
  max-width: 320px;
}
</style>
