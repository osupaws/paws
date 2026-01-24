<script setup lang="ts">
import Layout from "@renderer/components/Layout/Layout.vue";
import { getActiveThemeInfo, themeState } from "@renderer/state/theme.state";
import { watch } from "vue";
// This watcher handles all side effects of a theme change.
watch(
	() => themeState.activeThemeId,
	newThemeId => {
		if (!newThemeId) return;

		// 1. Save to store
		window.api.store.set("activeThemeId", newThemeId);

		// 2. Update the stylesheet <link>s in the DOM
		const themeInfo = getActiveThemeInfo();
		// Base theme ID will now be like 'paws-dark', so 'dark' will be 'themeInfo.base'
		const baseThemeId = `paws-${themeInfo.base}`;
		const baseThemeInfo = themeState.availableThemes.find(t => t.id === baseThemeId);

		const baseLink = document.getElementById("app-theme-base-link") as HTMLLinkElement;
		const customLink = document.getElementById("app-theme-custom-link") as HTMLLinkElement;

		if (baseLink && baseThemeInfo) {
			// Core themes are always served from the app's internal assets
			// The file path is hardcoded in `theme.state.ts`
			baseLink.href = `paws-app://themes/${themeInfo.base}.css`;
		}

		if (customLink && themeInfo.isCustom && themeInfo.file) {
			// Custom themes are served by hash via the C# backend
			customLink.href = `paws-theme://${themeInfo.file.hash}`;
		} else {
			// It's a core theme, so no custom styles are needed
			customLink.href = "";
		}
	},
	{ immediate: true }
);
</script>

<template>
	<div class="app-container">
		<Layout />
	</div>
</template>

<style scoped>
.app-container {
	width: 100vw;
	height: 100vh;
	overflow: hidden;
}
</style>
