<script setup lang="ts">
import { configState } from "../state/config.state";
import { themeState } from "../state/theme.state";
import { openSettings } from "../state/modal.state";
</script>

<template>
  <div class="dashboard">
    <header class="view-header">
      <h1 class="view-title">Dashboard</h1>
      <div class="status-indicator">
        <span class="dot" :class="{ 'is-active': true }"></span>
        <span class="status-label">Engine Ready</span>
      </div>
    </header>

    <div class="dashboard-grid">
      <!-- Информация о режиме игры -->
      <section
        class="info-card mode-info"
        :class="{ 'legacy-mode': configState.isLegacyMode }"
      >
        <div class="card-icon">
          {{ configState.isLegacyMode ? "📦" : "💎" }}
        </div>
        <div class="card-content">
          <h3 class="card-label">Active Mode</h3>
          <p class="card-value">
            {{ configState.isLegacyMode ? "Stable Client" : "Lazer Client" }}
          </p>
        </div>
      </section>

      <!-- Путь к игре -->
      <section class="info-card path-info clickable" @click="openSettings">
        <div class="card-icon">📂</div>
        <div class="card-content">
          <h3 class="card-label">Game Directory</h3>
          <p class="card-value path-text" v-if="configState.isLegacyMode">
            {{ configState.stablePath || "Not set" }}
          </p>
          <p class="card-value path-text" v-else>
            {{ configState.lazerPath || "Not set" }}
          </p>
        </div>
        <button class="action-btn" @click.stop="openSettings">edit</button>
      </section>

      <!-- Быстрый доступ к темам -->
      <section class="info-card theme-info">
        <div class="card-icon">🎨</div>
        <div class="card-content">
          <h3 class="card-label">Theme</h3>
          <p class="card-value">{{ themeState.activeThemeId }}</p>
        </div>
      </section>
    </div>

    <!-- Заглушка для будущих фишек -->
    <div class="empty-state">
      <div class="placeholder-art">paws core</div>
      <p class="placeholder-hint">
        Choose an operation from the menu to start cleaning
      </p>
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
}

.view-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
}

.view-title {
  font-family: "Fredoka", var(--paws-font-primary);
  font-size: 32px;
  color: var(--paws-color-text-primary);
  line-height: 1;
}

.status-indicator {
  display: flex;
  align-items: center;
  gap: 8px;
  background: var(--paws-color-bg-subprimary);
  padding: 4px 10px;
  border-radius: 20px;
  border: 1px solid var(--paws-color-bg-tertiary);
}

.status-label {
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 1px;
  font-weight: var(--paws-font-weight-bold);
  color: var(--paws-color-text-secondary);
}

.dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--paws-color-bg-tertiary);
}

.dot.is-active {
  background: #4ade80;
  box-shadow: 0 0 8px #4ade80;
}

/* Сетка дашборда */
.dashboard-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 16px;
}

.info-card {
  background: var(--paws-color-bg-subprimary);
  border: 1px solid var(--paws-color-bg-tertiary);
  border-radius: var(--paws-rounding-normal);
  padding: 16px;
  display: flex;
  align-items: center;
  gap: 12px;
  position: relative;
  overflow: hidden;
  transition: all 0.2s;
}

.info-card.clickable {
  cursor: pointer;
}

.info-card.clickable:hover {
  background: var(--paws-color-bg-tertiary);
}

.card-icon {
  font-size: 24px;
  background: var(--paws-color-bg-tertiary);
  width: 44px;
  height: 44px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 12px;
}

.card-content {
  flex: 1;
}

.card-label {
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 1px;
  color: var(--paws-color-text-secondary);
  font-weight: var(--paws-font-weight-medium);
  margin-bottom: 2px;
}

.card-value {
  font-size: 16px;
  color: var(--paws-color-text-primary);
  font-weight: var(--paws-font-weight-bold);
}

.path-text {
  font-size: 13px;
  font-weight: var(--paws-font-weight-normal);
  color: var(--paws-color-text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 180px;
}

/* Акценты для режима */
.mode-info.legacy-mode {
  border-color: var(--paws-color-accent-primary);
  background: linear-gradient(
    135deg,
    var(--paws-color-bg-subprimary) 0%,
    var(--paws-color-accent-primary) 300%
  );
}

.action-btn {
  background: var(--paws-color-interactive-primary);
  border: none;
  font-size: 10px;
  padding: 4px 8px;
  border-radius: 4px;
  color: #fff;
  font-weight: var(--paws-font-weight-bold);
  cursor: pointer;
  text-transform: uppercase;
}

/* Empty State */
.empty-state {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  opacity: 0.15;
  user-select: none;
}

.placeholder-art {
  font-family: "Fredoka", sans-serif;
  font-size: 80px;
  font-weight: 900;
  letter-spacing: -4px;
}

.placeholder-hint {
  font-size: 14px;
  font-weight: var(--paws-font-weight-light);
}
</style>
