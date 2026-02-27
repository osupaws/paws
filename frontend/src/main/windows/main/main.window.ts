import { is } from "@electron-toolkit/utils";
import { appState } from "@main/state/app.state";
import { BrowserWindow, shell } from "electron";
import log from "electron-log";
import { join } from "path";

export const createMainWindow = (): BrowserWindow => {
	const mainWindow = new BrowserWindow({
		width: 640,
		height: 800,
		resizable: false,
		maximizable: false,
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

	// We handle showing the window in index.ts after the splash/update flow
	// to ensure a smooth transition.

	mainWindow.webContents.setWindowOpenHandler(details => {
		shell.openExternal(details.url);
		return { action: "deny" };
	});

	// HMR for renderer base on electron-vite cli.
	// Load the remote URL for development or the local html file for production.
	if (is.dev && process.env.ELECTRON_RENDERER_URL) {
		log.info(`Main Window: Loading URL ${process.env.ELECTRON_RENDERER_URL}`);
		mainWindow.loadURL(process.env.ELECTRON_RENDERER_URL);
		mainWindow.webContents.openDevTools({ mode: "detach" });
	} else {
		const filePath = join(__dirname, "../renderer/index.html");
		log.info(`Main Window: Loading File ${filePath}`);
		mainWindow.loadFile(filePath);
	}

	mainWindow.webContents.on(
		"did-fail-load",
		(_event, errorCode, errorDescription, validatedURL) => {
			log.error(`Failed to load URL: ${validatedURL}, Error: ${errorDescription} (${errorCode})`);
		}
	);

	return mainWindow;
};
