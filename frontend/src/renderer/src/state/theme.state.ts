import type { Theme } from "@common/types";
import { reactive } from "vue";

// Hardcoded core themes to ensure they are always available
const coreThemes: Theme[] = [
	{
		id: "paws-dark",
		name: "Dark (Default)",
		base: "dark",
		file: null, // Core themes don't have a hash-based file
		isCustom: false
	},
	{
		id: "paws-light",
		name: "Light",
		base: "light",
		file: null, // Core themes don't have a hash-based file
		isCustom: false
	}
];

// Reactive store for theme management
export const themeState = reactive({
	activeThemeId: "paws-dark", // Will be properly set after initialization
	lastDarkThemeId: "paws-dark",
	lastLightThemeId: "paws-light",
	availableThemes: [...coreThemes] as Theme[]
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
				`[ThemeState] Custom theme ID '${theme.id}' uses a reserved prefix 'paws-' and might be ignored in the future.`
			);
		}
		return { ...theme, isCustom: true };
	});

	themeState.availableThemes = [...coreThemes, ...customThemes];
	console.log(
		`[ThemeState] Available themes populated. Total: ${themeState.availableThemes.length}`
	);

	// Handle backward compatibility for saved themes.
	// Old saved values might be 'dark' or 'light'. We need to convert them to 'paws-dark'/'paws-light'.
	let savedThemeId = window.api.store.get("activeThemeId", "paws-dark");
	if (savedThemeId === "dark") savedThemeId = "paws-dark";
	if (savedThemeId === "light") savedThemeId = "paws-light";

	// Load memory of last used base themes
	const savedLastDark = window.api.store.get("lastDarkThemeId", "paws-dark");
	const savedLastLight = window.api.store.get("lastLightThemeId", "paws-light");

	// Verify they still exist
	if (themeState.availableThemes.some(t => t.id === savedLastDark)) {
		themeState.lastDarkThemeId = savedLastDark;
	}
	if (themeState.availableThemes.some(t => t.id === savedLastLight)) {
		themeState.lastLightThemeId = savedLastLight;
	}

	const isSavedThemeAvailable = themeState.availableThemes.some(t => t.id === savedThemeId);

	if (isSavedThemeAvailable) {
		themeState.activeThemeId = savedThemeId;
		console.log(`[ThemeState] Set active theme from storage: ${savedThemeId}`);
	} else {
		themeState.activeThemeId = "paws-dark";
		console.log(`[ThemeState] Saved theme '${savedThemeId}' not found. Defaulting to 'paws-dark'.`);
	}
}

// Function to get the full theme info based on activeThemeId
export function getActiveThemeInfo(): Theme {
	const theme = themeState.availableThemes.find(t => t.id === themeState.activeThemeId);
	// Fallback to paws-dark if activeThemeId is somehow still invalid
	return theme || coreThemes[0];
}

/**
 * Imports a theme from a .pawstheme (zip) file.
 */
export async function importTheme(filePath: string): Promise<void> {
	try {
		console.log(`[ThemeState] Importing theme from: ${filePath}`);
		// Post to backend
		const newTheme = await window.api.backend.post("/api/themes/import", { filePath });

		// Add isCustom flag since backend DTO might not have it strictly typed for frontend logic yet,
		// or we just ensure it aligns with our frontend type.
		const themeWithFlag = { ...newTheme, isCustom: true };

		// Check if already exists
		const existingIndex = themeState.availableThemes.findIndex(t => t.id === themeWithFlag.id);
		if (existingIndex !== -1) {
			themeState.availableThemes[existingIndex] = themeWithFlag;
		} else {
			themeState.availableThemes.push(themeWithFlag);
		}

		console.log(`[ThemeState] Theme imported successfully: ${themeWithFlag.name}`);

		// Optionally switch to it immediately? Let's ask user.
		// For now, just adding it is enough.
	} catch (error) {
		console.error("[ThemeState] Failed to import theme:", error);
		throw error;
	}
}

/**
 * Sets the active theme and persists it.
 * Also updates the "last used" memory for the theme's base (dark/light).
 */
export function setActiveTheme(themeId: string): void {
	const theme = themeState.availableThemes.find(t => t.id === themeId);

	if (theme) {
		themeState.activeThemeId = themeId;
		window.api.store.set("activeThemeId", themeId);
		console.log(`[ThemeState] Theme changed to: ${themeId}`);

		// Remember this theme as the last used for its base
		if (theme.base === "dark") {
			themeState.lastDarkThemeId = themeId;
			window.api.store.set("lastDarkThemeId", themeId);
		} else if (theme.base === "light") {
			themeState.lastLightThemeId = themeId;
			window.api.store.set("lastLightThemeId", themeId);
		}
	} else {
		console.warn(`[ThemeState] Attempted to set invalid theme ID: ${themeId}`);
	}
}

/**
 * Switches to the last used theme of the specified base (dark/light).
 */
export function setThemeBase(base: "dark" | "light"): void {
	if (base === "dark") {
		setActiveTheme(themeState.lastDarkThemeId);
	} else {
		setActiveTheme(themeState.lastLightThemeId);
	}
}
