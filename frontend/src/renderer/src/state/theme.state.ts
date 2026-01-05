import { reactive, watch } from "vue";

// Define the structure for a Theme (built-in or custom)
export interface Theme {
  id: string; // Unique ID (e.g., 'dark', 'light', or custom theme name)
  name: string; // Display name (e.g., 'Dark (Default)', 'My Purple Theme')
  base: string; // Which built-in theme this is based on ('dark' or 'light')
  file: string; // Path to the theme's main CSS file (e.g., 'themes/dark.css' or 'themes/my-custom-theme/theme.css')
  customFile?: string; // Optional: Path to a custom user CSS file if it's an override theme
}

// Reactive store for theme management
export const themeState = reactive({
  // Initial theme; will be loaded from persisted storage later
  activeThemeId: "dark",
  availableThemes: [
    {
      id: "dark",
      name: "Dark (Default)",
      base: "dark",
      file: "themes/dark.css",
    },
    { id: "light", name: "Light", base: "light", file: "themes/light.css" },
    {
      id: "incomplete-test",
      name: "Incomplete Test",
      base: "dark",
      file: "themes/incomplete-test/theme.css",
    },
    // Custom themes will be dynamically loaded here later
  ] as Theme[],
});

// Function to get the full theme info based on activeThemeId
export function getActiveThemeInfo(): Theme {
  const theme = themeState.availableThemes.find(
    (t) => t.id === themeState.activeThemeId,
  );
  // Fallback to dark if activeThemeId is not found (e.g., if a custom theme was deleted)
  return theme || themeState.availableThemes.find((t) => t.id === "dark")!;
}

// Watch for changes in activeThemeId and update the main app's theme link
watch(
  () => themeState.activeThemeId,
  (newThemeId) => {
    const themeInfo = themeState.availableThemes.find(
      (t) => t.id === newThemeId,
    );
    const themeLink = document.getElementById(
      "app-theme-link",
    ) as HTMLLinkElement;
    if (themeLink && themeInfo) {
      themeLink.href = `paws-app://${themeInfo.file}`;
    }
  },
  { immediate: true },
); // Immediate ensures the theme is set on first load
