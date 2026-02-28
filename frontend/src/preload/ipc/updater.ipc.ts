import { ipcRenderer } from "electron";

export const updaterIpc = {
	check: (): Promise<void> => ipcRenderer.invoke("updater:check"),
	download: (): Promise<void> => ipcRenderer.invoke("updater:download"),
	install: (): Promise<void> => ipcRenderer.invoke("updater:install"),
	onStatus: (callback: (status: any) => void) => {
		const listener = (_event: any, status: any): void => callback(status);
		ipcRenderer.on("updater:status", listener);
		return (): void => {
			ipcRenderer.removeListener("updater:status", listener);
		};
	},

	getStatus: () => ipcRenderer.send("updater:get-status"),
	getVersion: (): Promise<{ app: string; schema: string }> =>
		ipcRenderer.invoke("updater:get-version"),

	fetchMetadata: (): Promise<any> => ipcRenderer.invoke("updater:fetch-metadata")
};
