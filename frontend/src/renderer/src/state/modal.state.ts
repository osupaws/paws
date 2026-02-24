import { reactive } from "vue";

export const modalState = reactive({
	isSettingsOpen: false,
	isPluginsOpen: false
});

export function openSettings(): void {
	modalState.isSettingsOpen = true;
}

export function closeSettings(): void {
	modalState.isSettingsOpen = false;
}

export function toggleSettings(): void {
	modalState.isSettingsOpen = !modalState.isSettingsOpen;
}

export function openPlugins(): void {
	modalState.isPluginsOpen = true;
}

export function closePlugins(): void {
	modalState.isPluginsOpen = false;
}

export function togglePlugins(): void {
	modalState.isPluginsOpen = !modalState.isPluginsOpen;
}

export function closeAllModals(): void {
	modalState.isSettingsOpen = false;
	modalState.isPluginsOpen = false;
}
