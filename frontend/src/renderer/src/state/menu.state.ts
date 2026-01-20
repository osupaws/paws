import { reactive } from "vue";

export const menuState = reactive({
  isOpen: false,
});

export function toggleMenu(): void {
  menuState.isOpen = !menuState.isOpen;
}

export function closeMenu(): void {
  menuState.isOpen = false;
}

export function openMenu(): void {
  menuState.isOpen = true;
}
