<script setup lang="ts">
import { PawsCard, PawsCheckbox, PawsHeading, PawsSpoilerCard, PluginIcon } from "@osupaws/paws-ui";
import { closePlugins, modalState } from "@renderer/state/modal.state";
import { fetchAllPlugins, pluginState, togglePluginActive } from "@renderer/state/plugin.state";
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
		<div v-if="modalState.isPluginsOpen" class="plugins-overlay" @click.self="closePlugins">
			<Transition name="scale">
				<div v-if="modalState.isPluginsOpen" class="plugins-modal">
					<div class="plugins-container">
						<div class="header-row">
							<PawsHeading size="lg" font-weight="medium" align="left">plugins</PawsHeading>
							<button class="close-button" @click="closePlugins">✕</button>
						</div>

						<div class="cards-container">
							<!-- Installed Section -->
							<PawsCard class="plugins-section">
								<template #heading>
									<PawsHeading size="sm" font-weight="medium" align="left">installed</PawsHeading>
								</template>

								<div class="plugins-list">
									<PawsSpoilerCard
										v-for="plugin in pluginState.allInstalledPlugins"
										:key="plugin.id"
									>
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
														<PluginIcon class="plugin-type-icon" />
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
								</div>
							</PawsCard>

							<!-- Available Section -->
							<PawsCard class="plugins-section">
								<template #heading>
									<PawsHeading size="sm" font-weight="medium" align="left">available</PawsHeading>
								</template>
								<div class="empty-state">more plugins coming soon...</div>
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

.plugins-overlay {
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

.plugins-modal {
	width: 520px;
	height: 500px;
	background-color: var(--paws-color-bg-primary);
	border: 1px solid var(--paws-color-bg-tertiary);
	border-radius: var(--paws-rounding-big, 16px);
	box-shadow:
		0 25px 50px -12px rgba(0, 0, 0, 0.5),
		0 0 1px 1px rgba(255, 255, 255, 0.05) inset;
}

.plugins-container {
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
	min-height: 48px;
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
}

.close-button:hover {
	background-color: var(--paws-color-interactive-primary);
	color: var(--paws-color-text-primary);
}

.cards-container {
	margin-top: 10px;
	display: flex;
	flex-direction: column;
	gap: 10px;
	flex: 1;
	overflow-y: auto;
}

.plugins-section {
	width: 100%;
	box-sizing: border-box;
}

/*
   Logic check:
   Overlay/Modal: bg-primary
   PawsCard (section): bg-secondary (default for Card in this context)
   PawsSpoilerCard: bg-primary (main background)
*/
.plugins-list {
	display: flex;
	flex-direction: column;
	gap: 8px;
}

.plugins-list :deep([data-paws-ui="PawsSpoilerCard"]) {
	background-color: var(--paws-color-bg-primary) !important;
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

.plugin-type-icon {
	width: 16px;
	height: 16px;
	color: var(--paws-color-text-secondary);
}

.plugin-name {
	font-size: 14px;
	font-weight: 500;
	color: var(--paws-color-text-primary);
}

.plugin-details {
	padding: 12px 0;
	display: flex;
	flex-direction: column;
	gap: 12px;
}

.detail-section {
	display: flex;
	flex-direction: column;
	gap: 4px;
}

.detail-label {
	font-size: 12px;
	font-weight: 500;
	color: var(--paws-color-text-secondary);
	opacity: 0.7;
}

.detail-text {
	font-size: 13px;
	color: var(--paws-color-text-primary);
	margin: 0;
	line-height: 1.4;
}

.meta-info {
	display: flex;
	flex-direction: column;
	gap: 4px;
}

.meta-row {
	display: flex;
	gap: 6px;
	font-size: 12px;
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
