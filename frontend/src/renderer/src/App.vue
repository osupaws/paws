<script setup lang="ts">
import { PawsTooltip } from "@osupaws/paws-ui";
import Layout from "@renderer/components/Layout/Layout.vue";
import { closeMenu, menuState } from "@renderer/state/menu.state";
import { closeAllModals, modalState } from "@renderer/state/modal.state";
import { getActiveThemeInfo, themeState } from "@renderer/state/theme.state";
import { updateThemeLinks } from "@renderer/utils/theme-manager";
import { onMounted, onUnmounted, watch } from "vue";

// Global keyboard handlers
const handleKeyDown = (e: KeyboardEvent): void => {
	if (e.key === "Escape") {
		const hadModalsOpen = modalState.isSettingsOpen || modalState.isPluginsOpen;
		if (hadModalsOpen) {
			closeAllModals();
		} else if (menuState.isOpen) {
			closeMenu();
		}
	}
};

onMounted(() => {
	window.addEventListener("keydown", handleKeyDown);
});

onUnmounted(() => {
	window.removeEventListener("keydown", handleKeyDown);
});

// This watcher handles all side effects of a theme change.
watch(
	() => themeState.activeThemeId,
	newThemeId => {
		if (!newThemeId) return;

		// 1. Save to store
		window.api.backend.post("/api/settings", { key: "activeThemeId", value: newThemeId });

		// 2. Update the stylesheet <link>s in the DOM
		const themeInfo = getActiveThemeInfo();
		const customHash = themeInfo.isCustom && themeInfo.file ? themeInfo.file.hash : undefined;

		updateThemeLinks(themeInfo.base, customHash);
	},
	{ immediate: true }
);
</script>

<template>
	<div class="app-container">
		<Layout />
		<PawsTooltip />
	</div>
</template>

<style scoped>
.app-container {
	width: 100vw;
	height: 100vh;
	overflow: hidden;
}
</style>
