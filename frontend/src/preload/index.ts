// frontend/src/preload/index.ts

import { electronAPI } from "@electron-toolkit/preload";
import { electronIpc } from "@preload/ipc/electron.ipc";
import { splashIpc } from "@preload/ipc/splash.ipc";
import { themesIpc } from "@preload/ipc/themes.ipc";
import { contextBridge, ipcRenderer } from "electron";

// --- START: ADDED CODE ---
const backendApi = {
	get: (endpoint: string): Promise<any> => ipcRenderer.invoke("api-get", endpoint),
	post: (endpoint: string, body: any): Promise<any> =>
		ipcRenderer.invoke("api-post", { endpoint, body })
};
// --- END: ADDED CODE ---

const storageApi = {
	uploadAsset: (filePath: string): Promise<any> =>
		ipcRenderer.invoke("paws:upload-asset", filePath),
	uploadTemp: (data: ArrayBuffer | Uint8Array): Promise<any> =>
		ipcRenderer.invoke("paws:upload-temp", new Uint8Array(data)),
	uploadTempPath: (filePath: string): Promise<any> =>
		ipcRenderer.invoke("paws:upload-temp-path", filePath),
	processAsset: (assetId: string, options: any): Promise<any> =>
		ipcRenderer.invoke("api-post", {
			endpoint: "/assets/process",
			body: { assetId, options }
		})
};

const api = {
	electron: electronIpc,

	splash: splashIpc,
	themes: themesIpc,
	// --- START: ADDED CODE ---
	backend: backendApi,
	storage: storageApi
	// --- END: ADDED CODE ---
};

if (process.contextIsolated) {
	try {
		contextBridge.exposeInMainWorld("electron", electronAPI);
		contextBridge.exposeInMainWorld("api", api);
	} catch (error) {
		console.error(error);
	}
} else {
	// @ts-ignore (define in d.ts)
	window.api = api;
}
