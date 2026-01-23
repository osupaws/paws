<script setup lang="ts">
import AppMenu from "@renderer/components/Menu/AppMenu.vue";
import Content from "@renderer/components/regions/Content/Content.vue";
import Titlebar from "@renderer/components/regions/Titlebar/Titlebar.vue";

import styles from "./Layout.module.css";
</script>

<template>
	<div :class="styles.layout">
		<Titlebar />
		<AppMenu />
		<Content />
	</div>
</template>
