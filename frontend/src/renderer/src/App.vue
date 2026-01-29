<script setup lang="ts">
import { PawsTooltip } from "@osupaws/paws-ui";
import Layout from "@renderer/components/Layout/Layout.vue";
import { getActiveThemeInfo, themeState } from "@renderer/state/theme.state";
import { updateThemeLinks } from "@renderer/utils/theme-manager";
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
