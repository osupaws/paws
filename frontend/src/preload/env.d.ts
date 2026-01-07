// frontend/src/preload/env.d.ts

/// <reference types="vite/client" />

import { ElectronAPI } from "@preload/ipc/electron.ipc";
import { SplashAPI } from "@preload/ipc/splash.ipc";
import { StoreAPI } from "@preload/ipc/store.ipc";

// --- START: MODIFIED/ADDED CODE ---
// This interface defines the shape of our backend API bridge
interface BackendAPI {
  get: (endpoint: string) => Promise<any>;
  post: (endpoint: string, body: any) => Promise<any>;
}

declare global {
  interface Window {
		api: {
			electron: ElectronAPI;
			store: StoreAPI;
			splash: SplashAPI;
			// This is the line that was missing from this specific file
			backend: BackendAPI;
		};
	}
}
// --- END: MODIFIED/ADDED CODE ---
