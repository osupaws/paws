import { reactive } from "vue";

// Define the structure for a Theme
export interface Theme {
  id: string; // The globally unique ID, e.g., 'paws-dark' or 'my-custom-theme'
  name: string;
  author?: string;
  version?: string;
  base: string;
  file: {
    hash: string;
    size: number;
    extension: string;
  } | null;
  isCustom: boolean;
}

// Hardcoded core themes to ensure they are always available
const coreThemes: Theme[] = [
  {
    id: "paws-dark",
    name: "Dark (Default)",
    base: "dark",
    file: null, // Core themes don't have a hash-based file
    isCustom: false,
  },
  {
    id: "paws-light",
    name: "Light",
    base: "light",
    file: null, // Core themes don't have a hash-based file
    isCustom: false,
  },
];

// Reactive store for theme management
export const themeState = reactive({
  activeThemeId: "paws-dark", // Will be properly set after initialization
  availableThemes: [...coreThemes] as Theme[],
});

/**
 * Fetches custom themes and combines them with core themes.
 * Then, initializes the active theme from storage, ensuring it's a valid, available theme.
 */
export async function initializeThemes(): Promise<void> {
  console.log("[ThemeState] Initializing themes...");

  const customThemeData = await window.api.themes.getCustom();

  // Mark fetched themes as custom.
  const customThemes = customThemeData.map((theme: Omit<Theme, "isCustom">) => {
    // Ensure custom themes don't accidentally use a reserved 'paws-' prefix for their ID
    if (theme.id.startsWith("paws-")) {
      console.warn(
        `[ThemeState] Custom theme ID '${theme.id}' uses a reserved prefix 'paws-' and might be ignored in the future.`,
      );
    }
    return { ...theme, isCustom: true };
  });

  themeState.availableThemes = [...coreThemes, ...customThemes];
  console.log(
    `[ThemeState] Available themes populated. Total: ${themeState.availableThemes.length}`,
  );

  // Handle backward compatibility for saved themes.
  // Old saved values might be 'dark' or 'light'. We need to convert them to 'paws-dark'/'paws-light'.
  let savedThemeId = window.api.store.get("activeThemeId", "paws-dark");
  if (savedThemeId === "dark") savedThemeId = "paws-dark";
  if (savedThemeId === "light") savedThemeId = "paws-light";

  const isSavedThemeAvailable = themeState.availableThemes.some(
    (t) => t.id === savedThemeId,
  );

  if (isSavedThemeAvailable) {
    themeState.activeThemeId = savedThemeId;
    console.log(`[ThemeState] Set active theme from storage: ${savedThemeId}`);
  } else {
    themeState.activeThemeId = "paws-dark";
    console.log(
      `[ThemeState] Saved theme '${savedThemeId}' not found. Defaulting to 'paws-dark'.`,
    );
  }
}

// Function to get the full theme info based on activeThemeId
export function getActiveThemeInfo(): Theme {
  const theme = themeState.availableThemes.find(
    (t) => t.id === themeState.activeThemeId,
  );
  // Fallback to paws-dark if activeThemeId is somehow still invalid
  return theme || coreThemes[0];
}
