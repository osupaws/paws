/// <reference types="vite/client" />

import { PawsAPI } from "@preload/api";

declare global {
  interface Window {
    api: PawsAPI;
  }
}
