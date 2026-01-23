import "@renderer/css/main.css";
import "@osupaws/paws-ui/dist/index.css";

import PawsUI from "@osupaws/paws-ui";
import App from "@renderer/App.vue";
import { initializeThemes } from "@renderer/state/theme.state";
import { createApp } from "vue";

async function startApp(): Promise<void> {
	// Ensure themes are loaded before the app is mounted
	await initializeThemes();

	const app = createApp(App);
	app.use(PawsUI);
	app.mount("#app");
}

startApp();
