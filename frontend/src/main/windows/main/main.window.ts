import { is } from "@electron-toolkit/utils";
import { appState } from "@main/state/app.state";
import { BrowserWindow, shell } from "electron";
import { join } from "path";

export const createMainWindow = (splashWindow: BrowserWindow): BrowserWindow => {
	const mainWindow = new BrowserWindow({
		width: 640,
		height: 800,
		resizable: false,
		frame: false,
		show: false,
		titleBarStyle: "hidden",
		backgroundColor: "#000000",
		webPreferences: {
			preload: join(__dirname, "../preload/index.js"),
			sandbox: false,
			contextIsolation: true,
			nodeIntegration: false,
			// TEMPORARY! Disable web security for development/testing, often necessary for local file access via custom protocols.
			// WARNING: Reconsider for production builds.
			webSecurity: false,
			// Disable preferred size mode to prevent autofill related console errors.
			enablePreferredSizeMode: false
		},
		icon: join(__dirname, "assets/icon.png")
	});
	appState.set("mainWindow", mainWindow);

	mainWindow.on("ready-to-show", () => {
		mainWindow.show();

		if (splashWindow) {
			splashWindow.destroy();
		}
	});

	mainWindow.webContents.setWindowOpenHandler(details => {
		shell.openExternal(details.url);
		return { action: "deny" };
	});

	// HMR for renderer base on electron-vite cli.
	// Load the remote URL for development or the local html file for production.
	if (is.dev && process.env.ELECTRON_RENDERER_URL) {
		mainWindow.loadURL(process.env.ELECTRON_RENDERER_URL);
		mainWindow.webContents.openDevTools({ mode: "detach" });
	} else {
		mainWindow.loadFile(join(__dirname, "../renderer/index.html"));
	}

	return mainWindow;
};
