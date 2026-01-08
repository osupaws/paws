import { ElectronAPI } from "@preload/ipc/electron.ipc";
import { SplashAPI } from "@preload/ipc/splash.ipc";
import { StoreAPI } from "@preload/ipc/store.ipc";
import { ThemesAPI } from "@preload/ipc/themes.ipc";

export interface BackendAPI {
  get: (endpoint: string) => Promise<any>;
  post: (endpoint: string, body: any) => Promise<any>;
}

export interface PawsAPI {
  electron: ElectronAPI;
  store: StoreAPI;
  splash: SplashAPI;
  themes: ThemesAPI;
  backend: BackendAPI;
}
