import { reactive } from "vue";

export const settingsState = reactive({
	isLegacyMode: false
});

/**
 * Initializes settings from the backend Realm database.
 */
export async function initializeSettings(): Promise<void> {
	try {
		const config = await window.api.backend.get("/api/config");
		settingsState.isLegacyMode = config.isLegacyMode;
		console.log("[Settings] Loaded config from backend:", config);
	} catch (error) {
		console.error("[Settings] Failed to load config:", error);
	}
}

/**
 * Updates the legacy mode setting and persists it to the backend.
 */
export async function setLegacyMode(enabled: boolean): Promise<void> {
	settingsState.isLegacyMode = enabled;
	try {
		await window.api.backend.post("/api/config", { isLegacyMode: enabled });
		console.log(`[Settings] Legacy Mode ${enabled ? "enabled" : "disabled"}`);
	} catch (error) {
		console.error("[Settings] Failed to save legacy mode:", error);
	}
}
