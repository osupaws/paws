import { reactive, onMounted, onUnmounted } from "vue";

export const menuState = reactive({
  isOpen: false,
});

export function toggleMenu() {
  menuState.isOpen = !menuState.isOpen;
}

export function closeMenu() {
  menuState.isOpen = false;
}

/**
 * Хук для управления глобальными событиями меню (например, Escape)
 */
export function useMenuControls() {
  const handleKeyDown = (e: KeyboardEvent) => {
    if (e.key === "Escape" && menuState.isOpen) {
      closeMenu();
    }
  };

  onMounted(() => {
    window.addEventListener("keydown", handleKeyDown);
  });

  onUnmounted(() => {
    window.removeEventListener("keydown", handleKeyDown);
  });
}
