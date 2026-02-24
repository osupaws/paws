<script setup lang="ts">
import AppMenu from "@renderer/components/Menu/AppMenu.vue";
import PluginsModal from "@renderer/components/PluginsModal/PluginsModal.vue";
import Content from "@renderer/components/regions/Content/Content.vue";
import Titlebar from "@renderer/components/regions/Titlebar/Titlebar.vue";
import SettingsModal from "@renderer/components/SettingsModal/SettingsModal.vue";
import { onMounted, ref } from "vue";

import styles from "./Layout.module.css";

const isMounted = ref(false);

onMounted(() => {
	isMounted.value = true;
});
</script>

<template>
	<div :class="styles.layout">
		<Titlebar />
		<div id="main-container" :class="styles.mainContainer">
			<AppMenu />
			<Content />
		</div>
		<SettingsModal v-if="isMounted" />
		<PluginsModal v-if="isMounted" />
	</div>
</template>
