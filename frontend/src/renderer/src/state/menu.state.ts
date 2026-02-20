import { reactive } from "vue";

import { closeAllModals } from "./modal.state";

export const menuState = reactive({
	isOpen: false
});

export function toggleMenu(): void {
	menuState.isOpen = !menuState.isOpen;
	if (menuState.isOpen) {
		closeAllModals();
	}
}

export function closeMenu(): void {
	menuState.isOpen = false;
}

export function openMenu(): void {
	menuState.isOpen = true;
	closeAllModals();
}
