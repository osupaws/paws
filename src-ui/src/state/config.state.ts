import { reactive, toRaw } from "vue";
import { callSidecar } from "../utils/sidecar-bridge";
import { setPawsUiConfig } from "@osupaws/paws-ui";
import { applyTheme, initThemes } from "./theme.state";

export interface AppConfig {
  isLegacyMode: boolean;
  stablePath: string;
  lazerPath: string;
  currentThemeId: string;
  isFirstLaunch: boolean;
  isSwitchOnLogoEnabled: boolean;
  isShowTips: boolean;
  isHideInTrayOnClose: boolean;
  isLaunchOnStartup: boolean;
  isInvisibleLaunch: boolean;
  isDeveloperModeEnabled: boolean;
  devPluginPath: string;
}

let isInitializing = false;

export const configState = reactive<AppConfig>({
  isLegacyMode: true,
  stablePath: "",
  lazerPath: "",
  currentThemeId: "paws-dark",
  isFirstLaunch: true,
  isSwitchOnLogoEnabled: true,
  isShowTips: true,
  isHideInTrayOnClose: true,
  isLaunchOnStartup: false,
  isInvisibleLaunch: false,
  isDeveloperModeEnabled: false,
  devPluginPath: "",
});

import { invoke } from "@tauri-apps/api/core";

export async function initConfig() {
  isInitializing = true;
  console.time("[ConfigState] Total Init");
  
  // Запускаем загрузку тем и конфига параллельно
  const [_, resp] = await Promise.all([
    initThemes(),
    callSidecar<AppConfig>("getConfig")
  ]);

  if (resp.success && resp.data) {
    Object.assign(configState, resp.data);
    setPawsUiConfig({ showTooltips: configState.isShowTips });
    applyTheme(configState.currentThemeId, configState.isShowTips);
    console.log("[ConfigState] Loaded from backend:", configState);
    
    // Регистрируем Dev Plugin при старте, если он активен
    if (configState.isDeveloperModeEnabled && configState.devPluginPath) {
      try {
        await invoke("register_plugin_path", { 
          pluginId: "org.test.hello", 
          path: configState.devPluginPath 
        });
      } catch (e) {
        console.error("Failed to register dev plugin path on startup:", e);
      }
    }

    // Спрашиваем путь к БД для отладки
    callSidecar<{ path: string }>("getDbMetadata").then(r => {
      if (r.success) console.log("%c[Database] path: " + r.data?.path, "color: #ffa4c1; font-weight: bold;");
    });
  }

  isInitializing = false;
  console.timeEnd("[ConfigState] Total Init");
}

/**
 * Переключает режим Stable/Lazer и сохраняет в БД.
 */
export async function toggleLegacyMode() {
  configState.isLegacyMode = !configState.isLegacyMode;
  await saveConfig();
}

/**
 * Сохраняет текущую конфигурацию на сервер.
 */
export async function saveConfig() {
  if (isInitializing) return;
  applyTheme(configState.currentThemeId, configState.isShowTips);
  const resp = await callSidecar("updateConfig", { config: toRaw(configState) });
  if (!resp.success) {
    console.error("[ConfigState] Failed to save config:", resp.error);
  }
}
