<script setup lang="ts">
import { menuState, toggleMenu } from "@renderer/state/menu.state";
import { setLegacyMode, settingsState } from "@renderer/state/settings.state";

import styles from "./Titlebar.module.css";

// Методы для вызова IPC каналов
const minimizeWindow = (): void => {
	window.api.electron.minimizeWindow();
};

const closeWindow = (): void => {
	window.api.electron.closeApp();
};

const handleLogoDblClick = async (): Promise<void> => {
	if (settingsState.isSwitchOnLogoEnabled) {
		await setLegacyMode(!settingsState.isLegacyMode);
	}
};
</script>

<template>
	<div :class="styles.titlebar">
		<!-- Левая секция: Кнопка пользователя -->
		<div :class="styles.left">
			<button
				:class="[styles.userButton, { [styles.active]: menuState.isOpen }]"
				@click="toggleMenu"
			>
				<!-- Временно просто текст -->
				<span>menu</span>
			</button>
		</div>

		<!-- Центральная секция: Логотип -->
		<div :class="styles.center">
			<div
				:class="[
					styles.logoContainer,
					{ [styles.interactive]: settingsState.isSwitchOnLogoEnabled }
				]"
				@dblclick="handleLogoDblClick"
			>
				<Transition name="legacy">
					<span v-if="settingsState.isLegacyMode" :class="styles.legacyText">legacy</span>
				</Transition>
				<span :class="[styles.logo, { [styles.shifted]: settingsState.isLegacyMode }]">paws</span>
			</div>
		</div>

		<!-- Правая секция: Кнопки управления окна -->
		<div :class="styles.right">
			<button :class="styles.windowButton" @click="minimizeWindow">—</button>
			<button :class="[styles.windowButton, styles.closeButton]" @click="closeWindow">✕</button>
		</div>
	</div>
</template>

<style>
/* Global transition classes for name="legacy" */
.legacy-enter-active,
.legacy-leave-active {
	transition: all 0.4s cubic-bezier(0.34, 1.35, 0.64, 1);
}

.legacy-enter-from,
.legacy-leave-to {
	opacity: 0;
	transform: translate(-50%, -10px);
}
</style>
