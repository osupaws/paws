<script setup lang="ts">
import { FolderIcon, PawsCard, PawsHeading, PawsInput } from "@osupaws/paws-ui";
import { closeSettings, modalState } from "@renderer/state/modal.state";
import { setLazerPath, setStablePath, settingsState } from "@renderer/state/settings.state";
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
</script>

<template>
	<Transition name="fade">
		<div v-if="modalState.isSettingsOpen" class="settings-overlay" @click.self="closeSettings">
			<Transition name="scale">
				<div v-if="modalState.isSettingsOpen" class="settings-modal">
					<div class="settings-container">
						<div class="header-row">
							<PawsHeading size="lg" font-weight="600" align="left">settings</PawsHeading>
							<button class="close-button" @click="closeSettings">✕</button>
						</div>

						<div class="cards-container">
							<PawsCard class="settings-card">
								<template #heading>
									<PawsHeading size="sm" font-weight="600" align="left">general</PawsHeading>
								</template>

								<div class="input-row">
									<PawsInput
										v-model="localLazerPath"
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
							</PawsCard>

							<PawsCard class="settings-card">
								<template #heading>
									<PawsHeading size="sm" font-weight="600" align="left">advanced</PawsHeading>
								</template>
								<!-- Advanced settings content will go here -->
							</PawsCard>
						</div>
					</div>
				</div>
			</Transition>
		</div>
	</Transition>
</template>

<style scoped>
.fade-enter-active,
.fade-leave-active {
	transition: opacity 0.25s ease;
}

.fade-enter-from,
.fade-leave-to {
	opacity: 0;
}

.scale-enter-active,
.scale-leave-active {
	transition:
		transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1),
		opacity 0.2s ease;
}

.scale-enter-from,
.scale-leave-to {
	transform: scale(0.9) translateY(10px);
	opacity: 0;
}

.settings-overlay {
	position: absolute;
	top: 0;
	left: 0;
	width: 100%;
	height: 100%;
	background-color: rgba(0, 0, 0, 0.65);
	display: flex;
	align-items: center;
	justify-content: center;
	z-index: 2000;
}

.settings-modal {
	width: 520px;
	height: 500px;
	background-color: var(--paws-color-bg-primary);
	border: 1px solid var(--paws-color-bg-tertiary);
	border-radius: var(--paws-rounding-big, 16px);
	box-shadow:
		0 25px 50px -12px rgba(0, 0, 0, 0.5),
		0 0 1px 1px rgba(255, 255, 255, 0.05) inset;
	overflow: hidden;
}

.settings-container {
	height: 100%;
	display: flex;
	flex-direction: column;
	padding: 4px 20px 20px 20px;
	box-sizing: border-box;
}

.header-row {
	display: flex;
	align-items: center;
	justify-content: space-between;
	/* No bottom border or explicit padding here, parent padding handles layout */
	min-height: 48px; /* Ensure sufficient height for alignment */
}

.close-button {
	background: none;
	border: none;
	color: var(--paws-color-text-secondary);
	font-size: 20px;
	cursor: pointer;
	padding: 4px;
	border-radius: 6px;
	transition: all 0.2s;
	display: flex;
	align-items: center;
	justify-content: center;
	/* Optional: Align close button nicely with the large heading */
}

.close-button:hover {
	background-color: var(--paws-color-interactive-primary);
	color: var(--paws-color-text-primary);
}

.cards-container {
	margin-top: 12px;
	display: flex;
	flex-direction: column;
	gap: 16px;
	flex: 1;
	overflow-y: auto;
}

.settings-card {
	width: 100%;
	box-sizing: border-box;
}

.input-row {
	margin-bottom: 16px;
}

.input-row:last-child {
	margin-bottom: 0;
}
</style>
