import { reactive } from "vue";
import { closeMenu } from "./menu.state";

export const modalState = reactive({
  isSettingsOpen: false,
  isPluginsOpen: false
});

function toggleBodyClass(isOpen: boolean) {
  if (isOpen) {
    document.body.classList.add("has-modal");
  } else {
    document.body.classList.remove("has-modal");
  }
}

export function openSettings(): void {
  closeMenu();
  modalState.isSettingsOpen = true;
  toggleBodyClass(true);
}

export function closeSettings(): void {
  if (!modalState.isSettingsOpen) return;
  modalState.isSettingsOpen = false;
  toggleBodyClass(false);
  window.dispatchEvent(new Event("paws:close-modals"));
}

export function toggleSettings(): void {
  if (modalState.isSettingsOpen) {
    closeSettings();
  } else {
    openSettings();
  }
}

export function openPlugins(): void {
  closeMenu();
  modalState.isPluginsOpen = true;
  toggleBodyClass(true);
}

export function closePlugins(): void {
  if (!modalState.isPluginsOpen) return;
  modalState.isPluginsOpen = false;
  toggleBodyClass(false);
  window.dispatchEvent(new Event("paws:close-modals"));
}

export function togglePlugins(): void {
  if (modalState.isPluginsOpen) {
    closePlugins();
  } else {
    openPlugins();
  }
}

export function closeAllModals(): void {
  if (!modalState.isSettingsOpen && !modalState.isPluginsOpen) return;
  modalState.isSettingsOpen = false;
  modalState.isPluginsOpen = false;
  toggleBodyClass(false);
  window.dispatchEvent(new Event("paws:close-modals"));
}

export function useModalControls() {
  const handleEscape = (e: KeyboardEvent) => {
    if (e.key === "Escape" && (modalState.isSettingsOpen || modalState.isPluginsOpen || document.querySelector('.backdrop'))) {
      closeAllModals();
    }
  };

  return { handleEscape };
}
