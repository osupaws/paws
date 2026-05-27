import { reactive } from "vue";
import { updateThemeLinks, type ThemeInfo } from "../utils/theme-manager";
import { setPawsUiConfig } from "@osupaws/paws-ui";

import { callSidecar } from "../utils/sidecar-bridge";

export interface Theme {
  id: string;
  name: string;
  isBuiltIn: boolean;
  baseThemeId: string;
  blobHash?: string;
  css?: string;
}

export const themeState = reactive({
  activeThemeId: "paws-dark" as string,
  availableThemes: [] as Theme[]
});

export async function initThemes() {
  const resp = await callSidecar<Theme[]>("getThemes");
  if (resp.success && resp.data) {
    themeState.availableThemes = resp.data;
  }
}

export function applyTheme(themeId: string, showTips?: boolean) {
  const theme = themeState.availableThemes.find(t => t.id === themeId) || themeState.availableThemes[0];
  if (!theme) return;

  themeState.activeThemeId = theme.id;
  
  const themeInfo: ThemeInfo = {
    id: theme.id,
    name: theme.name,
    base: theme.baseThemeId as any,
    fileHash: theme.blobHash
  };

  updateThemeLinks(themeInfo);
  setPawsUiConfig({ 
    theme: theme.baseThemeId?.includes("dark") ? "dark" : "light",
    ...(showTips !== undefined ? { showTooltips: showTips } : {})
  });

  // Asynchronously read and cache splash screen colors from computed styles (zero launch delay)
  setTimeout(() => {
    try {
      const bg = getComputedStyle(document.body).getPropertyValue('--paws-color-bg-primary').trim();
      const text = getComputedStyle(document.body).getPropertyValue('--paws-color-text-primary').trim();
      const accent = getComputedStyle(document.body).getPropertyValue('--paws-color-accent-primary').trim();
      
      if (bg) localStorage.setItem('paws-splash-bg', bg);
      if (text) localStorage.setItem('paws-splash-text', text);
      if (accent) localStorage.setItem('paws-splash-accent', accent);
    } catch (e) {
      console.warn("[ThemeManager] Failed to cache splash theme styles:", e);
    }
  }, 150);
}

export function setThemeBase(base: "dark" | "light") {
  const themeId = base === "dark" ? "paws-dark" : "paws-light";
  themeState.activeThemeId = themeId;
  applyTheme(themeId);
}

export function toggleBaseTheme() {
  const nextBase = themeState.activeThemeId.includes("dark") ? "light" : "dark";
  setThemeBase(nextBase);
}
