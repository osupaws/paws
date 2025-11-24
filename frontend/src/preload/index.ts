// frontend/src/preload/index.ts

import { electronAPI } from "@electron-toolkit/preload";
import { electronIpc } from "@preload/ipc/electron.ipc";
import { splashIpc } from "@preload/ipc/splash.ipc";
import { storeIpc } from "@preload/ipc/store.ipc";
import { contextBridge, ipcRenderer } from "electron";

// --- START: ADDED CODE ---
const backendApi = {
  get: (endpoint: string): Promise<any> =>
    ipcRenderer.invoke("api-get", endpoint),
  post: (endpoint: string, body: any): Promise<any> =>
    ipcRenderer.invoke("api-post", { endpoint, body }),
};
// --- END: ADDED CODE ---

const api = {
  electron: electronIpc,
  store: storeIpc,
  splash: splashIpc,
  // --- START: ADDED CODE ---
  backend: backendApi,
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
