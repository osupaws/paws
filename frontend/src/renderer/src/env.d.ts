// frontend/src/renderer/src/env.d.ts

/// <reference types="vite/client" />

// --- START: ADDED CODE ---
import { ElectronAPI } from "@preload/ipc/electron.ipc";
import { SplashAPI } from "@preload/ipc/splash.ipc";
import { StoreAPI } from "@preload/ipc/store.ipc";
import { ThemesAPI } from '@preload/ipc/themes.ipc';

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
      themes: ThemesAPI;
      backend: BackendAPI;
    };
  }
}
// --- END: ADDED CODE ---

declare module "*.vue" {
  import type { DefineComponent } from "vue";
  const component: DefineComponent<{}, {}, any>;
  export default component;
}
