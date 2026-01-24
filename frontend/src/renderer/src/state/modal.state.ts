import { reactive } from "vue";

export const modalState = reactive({
	isSettingsOpen: false
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
