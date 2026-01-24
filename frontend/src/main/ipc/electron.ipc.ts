import { appState } from "@main/state/app.state";
import { app, dialog, ipcMain, OpenDialogOptions } from "electron";

ipcMain.on("electron.close-app", (): void => {
	app.quit();
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
