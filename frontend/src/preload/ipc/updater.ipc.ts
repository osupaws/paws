import { ipcRenderer } from "electron";

export const updaterIpc = {
	check: (): Promise<void> => ipcRenderer.invoke("updater:check"),
	download: (): Promise<void> => ipcRenderer.invoke("updater:download"),
	install: (): Promise<void> => ipcRenderer.invoke("updater:install"),
	onStatus: (callback: (status: any) => void) => {
		const listener = (_event, status: any) => callback(status);
		ipcRenderer.on("updater:status", listener);
		return () => ipcRenderer.removeListener("updater:status", listener);
	},
	getStatus: () => ipcRenderer.send("updater:get-status")
};
