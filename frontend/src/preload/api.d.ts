import { ElectronAPI } from "@preload/ipc/electron.ipc";
import { SplashAPI } from "@preload/ipc/splash.ipc";
import { ThemesAPI } from "@preload/ipc/themes.ipc";

export interface BackendAPI {
	get: (endpoint: string) => Promise<any>;
	post: (endpoint: string, body: any) => Promise<any>;
}

export interface StorageAPI {
	uploadAsset: (filePath: string) => Promise<{ assetId: string }>;
	processAsset: (assetId: string, options: any) => Promise<any>;
}

export interface PawsAPI {
	electron: ElectronAPI;

	splash: SplashAPI;
	themes: ThemesAPI;
	backend: BackendAPI;
	storage: StorageAPI;
}
