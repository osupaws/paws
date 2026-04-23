<script setup lang="ts">
import { onMounted } from "vue";
import { updateThemeLinks } from "../utils/theme-manager";

// Простая функция для демонстрации переключения на дефолтную тему при загрузке
onMounted(() => {
  updateThemeLinks({
    id: "paws-dark",
    name: "Dark (Default)",
  });
});
</script>

<template>
  <div class="placeholder">
    <div class="status-badge">system ready</div>
    <p class="status-text">Router connected.</p>
  </div>
</template>

<style scoped>
.placeholder {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  height: 100%;
}

.status-badge {
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 1px;
  background: var(--paws-color-bg-tertiary);
  padding: 4px 8px;
  border-radius: 4px;
  color: var(--paws-color-accent-primary);
  font-weight: var(--paws-font-weight-bold);
}

.status-text {
  font-size: 14px;
  color: var(--paws-color-text-secondary);
  font-weight: var(--paws-font-weight-light);
}
</style>
