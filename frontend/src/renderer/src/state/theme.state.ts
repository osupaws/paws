import { reactive } from 'vue';

// Define the structure for a Theme
export interface Theme {
  id: string;
  name: string;
  base: string;
  file: string;
}

// Hardcoded core themes to ensure they are always available
const coreThemes: Theme[] = [
  { id: 'dark', name: 'Dark (Default)', base: 'dark', file: 'themes/dark.css' },
  { id: 'light', name: 'Light', base: 'light', file: 'themes/light.css' }
];

// Reactive store for theme management
export const themeState = reactive({
  activeThemeId: 'dark', // Will be properly set after initialization
  availableThemes: [...coreThemes] as Theme[],
});

/**
 * Fetches custom themes and combines them with core themes.
 * Then, initializes the active theme from storage, ensuring it's a valid, available theme.
 */
export async function initializeThemes(): Promise<void> {
  console.log('[ThemeState] Initializing themes...');
  
  const customThemes = await window.api.themes.getCustom();
  themeState.availableThemes = [...coreThemes, ...customThemes];
  console.log(`[ThemeState] Available themes populated. Total: ${themeState.availableThemes.length}`);

  const savedThemeId = window.api.store.get('activeThemeId', 'dark');
  const isSavedThemeAvailable = themeState.availableThemes.some(t => t.id === savedThemeId);
  
  if (isSavedThemeAvailable) {
    themeState.activeThemeId = savedThemeId;
    console.log(`[ThemeState] Set active theme from storage: ${savedThemeId}`);
  } else {
    themeState.activeThemeId = 'dark';
    console.log(`[ThemeState] Saved theme '${savedThemeId}' not found. Defaulting to 'dark'.`);
  }
}

// Function to get the full theme info based on activeThemeId
export function getActiveThemeInfo(): Theme {
  const theme = themeState.availableThemes.find(t => t.id === themeState.activeThemeId);
  // Fallback to dark if activeThemeId is somehow still invalid
  return theme || coreThemes[0];
}
