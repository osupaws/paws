import { ipcMain } from "electron";
import { getCustomThemes } from "../themes/theme-manager";

/**
 * Registers IPC handlers for theme-related operations.
 */
ipcMain.handle("themes:get-custom", async () => {
	console.log("[IPC] Received request for custom themes.");
	return getCustomThemes();
});
