// frontend/src/renderer/src/env.d.ts

/// <reference types="vite/client" />

import { PawsAPI } from "@preload/api.d.ts";

declare global {
  interface Window {
    api: PawsAPI;
  }
}
