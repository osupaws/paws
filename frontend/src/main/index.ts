// frontend/src/main/index.ts

import "@main/ipc/register-ipc";

import { electronApp, is } from "@electron-toolkit/utils";
import { startBackend, stopBackend } from "@main/backend/backend";
import { createMainWindow } from "@main/windows/main/main.window";
import { createSplashWindow } from "@main/windows/splash/splash.window";
import { app, BrowserWindow, ipcMain, net, protocol } from "electron";
import log from "electron-log";
import { existsSync } from "fs";
import { join, normalize } from "path";

// Linter Fix: Add async return type Promise<any>
const BACKEND_PORT = 5088;

async function forwardRequest(endpoint: string, options: RequestInit = {}): Promise<any> {
	const baseUrl = `http://localhost:${BACKEND_PORT}`;
	try {
		const response = await net.fetch(`${baseUrl}${endpoint}`, options);
		if (!response.ok) {
			const errorText = await response.text();
			throw new Error(errorText || `API Error ${response.status}`);
		}
		const responseText = await response.text();
		return responseText ? JSON.parse(responseText) : null;
	} catch (error) {
		if (error instanceof Error) {
			log.error(`API request to ${endpoint} failed:`, error.message);
		}
		throw error;
	}
}

// Linter Fix: Add Promise<any> return type
ipcMain.handle("api-get", (_event, endpoint): Promise<any> => forwardRequest(endpoint));

// Linter Fix: Add Promise<any> return type
ipcMain.handle("api-post", (_event, { endpoint, body }): Promise<any> => {
	return forwardRequest(endpoint, {
		method: "POST",
		headers: { "Content-Type": "application/json" },
		body: JSON.stringify(body)
	});
});

// Register schemes as privileged to allow fetch API, service workers, and bypass CSP for local resources if needed.
// This MUST be called before app.ready
protocol.registerSchemesAsPrivileged([
	{
		scheme: "paws-app",
		privileges: {
			secure: true,
			standard: true,
			supportFetchAPI: true, // Critical for fetch("paws-app://...")
			bypassCSP: false,
			corsEnabled: true
		}
	},
	{
		scheme: "paws-theme",
		privileges: {
			secure: true,
			standard: true,
			supportFetchAPI: true, // Critical for fetch("paws-theme://...")
			bypassCSP: false,
			corsEnabled: true
		}
	},
	{
		scheme: "paws-plugin",
		privileges: {
			secure: true,
			standard: true,
			supportFetchAPI: true, // Critical for fetch("paws-plugin://...")
			bypassCSP: false,
			corsEnabled: true
		}
	}
]);

app.whenReady().then(async () => {
	electronApp.setAppUserModelId("org.paws.Paws");

	// --- PAWS App Protocol (System Files) ---
	// This protocol serves non-plugin, built-in application assets.
	// --- PAWS App Protocol (System Files) ---
	// This protocol serves non-plugin, built-in application assets.
	protocol.handle("paws-app", request => {
		try {
			const url = new URL(request.url);

            // Handle hostname as part of the path (e.g. paws-app://file.js -> hostname=file.js)
            let relativePath = url.hostname;
            if (url.pathname && url.pathname !== "/") {
                relativePath = join(relativePath, url.pathname);
            }

            // Remove trailing separator if present (browser adds trailing slash to hostname-only URLs)
            if (relativePath.endsWith("/") || relativePath.endsWith("\\")) {
                relativePath = relativePath.slice(0, -1);
            }

			const publicRoot = is.dev
				? join(__dirname, "..", "..", "public") // Dev: serve from source public folder
				: join(__dirname, "..", "renderer"); // Prod: serve from built renderer folder

			const absolutePath = join(publicRoot, relativePath);

			// Explicitly check file existence
			if (!existsSync(absolutePath)) {
				log.warn(`File not found for paws-app: ${absolutePath} (Request: ${request.url})`);
				return new Response("Not Found", { status: 404 });
			}

			// SECURITY: Path Sandboxing
			if (!normalize(absolutePath).startsWith(normalize(publicRoot))) {
				log.error(
					`Security violation: Attempt to access file outside of allowed directory for paws-app. Request: ${request.url}`
				);
				return new Response("Forbidden", { status: 403 });
			}

            const fileUrl = require("url").pathToFileURL(absolutePath).toString();
			return net.fetch(fileUrl);
		} catch (error) {
			log.error(`Error in 'paws-app' protocol for ${request.url}: ${error}`);
			return new Response("Internal Server Error", { status: 500 });
		}
	});

	// --- PAWS Theme Protocol (User Themes) ---
	// This protocol serves user-provided theme assets by fetching them from the backend by hash.
	protocol.handle("paws-theme", request => {
		try {
			const url = new URL(request.url);
			const hash = url.hostname; // The hash is now the "host" of the URL

			if (!hash) {
				log.error("`paws-theme` protocol error: No hash provided in the URL.");
				return new Response("Bad Request", { status: 400 });
			}

			const fileUrl = `http://localhost:5088/api/files/${hash}`;
			// log.info(`Forwarding paws-theme request to: ${fileUrl}`);

			// Forward the request to the C# backend's file serving endpoint
			return net.fetch(fileUrl);
		} catch (error) {
			log.error(`Error in 'paws-theme' protocol for ${request.url}: ${error}`);
			return new Response("Internal Server Error", { status: 500 });
		}
	});

	// --- PAWS Plugin Protocol ---
	// This protocol is used to serve the UI of a loaded plugin from the database.
	protocol.handle("paws-plugin", request => {
		try {
			const url = new URL(request.url);
			const pluginId = url.hostname;
			// pathname includes leading slash, e.g. /index.html
			const filePath = url.pathname;

			if (!pluginId) {
				log.error("`paws-plugin` protocol error: No pluginId provided.");
				return new Response("Bad Request", { status: 400 });
			}

			// Construct API URL: http://localhost:5088/api/plugins/{id}/files/{path}
			const apiUrl = `http://localhost:5088/api/plugins/${pluginId}/files${filePath}`;

			return net.fetch(apiUrl);
		} catch (error) {
			log.error(`Error in 'paws-plugin' customized protocol handler for ${request.url}: ${error}`);
			return new Response("Internal Server Error", { status: 500 });
		}
	});

	const splashWindow = createSplashWindow();
	createMainWindow(splashWindow);

	startBackend();

	app.on("activate", function () {
		if (BrowserWindow.getAllWindows().length === 0) createMainWindow(splashWindow);
	});
});

app.on("window-all-closed", () => {
	if (process.platform !== "darwin") {
		app.quit();
	}
});

app.on("before-quit", () => {
	stopBackend();
});
