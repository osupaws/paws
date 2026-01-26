<script setup lang="ts">
import {
	DarkModeIcon,
	LightModeIcon,
	PawsMenuButton,
	PawsMultiSwitch,
	PluginIcon,
	SettingsIcon
} from "@osupaws/paws-ui";
import { closeMenu, menuState } from "@renderer/state/menu.state";
import { openSettings } from "@renderer/state/modal.state";
import { pluginState, setActivePlugin } from "@renderer/state/plugin.state";
import { setActiveTheme, themeState } from "@renderer/state/theme.state";
import { computed } from "vue";

import styles from "./AppMenu.module.css";

const selectPlugin = (id: string): void => {
	setActivePlugin(id);
	closeMenu();
};

const themeModel = computed({
	get: () => (themeState.activeThemeId?.includes("dark") ? "dark" : "light"),
	set: (val: string) => {
		if (val === "dark") setActiveTheme("paws-dark");
		else setActiveTheme("paws-light");
	}
});

const handleOpenSettings = (): void => {
	openSettings();
	closeMenu();
};
</script>

<template>
	<Transition name="menu">
		<div v-if="menuState.isOpen" :class="styles.menuWrapper" @click.self="closeMenu">
			<div :class="styles.menu">
				<!-- Section: Client Launcher Placeholder -->
				<div :class="[styles.section, styles.paddedSection]">
					<PawsMenuButton label="launch akatsuki">launch akatsuki</PawsMenuButton>
				</div>

				<!-- Section: Plugins -->
				<div :class="styles.section">
					<div :class="styles.pluginList">
						<PawsMenuButton
							v-for="plugin in pluginState.loadedPlugins"
							:key="plugin.id"
							:label="plugin.name"
							:active="pluginState.activePluginId === plugin.id"
							@click="selectPlugin(plugin.id)"
						>
							{{ plugin.name }}
						</PawsMenuButton>

						<div v-if="pluginState.loadedPlugins.length === 0" :class="styles.pluginItem">
							No plugins loaded
						</div>
					</div>
				</div>

				<!-- Footer: Settings, Theme, etc. -->
				<div :class="styles.footer">
					<PawsMenuButton
						:class="styles.footerButton"
						label="Plugins"
						tooltip="Plugins"
						@click="() => {}"
					>
						<template #icon>
							<PluginIcon />
						</template>
					</PawsMenuButton>

					<PawsMultiSwitch v-model="themeModel" :options="['light', 'dark']">
						<template #light>
							<LightModeIcon />
						</template>
						<template #dark>
							<DarkModeIcon />
						</template>
					</PawsMultiSwitch>

					<PawsMenuButton
						:class="styles.footerButton"
						label="Settings"
						tooltip="Settings"
						@click="handleOpenSettings"
					>
						<template #icon>
							<SettingsIcon />
						</template>
					</PawsMenuButton>
				</div>
			</div>
		</div>
	</Transition>
</template>
