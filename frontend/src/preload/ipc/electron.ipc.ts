import { ipcRenderer } from "electron";

export const electronIpc = {
	closeApp: () => ipcRenderer.send("electron.close-app"),
	minimizeWindow: () => ipcRenderer.send("electron.minimize-window"),
	showOpenDialog: (options: any) => ipcRenderer.invoke("electron.show-open-dialog", options),
	relaunch: () => ipcRenderer.send("electron.relaunch")
};

export type ElectronAPI = typeof electronIpc;
