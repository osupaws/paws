<script setup lang="ts">
import AppMenu from "@renderer/components/Menu/AppMenu.vue";
import Content from "@renderer/components/regions/Content/Content.vue";
import Titlebar from "@renderer/components/regions/Titlebar/Titlebar.vue";
import SettingsModal from "@renderer/components/SettingsModal/SettingsModal.vue";

import styles from "./Layout.module.css";
</script>

<template>
	<div :class="styles.layout">
		<Titlebar />
		<div :class="styles.mainContainer">
			<AppMenu />
			<Content />
			<SettingsModal />
		</div>
	</div>
</template>
