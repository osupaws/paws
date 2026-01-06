import "@renderer/css/main.css";

import App from "@renderer/App.vue";
import { initializeThemes } from '@renderer/state/theme.state';
import { createApp } from "vue";

async function startApp() {
  // Ensure themes are loaded before the app is mounted
  await initializeThemes();
  
  createApp(App).mount("#app");
}

startApp();
