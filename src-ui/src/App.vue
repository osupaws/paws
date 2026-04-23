<script setup lang="ts">
import { onMounted, onUnmounted, watch, ref } from "vue";
import SettingsModal from "./components/SettingsModal.vue";
import AppMenu from "./components/AppMenu.vue";
import SplashScreen from "./components/SplashScreen.vue";
import {
  toggleMenu,
  useMenuControls,
  menuState,
  closeMenu,
} from "./state/menu.state";
import {
  modalState,
  useModalControls,
  closeAllModals,
} from "./state/modal.state";
import {
  configState,
  initConfig,
  toggleLegacyMode,
} from "./state/config.state";
import { pluginState } from "./state/plugin.state";
import { themeState } from "./state/theme.state";
import { getCurrentWindow } from "@tauri-apps/api/window";
import { listen } from "@tauri-apps/api/event";
import { invoke } from "@tauri-apps/api/core";
import { MinimizeIcon, CloseIcon, PawsTooltip } from "@osupaws/paws-ui";

const appWindow = getCurrentWindow();
const isMounted = ref(false);
const isAppReady = ref(false);
const isAutostart = ref(false);
const jsStartTime = performance.now();

useMenuControls();
const { handleEscape } = useModalControls();

// Реализация моста для плагинов (window.paws)
const initPawsBridge = () => {
  const listeners: Record<string, Function[]> = {};

  (window as any).paws = {
    on: (event: string, callback: Function) => {
      if (!listeners[event]) listeners[event] = [];
      listeners[event].push(callback);

      // Сразу вызываем callback с текущим состоянием для инициализации
      if (event === "theme-changed") {
        callback(themeState.activeThemeId.includes("dark") ? "dark" : "light");
      }
      if (event === "mode-changed") {
        callback(configState.isLegacyMode ? "stable" : "lazer");
      }

      return () => {
        const list = listeners[event];
        if (list) {
          listeners[event] = list.filter((l) => l !== callback);
        }
      };
    },
    emit: (event: string, payload: any) => {
      if (listeners[event]) {
        listeners[event].forEach((l) => l(payload));
      }
    },
  };

  // Следим за изменениями стейтов ядра и транслируем их в мост
  watch(
    () => themeState.activeThemeId,
    (newId) => {
      (window as any).paws.emit(
        "theme-changed",
        newId.includes("dark") ? "dark" : "light",
      );
    },
  );

  watch(
    () => configState.isLegacyMode,
    (isLegacy) => {
      (window as any).paws.emit("mode-changed", isLegacy ? "stable" : "lazer");
    },
  );
};

const handleLogoDblClick = async () => {
  if (configState.isSwitchOnLogoEnabled) {
    await toggleLegacyMode();
  }
};

const onGlobalKeydown = (e: KeyboardEvent) => {
  handleEscape(e);
};

// Обещание, которое дождется SplashScreen
const initConfigPromise = ref<Promise<void> | null>(null);

onMounted(async () => {
  isMounted.value = true;
  window.addEventListener("keydown", onGlobalKeydown);
  initPawsBridge();

  // Перехват закрытия: прячем в трей, если включено
  appWindow.onCloseRequested(async (event) => {
    console.log("[App] Close requested. Params:", {
      isHideInTray: configState.isHideInTrayOnClose,
      config: JSON.stringify(configState),
    });

    if (configState.isHideInTrayOnClose) {
      console.log("[App] Preventing close, hiding to tray instead.");
      event.preventDefault();
      await appWindow.hide();
    } else {
      console.log("[App] Hide in tray is DISABLED. Allowing window to close.");
      // В Tauri v2 по умолчанию окно закроется, если не вызвать preventDefault.
      // Но мы можем вызвать destroy() вручную для гарантии, если это кнопка "X"
    }
  });

  // Проверяем, запущен ли апп из автозагрузки (ярлыком с --autostart)
  try {
    isAutostart.value = await invoke<boolean>("is_autostart_launch");
    console.log("[App] Autostart detection result:", isAutostart.value);
  } catch (e) {
    console.error("[App] Failed to detect autostart:", e);
  }

  // Мы просто запускаем процесс. SplashScreen сам решит, когда показать окно
  console.log("[App] Starting initConfig...");
  initConfigPromise.value = initConfig()
    .then(() => {
      console.log("[App] initConfig finished successfully");
    })
    .catch((e) => {
      console.error("[App] initConfig failed:", e);
    });

  // Оптимизация ресурсов при скрытии в трей
  listen("paws://window-hide", () => {
    console.log("[App] System entering background mode. Cleaning up UI...");
    closeAllModals();
    closeMenu();
  });
});

onUnmounted(() => {
  window.removeEventListener("keydown", onGlobalKeydown);
});

/**
 * Программное перетаскивание окна.
 */
const handleDrag = async (e: MouseEvent) => {
  if (e.button === 0) {
    await appWindow.startDragging();
  }
};

/**
 * Логика показа окна после SplashScreen.
 */
const handleReady = async () => {
  const totalRustMs = await invoke<number>("get_startup_telemetry");
  const jsLoadMs = Math.round(performance.now() - jsStartTime);

  console.log(`[Telemetry] Initialized!`);
  console.log(`- Time since Process Start: ${totalRustMs}ms`);
  console.log(`- Time since JS Engine Load: ${jsLoadMs}ms`);

  // Вычисляем, должны ли мы вообще показывать окно.
  // Если запуск как autostart, показываем только если НЕ включен "невидимый запуск".
  const shouldShow = !(isAutostart.value && configState.isInvisibleLaunch);

  if (shouldShow) {
    // Если autostart == true, SplashScreen НЕ вызывал show(), поэтому мы должны вызвать его сейчас.
    // Если autostart == false, окно УЖЕ видимо благодаря SplashScreen.
    if (isAutostart.value) {
      await appWindow.show();
      await new Promise((r) => setTimeout(r, 50)); // Микро-задержка, чтобы сплэш не мигнул
    }
  }

  isAppReady.value = true;
};
</script>

<template>
  <SplashScreen
    v-if="isMounted && initConfigPromise"
    key="splash"
    :init-promise="initConfigPromise"
    :auto-show="!isAutostart"
    @ready="handleReady"
  />

  <Transition name="fade">
    <div
      v-show="isAppReady"
      class="app-shell"
      :class="{
        'open-menu': menuState.isOpen,
        'has-open-modal': modalState.isSettingsOpen || modalState.isPluginsOpen,
      }"
    >
      <div class="titlebar-background"></div>

      <header class="titlebar" @contextmenu.prevent @mousedown="handleDrag">
        <div class="titlebar-grid">
          <div class="section left">
            <button
              class="nav-button"
              :class="{ active: menuState.isOpen }"
              @click="toggleMenu"
              @mousedown.stop
            >
              <span class="nav-label">menu</span>
            </button>
          </div>

          <div class="section center">
            <div
              class="logo-wrapper"
              :class="{ interactive: configState.isSwitchOnLogoEnabled }"
              @dblclick="handleLogoDblClick"
              @mousedown="
                configState.isSwitchOnLogoEnabled
                  ? $event.stopPropagation()
                  : null
              "
            >
              <Transition name="legacy">
                <span v-if="configState.isLegacyMode" class="legacy-text"
                  >legacy</span
                >
              </Transition>
              <span class="logo" :class="{ shifted: configState.isLegacyMode }"
                >paws</span
              >
            </div>
          </div>

          <div class="section right">
            <div class="window-controls">
              <button
                class="win-btn minimize"
                @click="appWindow.minimize()"
                @mousedown.stop
              >
                <MinimizeIcon />
              </button>
              <button
                class="win-btn close"
                @click="appWindow.close()"
                @mousedown.stop
              >
                <CloseIcon />
              </button>
            </div>
          </div>
        </div>
      </header>

      <main class="main-container">
        <div id="paws-modal-root" class="content-surface">
          <!-- Отрисовка плагина -->
          <div v-if="pluginState.activePluginId" style="width: 100%; height: 100%;">
            <iframe
              :src="`http://pawsplugin.localhost/${pluginState.activePluginId}/index.html`"
              style="width: 100%; height: 100%; border: none; background: transparent;"
            ></iframe>
          </div>

          <!-- Отрисовка роутера (дашборда) -->
          <router-view v-else-if="$route" />

          <div v-else class="placeholder">
            <div class="status-badge">system ready</div>
            <p class="status-text">waiting for core...</p>
          </div>
        </div>

        <AppMenu />

        <!-- Модалки -->
        <SettingsModal v-if="isMounted" />
      </main>
      <PawsTooltip />
    </div>
  </Transition>
</template>

<style scoped>
/* Глобально скрываем скроллбары во всем приложении (Tauri/Webkit) */
:global(::-webkit-scrollbar) {
  display: none !important;
}

/* Для Firefox и стандартов (скрываем полосу прокрутки, сохраняя скролл) */
:global(*) {
  scrollbar-width: none !important;
  -ms-overflow-style: none !important;
}

/* === Плавное появление самого приложения === */
.fade-enter-active {
  transition: opacity 0.5s ease;
}
.fade-enter-from {
  opacity: 0;
}

.titlebar-background {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: calc(var(--paws-titlebar-height) + var(--paws-rounding-big));
  background-color: var(--paws-color-bg-titlebar);
  z-index: 1;
}

.titlebar {
  height: var(--paws-titlebar-height);
  position: relative;
  z-index: 10;
  display: flex;
  align-items: center;
  padding: 0 12px;
}

.titlebar-grid {
  display: grid;
  grid-template-columns: 1fr auto 1fr;
  width: 100%;
  align-items: center;
}

.section {
  display: flex;
  align-items: center;
}

.center {
  justify-content: center;
}
.right {
  justify-content: flex-end;
}

.logo {
  font-family: "Fredoka", var(--paws-font-primary);
  font-size: 32px;
  font-weight: var(--paws-font-weight-medium);
  color: var(--paws-color-text-primary);
  line-height: 1;
  position: relative;
  top: -2px;
  transition: transform 0.4s cubic-bezier(0.34, 1.35, 0.64, 1);
  transform: translateY(0) translateZ(0);
  -webkit-font-smoothing: subpixel-antialiased;
  text-rendering: optimizeLegibility;
  /* Исключаем из View Transition, чтобы не было гостинга при движении */
  view-transition-name: none;
  backface-visibility: hidden;
}

.logo.shifted {
  transform: translateY(4px) translateZ(0);
}

.logo-wrapper {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  position: relative;
}

.logo-wrapper.interactive {
  user-select: none;
  -webkit-app-region: no-drag;
}

.legacy-text {
  position: absolute;
  top: -4px;
  left: 50%;
  transform: translate(-50%, 0) translateZ(0);
  font-family: "Fredoka", var(--paws-font-primary);
  font-size: 14px;
  font-weight: 500;
  color: var(--paws-color-text-secondary);
  opacity: 0.7;
  width: max-content;
  line-height: 1;
  -webkit-font-smoothing: subpixel-antialiased;
  /* Исключаем из View Transition */
  view-transition-name: none;
  backface-visibility: hidden;
}

/* Legacy label transition */
.legacy-enter-active,
.legacy-leave-active {
  transition:
    opacity 0.4s cubic-bezier(0.34, 1.35, 0.64, 1),
    transform 0.4s cubic-bezier(0.34, 1.35, 0.64, 1);
}

.legacy-enter-from,
.legacy-leave-to {
  opacity: 0;
  transform: translate(-50%, -10px) translateZ(0);
}

.nav-button {
  background-color: var(--paws-color-interactive-primary);
  border: none;
  height: 32px;
  padding: 0 12px;
  border-radius: var(--paws-rounding-normal);
  color: var(--paws-color-text-secondary);
  font-family: var(--paws-font-primary);
  font-weight: var(--paws-font-weight-medium);
  cursor: pointer;
  transition: all 0.2s ease;
}

.nav-button:hover,
.nav-button.active {
  background-color: var(--paws-color-accent-primary);
  color: var(--paws-color-accent-secondary);
}

.window-controls {
  display: flex;
  gap: 4px;
}

.win-btn {
  background: transparent;
  border: none;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: var(--paws-rounding-normal);
  color: var(--paws-color-text-secondary);
  cursor: pointer;
  transition: all 0.2s ease;
}

.win-btn:hover {
  background-color: var(--paws-color-interactive-primary);
}

.win-btn.close:hover {
  background-color: var(--paws-color-error);
  color: #fff;
}

.app-shell {
  display: flex;
  flex-direction: column;
  height: 100vh;
  overflow: hidden;
}

.main-container {
  flex: 1;
  position: relative;
  z-index: 5;
  transform: translateZ(0);
  background-color: var(--paws-color-bg-subprimary);
  border-radius: var(--paws-rounding-big) var(--paws-rounding-big) 0 0;
  overflow: hidden;
}

.content-surface {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
}

.placeholder {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
}

.status-badge {
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 1px;
  background: var(--paws-color-bg-tertiary);
  padding: 4px 8px;
  border-radius: 4px;
  color: var(--paws-color-accent-primary);
  font-weight: var(--paws-font-weight-bold);
}

.status-text {
  font-size: 14px;
  color: var(--paws-color-text-secondary);
  font-weight: var(--paws-font-weight-light);
}
</style>
