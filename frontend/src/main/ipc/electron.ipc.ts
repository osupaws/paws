// frontend/src/main/ipc/electron.ipc.ts

import { appState } from "@main/state/app.state";
import { app, ipcMain } from "electron";

ipcMain.on("electron.close-app", (): void => {
  app.quit();
});

ipcMain.on("electron.minimize-window", (): void => {
  const mainWindow = appState.get("mainWindow");

  if (mainWindow) {
    mainWindow.minimize();
  }
});
