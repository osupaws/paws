<script setup lang="ts">
import {
  CloseIcon,
  PawsCheckbox,
  PawsHeading,
  PawsModal,
  PawsSpoilerCard,
  PawsSubButton
} from "@osupaws/paws-ui";
import { closePlugins, modalState } from "../state/modal.state";
import { fetchAllPlugins, pluginState, togglePluginActive } from "../state/plugin.state";
import { onMounted } from "vue";

// Sanitization function for SVG icons from backend
const sanitizeSvg = (svg: string) => {
  // A simple placeholder since we don't have the full sanitizer imported here yet.
  // In a real implementation you would use DOMPurify or similar.
  if (!svg || typeof svg !== 'string') return '';
  if (svg.includes('<script')) return '';
  return svg;
};

onMounted(() => {
  fetchAllPlugins();
});

const handleToggle = async (id: string, isActive: boolean) => {
  await togglePluginActive(id, isActive);
};
</script>

<template>
  <PawsModal
    :is-open="modalState.isPluginsOpen"
    teleport-to="#paws-modal-root"
    @close="closePlugins"
  >
    <template #heading>
      <PawsHeading size="lg" font-weight="medium" align="left">plugins</PawsHeading>
    </template>

    <template #actions>
      <PawsSubButton size="medium" text="close" @click="closePlugins">
        <template #icon>
          <CloseIcon />
        </template>
      </PawsSubButton>
    </template>

    <div class="cards-container">
      <div class="plugins-list">
        <PawsSpoilerCard v-for="plugin in pluginState.allInstalledPlugins" :key="plugin.id">
          <template #header>
            <div class="plugin-header-content">
              <div class="header-left">
                <PawsCheckbox
                  :model-value="plugin.isActive"
                  label="enable"
                  @update:model-value="val => handleToggle(plugin.id, val)"
                />
              </div>
              <div class="header-center">
                <div class="title-group">
                  <!-- eslint-disable vue/no-v-html -->
                  <div
                    v-if="plugin.iconData"
                    class="plugin-icon"
                    v-html="sanitizeSvg(plugin.iconData)"
                  ></div>
                  <!-- eslint-enable vue/no-v-html -->
                  <span class="plugin-name">{{ plugin.name }}</span>
                </div>
              </div>
            </div>
          </template>

          <div class="plugin-details">
            <div class="detail-section">
              <span class="detail-label">description</span>
              <p class="detail-text">
                {{ plugin.description || "no description provided" }}
              </p>
            </div>

            <div class="detail-section">
              <span class="detail-label">details</span>
              <div class="meta-info">
                <div v-if="plugin.permissions?.length" class="meta-row">
                  <span class="meta-label">permissions:</span>
                  <span class="meta-values">{{ plugin.permissions.join(", ") }}</span>
                </div>
                <div v-if="plugin.provides?.length" class="meta-row">
                  <span class="meta-label">provides:</span>
                  <span class="meta-values">{{ plugin.provides.join(", ") }}</span>
                </div>
                <div v-if="plugin.consumes?.length" class="meta-row">
                  <span class="meta-label">consumes:</span>
                  <span class="meta-values">{{ plugin.consumes.join(", ") }}</span>
                </div>
              </div>
            </div>
          </div>
        </PawsSpoilerCard>

        <div v-if="pluginState.allInstalledPlugins.length === 0" class="empty-state">
          no plugins installed yet
        </div>
      </div>
    </div>
  </PawsModal>
</template>

<style scoped>
.cards-container {
  display: flex;
  flex-direction: column;
}

.plugins-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.plugin-header-content {
  display: flex;
  align-items: center;
  width: 100%;
  height: 32px;
  position: relative; /* Context for absolute centering */
}

.header-left {
  display: flex;
  align-items: center;
}

.header-center {
  position: absolute;
  left: 0;
  top: 0;
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  pointer-events: none;
}

.title-group {
  display: flex;
  align-items: center;
  gap: 8px;
  pointer-events: auto;
}

.plugin-icon {
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--paws-color-text-primary);
}

.plugin-icon :deep(svg) {
  width: 100%;
  height: 100%;
}

.plugin-name {
  font-size: 16px;
  font-weight: var(--paws-font-weight-medium);
  color: var(--paws-color-text-primary);
}

.plugin-details {
  padding: 12px 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.detail-section {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.detail-label {
  font-size: 16px;
  font-weight: var(--paws-font-weight-medium);
  color: var(--paws-color-text-secondary);
  opacity: 0.7;
}

.detail-text {
  font-size: 14px;
  font-weight: var(--paws-font-weight-light);
  color: var(--paws-color-text-primary);
  margin: 0;
  line-height: 1.4;
}

.meta-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.meta-row {
  display: flex;
  gap: 6px;
  font-size: 14px;
  font-weight: var(--paws-font-weight-light);
}

.meta-label {
  color: var(--paws-color-text-secondary);
}

.meta-values {
  color: var(--paws-color-text-primary);
}

.empty-state {
  padding: 20px;
  text-align: center;
  color: var(--paws-color-text-secondary);
  font-size: 13px;
  opacity: 0.5;
}
</style>
