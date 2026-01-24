<script setup lang="ts">
import { closeSettings, modalState } from "@renderer/state/modal.state";
</script>

<template>
	<Transition name="fade">
		<div v-if="modalState.isSettingsOpen" class="settings-overlay" @click.self="closeSettings">
			<Transition name="scale">
				<div v-if="modalState.isSettingsOpen" class="settings-modal">
					<div class="settings-header">
						<h2 class="settings-title">Settings</h2>
						<button class="close-button" @click="closeSettings">✕</button>
					</div>
					<div class="settings-content">
						<!-- Content will go here in the next steps -->
						<p style="color: var(--paws-color-text-secondary)">
							Settings options will appear here.
						</p>
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
	position: fixed;
	top: 0;
	left: 0;
	width: 100vw;
	height: 100vh;
	background-color: rgba(0, 0, 0, 0.65);
	backdrop-filter: blur(8px);
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
	display: flex;
	flex-direction: column;
	overflow: hidden;
}

.settings-header {
	padding: 24px 28px;
	display: flex;
	align-items: center;
	justify-content: space-between;
	background: linear-gradient(to bottom, rgba(255, 255, 255, 0.02), transparent);
}

.settings-title {
	margin: 0;
	font-size: 22px;
	font-weight: 600;
	color: var(--paws-color-text-primary);
	letter-spacing: -0.01em;
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

.settings-content {
	flex: 1;
	padding: 24px;
	overflow-y: auto;
}
</style>
