import { is } from "@electron-toolkit/utils";
import { startBackend, stopBackend } from "@main/backend/backend";
import { appState } from "@main/state/app.state";
import { createMainWindow } from "@main/windows/main/main.window";
import { createSplashWindow } from "@main/windows/splash/splash.window";
import { app, dialog, ipcMain, OpenDialogOptions } from "electron";
import log from "electron-log";
import { join } from "path";

ipcMain.on("electron.close-app", (): void => {
	app.quit();
});

ipcMain.on("electron.relaunch", () => {
	log.info("Relaunch requested...");
	stopBackend();

	if (is.dev) {
		log.info("Dev Mode: Performing soft reload.");
		const mainWindow = appState.get("mainWindow");

		if (mainWindow && !mainWindow.isDestroyed()) {
			// Instead of closing and risking app.quit(), we just reload the main window
			// But first, let's show the splash screen again if possible
			createSplashWindow();

			// Start backend again
			startBackend();

			// Main window will be shown by startBackend() logic when ready
			// For now, keep it hidden or loading
			if (process.env.ELECTRON_RENDERER_URL) {
				mainWindow.loadURL(process.env.ELECTRON_RENDERER_URL);
			} else {
				mainWindow.loadFile(join(__dirname, "../renderer/index.html"));
			}
			mainWindow.hide();
		} else {
			// Fallback: just create new windows
			createSplashWindow();
			createMainWindow();
			startBackend();
		}
	} else {
		// Production: perform a clean hard relaunch
		app.relaunch();
		app.exit(0);
	}
});

ipcMain.on("electron.minimize-window", (): void => {
	const mainWindow = appState.get("mainWindow");

	if (mainWindow) {
		mainWindow.minimize();
	}
});

ipcMain.handle("electron.show-open-dialog", async (_event, options: OpenDialogOptions) => {
	const mainWindow = appState.get("mainWindow");
	if (!mainWindow) return { canceled: true, filePaths: [] };
	return await dialog.showOpenDialog(mainWindow, options);
});
