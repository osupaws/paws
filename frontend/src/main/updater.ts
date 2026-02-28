import { app, BrowserWindow, ipcMain, net } from "electron";
import log from "electron-log";
import { autoUpdater, UpdateInfo } from "electron-updater";

// Configuration
const METADATA_URL = "https://raw.githubusercontent.com/osupaws/paws/main/update-metadata.json";

export interface UpdateStatus {
	type:
		| "checking"
		| "available"
		| "not-available"
		| "downloading"
		| "downloaded"
		| "error"
		| "mandatory-check-failed";
	version?: string;
	progress?: number;
	error?: string;
	isMandatory?: boolean;
}

import {
	splashSendProgressUpdate,
	splashSendStatusUpdate
} from "./windows/splash/splash.web-contents";

let lastStatus: UpdateStatus = { type: "checking" };
let resolveCanProceed: (value: boolean) => void;

export const canProceedPromise = new Promise<boolean>(resolve => {
	resolveCanProceed = resolve;
});

export async function initializeUpdater(): Promise<void> {
	autoUpdater.logger = log;
	autoUpdater.autoDownload = false;

	// 1. Mandatory Metadata Check (Future use for forced min version)
	splashSendStatusUpdate("Verifying version...");
	try {
		const response = await net.fetch(METADATA_URL);
		if (response.ok) {
			const metadata = await response.json();
			const { minVersion } = metadata;
			const currentVersion = app.getVersion();

			if (minVersion && isVersionLower(currentVersion, minVersion)) {
				log.info(`[Updater] Core minVersion: ${minVersion} (Current: ${currentVersion})`);
			}
		}
	} catch (error) {
		log.warn("[Updater] Failed to fetch update metadata:", error);
	}

	// 2. Setup Listeners
	autoUpdater.on("checking-for-update", () => {
		emitStatus({ type: "checking" });
		splashSendStatusUpdate("Checking for updates...");
	});

	autoUpdater.on("update-available", (info: UpdateInfo) => {
		log.info("[Updater] Update available:", info.version);
		// All updates are now treated as mandatory/automatic by default
		emitStatus({ type: "available", version: info.version, isMandatory: true });

		splashSendStatusUpdate(`Downloading update ${info.version}...`);
		autoUpdater.downloadUpdate();
	});

	autoUpdater.on("update-not-available", () => {
		emitStatus({ type: "not-available" });
		splashSendStatusUpdate("App is up to date.");
		setTimeout(() => resolveCanProceed(true), 500);
	});

	autoUpdater.on("error", (err: Error) => {
		log.error("[Updater] Error:", err);
		emitStatus({ type: "error", error: err.message });
		splashSendStatusUpdate("Warning: Update check failed.");
		// Proceed anyway if update check fails to prevent soft-lock
		setTimeout(() => resolveCanProceed(true), 1500);
	});

	autoUpdater.on("download-progress", progressObj => {
		emitStatus({ type: "downloading", progress: progressObj.percent });
		splashSendProgressUpdate({
			percent: progressObj.percent,
			message: `Downloading update... ${Math.floor(progressObj.percent)}%`
		});
	});

	autoUpdater.on("update-downloaded", info => {
		log.info("[Updater] Update downloaded:", info.version);
		emitStatus({ type: "downloaded", version: info.version });

		splashSendStatusUpdate("Installing update...");
		autoUpdater.quitAndInstall();
	});

	// IPC Handlers
	ipcMain.handle("updater:check", () => {
		autoUpdater.checkForUpdates();
	});

	ipcMain.handle("updater:download", () => {
		autoUpdater.downloadUpdate();
	});

	ipcMain.handle("updater:install", () => {
		autoUpdater.quitAndInstall();
	});

	ipcMain.handle("updater:get-version", async () => {
		const fs = await import("fs");
		const path = await import("path");
		// Try to find package.json in app path or one level up (common in dev/prod differences)
		let pkgPath = path.join(app.getAppPath(), "package.json");
		if (!fs.existsSync(pkgPath)) {
			pkgPath = path.join(app.getAppPath(), "..", "package.json");
		}

		let pkg: any = {};
		try {
			pkg = JSON.parse(fs.readFileSync(pkgPath, "utf-8"));
		} catch (e) {
			log.error("[Updater] Could not read package.json:", e);
		}

		return {
			app: app.getVersion(),
			schema: pkg.lazerSchemaVersion || "unknown"
		};
	});

	ipcMain.handle("updater:fetch-metadata", async () => {
		try {
			// In development, try to read the local file first for instant feedback
			if (!app.isPackaged) {
				const fs = await import("fs");
				const path = await import("path");
				// Assuming the file is in the root of the project (pawsprtp/update-metadata.json)
				const localPath = path.join(app.getAppPath(), "..", "update-metadata.json");
				if (fs.existsSync(localPath)) {
					const content = fs.readFileSync(localPath, "utf-8");
					return JSON.parse(content);
				}
			}

			const response = await net.fetch(METADATA_URL);
			if (response.ok) {
				return await response.json();
			}
		} catch (error) {
			log.error("[Updater] Failed to fetch metadata for UI:", error);
		}
		return null;
	});

	ipcMain.on("updater:get-status", event => {
		event.reply("updater:status", lastStatus);
	});

	// Trigger initial check
	if (app.isPackaged) {
		autoUpdater.checkForUpdates();
	} else {
		log.info("[Updater] Development mode - skipping auto-update check.");
		splashSendStatusUpdate("Dev Mode: Skipping updates.");
		setTimeout(() => resolveCanProceed(true), 1000);
	}
}

function emitStatus(status: UpdateStatus): void {
	lastStatus = status;
	BrowserWindow.getAllWindows().forEach(win => {
		if (!win.isDestroyed()) {
			win.webContents.send("updater:status", status);
		}
	});
}

function isVersionLower(current: string, target: string): boolean {
	const clean = (v: string): number[] =>
		v
			.split("-")[0]
			.split(".")
			.map(n => parseInt(n, 10));

	const c = clean(current);
	const t = clean(target);

	for (let i = 0; i < 3; i++) {
		const cVal = c[i] || 0;
		const tVal = t[i] || 0;
		if (cVal < tVal) return true;
		if (cVal > tVal) return false;
	}
	const cTag = current.split("-")[1];
	const tTag = target.split("-")[1];

	if (!tTag && cTag) return true;
	if (tTag && !cTag) return false;

	if (cTag && tTag) {
		return cTag.localeCompare(tTag) < 0;
	}

	return false;
}
