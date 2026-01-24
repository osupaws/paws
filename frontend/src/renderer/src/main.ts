import "@renderer/css/main.css";
import "@osupaws/paws-ui/dist/index.css";

import PawsUI from "@osupaws/paws-ui";
import App from "@renderer/App.vue";
import { initializeSettings } from "@renderer/state/settings.state";
import { initializeThemes } from "@renderer/state/theme.state";
import { createApp } from "vue";

async function startApp(): Promise<void> {
	// Ensure themes and settings are loaded before the app is mounted
	await Promise.all([initializeThemes(), initializeSettings()]);

	const app = createApp(App);
	app.use(PawsUI);
	app.mount("#app");
}

startApp();
