<script setup lang="ts">
import {
	CloseIcon,
	FolderIcon,
	PawsCard,
	PawsCheckbox,
	PawsDropdown,
	PawsHeading,
	PawsInput,
	PawsModal,
	PawsSubButton,
	ThemeIcon
} from "@osupaws/paws-ui";
import { closeSettings, modalState } from "@renderer/state/modal.state";
import {
	setLazerPath,
	setLegacyMode,
	setShowTips,
	setStablePath,
	setSwitchOnLogoEnabled,
	settingsState
} from "@renderer/state/settings.state";
import { setActiveTheme, themeState } from "@renderer/state/theme.state";
import { ref, watch } from "vue";

// Local state for inputs to prevent saving on every keystroke
const localLazerPath = ref(settingsState.lazerPath || "");
const localStablePath = ref(settingsState.stablePath || "");

// Sync local state when settings are loaded/changed elsewhere
watch(
	() => settingsState.lazerPath,
	val => (localLazerPath.value = val || "")
);
watch(
	() => settingsState.stablePath,
	val => (localStablePath.value = val || "")
);

const handleLazerSave = (): void => {
	if (localLazerPath.value !== settingsState.lazerPath) {
		setLazerPath(localLazerPath.value);
	}
};

const handleStableSave = (): void => {
	if (localStablePath.value !== settingsState.stablePath) {
		setStablePath(localStablePath.value);
	}
};

const handleLazerCancel = (): void => {
	localLazerPath.value = settingsState.lazerPath || "";
};

const handleStableCancel = (): void => {
	localStablePath.value = settingsState.stablePath || "";
};

const openFolderDialog = async (type: "stable" | "lazer"): Promise<void> => {
	const result = await window.api.electron.showOpenDialog({
		properties: ["openDirectory"]
	});

	if (!result.canceled && result.filePaths.length > 0) {
		const path = result.filePaths[0];
		if (type === "stable") {
			localStablePath.value = path;
			handleStableSave();
		} else {
			localLazerPath.value = path;
			handleLazerSave();
		}
	}
};

const handleThemeChange = (newThemeId: string): void => {
	setActiveTheme(newThemeId);
};

const appVersion = ref("...");
const schemaVersion = ref("...");

window.api.updater.getVersion().then(v => {
	appVersion.value = v.app;
	schemaVersion.value = v.schema;
});
</script>

<template>
	<PawsModal
		:is-open="modalState.isSettingsOpen"
		teleport-to="#main-container"
		@close="closeSettings"
	>
		<template #heading>
			<PawsHeading size="lg" font-weight="medium" align="left">settings</PawsHeading>
		</template>

		<template #actions>
			<PawsSubButton size="medium" text="close" @click="closeSettings">
				<template #icon>
					<CloseIcon />
				</template>
			</PawsSubButton>
		</template>

		<div class="cards-container">
			<PawsCard mode="titled" class="settings-card">
				<template #heading>
					<PawsHeading size="lg" font-weight="medium" align="left">general</PawsHeading>
				</template>

				<div class="input-row">
					<PawsInput
						v-model="localLazerPath"
						v-paws-tooltip="'select the location of your osu!lazer installation'"
						button-text="lazer path"
						placeholder="select osu!lazer directory"
						:is-icon-clickable="true"
						@focusout="handleLazerSave"
						@keydown.enter="handleLazerSave"
						@keydown.esc="handleLazerCancel"
						@icon-click="openFolderDialog('lazer')"
					>
						<template #icon>
							<FolderIcon />
						</template>
					</PawsInput>
				</div>

				<div class="input-row">
					<PawsInput
						v-model="localStablePath"
						v-paws-tooltip="'select the location of your osu!stable installation'"
						button-text="stable path"
						placeholder="select osu!stable directory"
						:is-icon-clickable="true"
						@focusout="handleStableSave"
						@keydown.enter="handleStableSave"
						@keydown.esc="handleStableCancel"
						@icon-click="openFolderDialog('stable')"
					>
						<template #icon>
							<FolderIcon />
						</template>
					</PawsInput>
				</div>

				<div class="input-row">
					<PawsDropdown
						v-paws-tooltip="'choose the visual theme of the application'"
						:options="themeState.availableThemes.map(t => t.id)"
						:model-value="themeState.activeThemeId"
						button-text="theme"
						placeholder="select theme"
						@update:model-value="handleThemeChange"
					>
						<template #icon>
							<ThemeIcon />
						</template>
					</PawsDropdown>
				</div>
			</PawsCard>

			<PawsCard mode="titled" class="settings-card">
				<template #heading>
					<PawsHeading size="lg" font-weight="medium" align="left">advanced</PawsHeading>
				</template>

				<div class="input-row checkbox-row checkbox-group">
					<PawsCheckbox
						v-paws-tooltip="'enables integration with osu!stable instead of osu!lazer'"
						label="legacy mode"
						:model-value="settingsState.isLegacyMode"
						@update:model-value="setLegacyMode"
					/>
					<PawsCheckbox
						v-paws-tooltip="'double-click the titlebar logo to toggle legacy mode'"
						label="switch on logo"
						:model-value="settingsState.isSwitchOnLogoEnabled"
						@update:model-value="setSwitchOnLogoEnabled"
					/>
				</div>

				<div class="input-row checkbox-row">
					<PawsCheckbox
						v-paws-tooltip="'show helpful tooltips when hovering over interface elements'"
						label="show tips on hover"
						:model-value="settingsState.isShowTips"
						@update:model-value="setShowTips"
					/>
				</div>
			</PawsCard>

			<PawsCard mode="titled" class="settings-card about-card">
				<template #heading>
					<PawsHeading size="lg" font-weight="medium" align="left">about</PawsHeading>
				</template>

				<div class="about-content">
					<div class="about-row">
						<span class="label">paws version:</span>
						<span class="value">{{ appVersion }}</span>
					</div>
					<div class="about-row">
						<span class="label">lazer schema v:</span>
						<span class="value">{{ schemaVersion || "..." }}</span>
					</div>
					<div class="about-links">
						<a href="https://github.com/osupaws/paws" target="_blank" class="about-link">
							github
						</a>
						<span class="sep">•</span>
						<a href="https://t.me/osupaws" target="_blank" class="about-link">telegram</a>
						<span class="sep">•</span>
						<a
							v-paws-tooltip="'paws clan on osu!'"
							href="https://osu.ppy.sh/teams/23155"
							target="_blank"
							class="about-link"
						>
							osu! team
						</a>
					</div>
				</div>
			</PawsCard>
		</div>
	</PawsModal>
</template>

<style scoped>
.cards-container {
	display: flex;
	flex-direction: column;
	gap: 10px;
	box-sizing: border-box;
}

.settings-card {
	width: 100%;
	box-sizing: border-box;
}

.input-row {
	margin-bottom: 10px;
	display: flex;
	flex-direction: column;
	width: 100%;
	box-sizing: border-box;
}

.input-row:last-child {
	margin-bottom: 0;
}

/* Force dropdown to full width */
.input-row :deep(.paws-dropdown) {
	width: 100%;
}

.checkbox-row {
	align-items: flex-start;
}

.checkbox-group {
	flex-direction: row;
	align-items: center;
	gap: 20px;
}

.input-row :deep(input) {
	text-transform: lowercase;
}

.about-content {
	display: flex;
	flex-direction: column;
	gap: 5px;
	opacity: 0.8;
}

.about-row {
	display: flex;
	justify-content: space-between;
	font-size: 0.9rem;
}

.about-row .label {
	color: var(--paws-text-dim, #888);
}

.about-links {
	margin-top: 10px;
	display: flex;
	justify-content: center;
	align-items: center;
	gap: 10px;
}

.about-link {
	color: var(--paws-primary, #ff66aa);
	text-decoration: none;
	font-size: 0.85rem;
	transition: opacity 0.2s ease;
}

.about-link:hover {
	opacity: 0.7;
	text-decoration: underline;
}

.sep {
	opacity: 0.3;
	font-size: 0.8rem;
}
</style>
