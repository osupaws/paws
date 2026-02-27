import { ElectronAPI } from "@preload/ipc/electron.ipc";
import { SplashAPI } from "@preload/ipc/splash.ipc";
import { ThemesAPI } from "@preload/ipc/themes.ipc";
import { UpdateStatus } from "@main/updater";

export interface UpdaterAPI {
	check: () => Promise<void>;
	download: () => Promise<void>;
	install: () => Promise<void>;
	onStatus: (callback: (status: UpdateStatus) => void) => () => void;
	getStatus: () => void;
}

export interface BackendAPI {
	get: (endpoint: string) => Promise<any>;
	post: (endpoint: string, body: any) => Promise<any>;
}

export interface StorageAPI {
	uploadAsset: (filePath: string) => Promise<{ assetId: string }>;
	/** Fallback: Upload pure binary data (buffer). Use when data has no physical path. */
	uploadTemp: (data: ArrayBuffer | Uint8Array) => Promise<{ tempHandle: string }>;
	/** Fast: Upload file by its system path. Preferred for Drag-and-Drop in Electron. */
	uploadTempPath: (filePath: string) => Promise<{ tempHandle: string }>;
	processAsset: (assetId: string, options: any) => Promise<any>;
}

export interface PawsAPI {
	electron: ElectronAPI;

	splash: SplashAPI;
	themes: ThemesAPI;
	updater: UpdaterAPI;
	backend: BackendAPI;

	storage: StorageAPI;
}
