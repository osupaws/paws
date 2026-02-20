<script setup lang="ts">
import {
	CloseIcon,
	PawsCheckbox,
	PawsHeading,
	PawsModal,
	PawsSpoilerCard,
	PawsSubButton
} from "@osupaws/paws-ui";
import { closePlugins, modalState } from "@renderer/state/modal.state";
import { fetchAllPlugins, pluginState, togglePluginActive } from "@renderer/state/plugin.state";
import { sanitizeSvg } from "@renderer/utils/sanitizer";
import { onMounted } from "vue";

onMounted(() => {
	fetchAllPlugins();
});

const handleToggle = async (id: string, isActive: boolean): Promise<void> => {
	await togglePluginActive(id, isActive);
};
</script>

<template>
	<Transition name="fade">
		<div v-if="modalState.isPluginsOpen" class="modal-overlay" @click.self="closePlugins">
			<Transition name="scale">
				<PawsModal v-if="modalState.isPluginsOpen" @close="closePlugins">
					<template #heading>
						<PawsHeading size="lg" font-weight="medium" align="left">plugins</PawsHeading>
					</template>

					<template #actions>
						<PawsSubButton size="medium" @click="closePlugins">
							<template #icon>
								<CloseIcon />
							</template>
						</PawsSubButton>
					</template>

					<div class="cards-container">
						<div class="plugins-list">
							<PawsSpoilerCard v-for="plugin in pluginState.allInstalledPlugins" :key="plugin.id">
								<template #header>
									<div class="plugin-header-content">
										<div class="header-left">
											<PawsCheckbox
												:model-value="plugin.isActive"
												label="enable"
												@update:model-value="val => handleToggle(plugin.id, val)"
											/>
										</div>
										<div class="header-center">
											<div class="title-group">
												<!-- eslint-disable vue/no-v-html -->
												<div
													v-if="plugin.icon"
													class="plugin-icon"
													v-html="sanitizeSvg(plugin.icon)"
												></div>
												<!-- eslint-enable vue/no-v-html -->
												<span class="plugin-name">{{ plugin.name }}</span>
											</div>
										</div>
									</div>
								</template>

								<div class="plugin-details">
									<div class="detail-section">
										<span class="detail-label">description</span>
										<p class="detail-text">
											{{ plugin.description || "no description provided" }}
										</p>
									</div>

									<div class="detail-section">
										<span class="detail-label">details</span>
										<div class="meta-info">
											<div v-if="plugin.permissions?.length" class="meta-row">
												<span class="meta-label">permissions:</span>
												<span class="meta-values">{{ plugin.permissions.join(", ") }}</span>
											</div>
											<div v-if="plugin.provides?.length" class="meta-row">
												<span class="meta-label">provides:</span>
												<span class="meta-values">{{ plugin.provides.join(", ") }}</span>
											</div>
											<div v-if="plugin.consumes?.length" class="meta-row">
												<span class="meta-label">consumes:</span>
												<span class="meta-values">{{ plugin.consumes.join(", ") }}</span>
											</div>
										</div>
									</div>
								</div>
							</PawsSpoilerCard>

							<div v-if="pluginState.allInstalledPlugins.length === 0" class="empty-state">
								no plugins installed yet
							</div>
						</div>
					</div>
				</PawsModal>
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

.modal-overlay {
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

.cards-container {
	display: flex;
	flex-direction: column;
	gap: 10px;
}

.plugins-list {
	display: flex;
	flex-direction: column;
	gap: 8px;
}

.plugin-header-content {
	display: flex;
	align-items: center;
	width: 100%;
	height: 32px;
	position: relative; /* Context for absolute centering */
}

.header-left {
	display: flex;
	align-items: center;
}

.header-center {
	position: absolute;
	left: 0;
	top: 0;
	width: 100%;
	height: 100%;
	display: flex;
	align-items: center;
	justify-content: center;
	pointer-events: none;
}

.title-group {
	display: flex;
	align-items: center;
	gap: 8px;
	pointer-events: auto;
}

.plugin-icon {
	width: 24px;
	height: 24px;
	mask-size: contain;
	-webkit-mask-size: contain;
	mask-repeat: no-repeat;
	-webkit-mask-repeat: no-repeat;
	mask-position: center;
	-webkit-mask-position: center;
}

.plugin-name {
	font-size: 16px;
	font-weight: var(--paws-font-weight-medium);
	color: var(--paws-color-text-primary);
}

.plugin-details {
	padding: 12px 0;
	display: flex;
	flex-direction: column;
	gap: 8px; /* Расстояние между description и details блоками 8px */
}

.detail-section {
	display: flex;
	flex-direction: column;
	gap: 2px; /* Расстояние между заголовком и контентом 2px */
}

.detail-label {
	font-size: 16px; /* Размер шрифта заголовка 16px */
	font-weight: var(--paws-font-weight-medium); /* Вес заголовка medium */
	color: var(--paws-color-text-secondary);
	opacity: 0.7;
}

.detail-text {
	font-size: 14px; /* Размер контента 14px */
	font-weight: var(--paws-font-weight-light); /* Вес контента light */
	color: var(--paws-color-text-primary);
	margin: 0;
	line-height: 1.4;
}

.meta-info {
	display: flex;
	flex-direction: column;
	gap: 2px;
}

.meta-row {
	display: flex;
	gap: 6px;
	font-size: 14px; /* Контент мета-инфо тоже 14px */
	font-weight: var(--paws-font-weight-light); /* Вес light */
}

.meta-label {
	color: var(--paws-color-text-secondary);
}

.meta-values {
	color: var(--paws-color-text-primary);
}

.empty-state {
	padding: 20px;
	text-align: center;
	color: var(--paws-color-text-secondary);
	font-size: 13px;
	opacity: 0.5;
}
</style>
