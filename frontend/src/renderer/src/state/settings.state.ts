import { reactive } from "vue";

export const settingsState = reactive({
	isLegacyMode: false,
	isSwitchOnLogoEnabled: false,
	isShowTips: true, // Default to true for tips usually
	stablePath: null as string | null,
	lazerPath: null as string | null
});

/**
 * Helper to save a setting to the backend.
 */
async function saveSetting(key: string, value: string, type: string = "string"): Promise<void> {
	try {
		await window.api.backend.post("/api/settings", { key, value, type });
		console.log(`[Settings] Setting saved: ${key} = ${value} (${type})`);
	} catch (error) {
		console.error(`[Settings] Failed to save setting ${key}:`, error);
		throw error;
	}
}

/**
 * Initializes settings from the backend.
 */
export async function initializeSettings(): Promise<void> {
	try {
		const settings = (await window.api.backend.get("/api/settings")) as {
			key: string;
			value: string;
			type: string;
		}[];

		const settingsMap = settings.reduce(
			(acc, s) => {
				acc[s.key] = s.value;
				return acc;
			},
			{} as Record<string, string>
		);

		settingsState.isLegacyMode = settingsMap["core.modes.legacy"] === "true";
		settingsState.isSwitchOnLogoEnabled = settingsMap["core.ui.switchOnLogo"] === "true";
		// For tips, default is true if key is missing, or parse value
		settingsState.isShowTips =
			settingsMap["core.ui.showTips"] !== undefined
				? settingsMap["core.ui.showTips"] === "true"
				: true;
		settingsState.stablePath = settingsMap["core.paths.stable"] || null;
		settingsState.lazerPath = settingsMap["core.paths.lazer"] || null;

		console.log("[Settings] Loaded settings from backend:", settingsMap);
	} catch (error) {
		console.error("[Settings] Failed to load settings:", error);
	}
}

/**
 * Updates the legacy mode setting.
 */
export async function setLegacyMode(enabled: boolean): Promise<void> {
	settingsState.isLegacyMode = enabled;
	await saveSetting("core.modes.legacy", enabled.toString(), "bool");
}

/**
 * Updates the switch on logo setting.
 */
export async function setSwitchOnLogoEnabled(enabled: boolean): Promise<void> {
	settingsState.isSwitchOnLogoEnabled = enabled;
	await saveSetting("core.ui.switchOnLogo", enabled.toString(), "bool");
}

/**
 * Updates the show tips setting.
 */
export async function setShowTips(enabled: boolean): Promise<void> {
	settingsState.isShowTips = enabled;
	await saveSetting("core.ui.showTips", enabled.toString(), "bool");
}

/**
 * Updates the stable path.
 */
export async function setStablePath(path: string): Promise<void> {
	settingsState.stablePath = path;
	await saveSetting("core.paths.stable", path, "string");
}

/**
 * Updates the lazer path.
 */
export async function setLazerPath(path: string): Promise<void> {
	settingsState.lazerPath = path;
	await saveSetting("core.paths.lazer", path, "string");
}
