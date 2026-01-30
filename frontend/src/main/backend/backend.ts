import { is } from "@electron-toolkit/utils";
import { appState } from "@main/state/app.state";
import {
	splashSendProgressUpdate,
	splashSendStatusUpdate
} from "@main/windows/splash/splash.web-contents";
import { spawn } from "child_process";
import { app, dialog } from "electron";
import log from "electron-log";
import { existsSync } from "fs";
import { dirname, join } from "path";

let isStopping = false;

export const startBackend = (): void => {
	isStopping = false;
	const startTime = Date.now();
	const backendExecutableName = process.platform === "win32" ? "Paws.Host.exe" : "Paws.Host";

	const backendExecutable = is.dev
		? join(
				__dirname,
				"..",
				"..",
				"..",
				"Paws.DotNet",
				"Paws.Host",
				"bin",
				"Debug",
				"net8.0",
				backendExecutableName
			)
		: join(dirname(app.getPath("exe")), "resources", "Paws.Backend", backendExecutableName);

	if (!existsSync(backendExecutable)) {
		const errorMsg =
			"Critical Error: The Paws backend executable could not be found " +
			`at the expected location: ${backendExecutable}.` +
			"The application cannot continue.";
		log.error(errorMsg);
		dialog.showErrorBox("Fatal Error", errorMsg);

		app.quit();
		return;
	}

	// Args are no longer used for configuration; Backend uses its own DB
	const args = [];

	let heartbeatTimer: NodeJS.Timeout | null = null;
	let lastHeartbeat = Date.now();

	const triggerCrashScreen = (reason: string): void => {
		if (isStopping) return; // Ignore intentional stops
		log.error(`[Watchdog] Triggering crash screen: ${reason}`);

		const proc = appState.get("backendProcess");
		if (proc && !proc.killed) {
			proc.kill();
		}

		// Show Crash Screen
		const mainWindow = appState.get("mainWindow");
		if (mainWindow && !mainWindow.isDestroyed()) {
			if (is.dev && process.env.ELECTRON_RENDERER_URL) {
				mainWindow.loadURL(`${process.env.ELECTRON_RENDERER_URL}/crash.html`);
			} else {
				mainWindow.loadFile(join(__dirname, "../renderer/crash.html"));
			}
			mainWindow.show();
		}

		if (heartbeatTimer) clearInterval(heartbeatTimer);
	};

	const checkHeartbeat = (): void => {
		const now = Date.now();
		if (now - lastHeartbeat > 5000) {
			// HEARTBEAT_TIMEOUT = 5000
			triggerCrashScreen(`No heartbeat for ${now - lastHeartbeat}ms`);
		}
	};

	try {
		const backendProcess = spawn(backendExecutable, args);
		appState.set("backendProcess", backendProcess);

		let hostSignaledReady = false;

		backendProcess.stdout.on("data", data => {
			const logMessage = data.toString().trim();
			log.info(`[Backend]: ${logMessage}`);

			if (logMessage.includes("[Heartbeat]")) {
				lastHeartbeat = Date.now();
				return; // Don't process heartbeats further
			}

			// Parse [Progress] logs from C#
			// Format: [Progress] 45.0% - Loading plugins

			// C# logger might prefix with [Info] depending on configuration, so we handle both raw console write or Logger form.
			// Our HostServices uses _logger.LogInformation, which usually produces: "info: [Progress] 45.0% - Message"
			// OR "info: Paws.Host.HostServices[0] [Progress]..."
			// Let's make the regex more robust to find "[Progress]" anywhere.

			const pMatch = logMessage.match(/\[Progress\]\s+(\d+(?:\.\d+)?)%\s+-\s+(.*)/);
			if (pMatch) {
				const percent = parseFloat(pMatch[1]);
				const message = pMatch[2].trim();
				splashSendProgressUpdate({ percent, message });
			}

			if (!hostSignaledReady && logMessage.includes("Application started.")) {
				hostSignaledReady = true;
				log.info("Backend signaled readiness.");
				splashSendStatusUpdate("Backend services started.");
				splashSendProgressUpdate({ percent: 100, message: "Ready" });

				// Start Watchdog
				heartbeatTimer = setInterval(checkHeartbeat, 1000);

				const mainWindow = appState.get("mainWindow");
				if (mainWindow && !mainWindow.isDestroyed()) {
					// Ensure splash stays open for at least 2 seconds (Smart Minimum Duration)
					const minSplashDuration = 2000;
					const elapsed = Date.now() - startTime;
					const delay = Math.max(0, minSplashDuration - elapsed);

					setTimeout(() => {
						mainWindow.show();
						// Close splash after main window is visible
						const splashWindow = appState.get("splashWindow");
						if (splashWindow && !splashWindow.isDestroyed()) {
							splashWindow.destroy();
						}
					}, delay);
				}
			}
		});

		backendProcess.stderr.on("data", data =>
			log.error(`Backend STDERR: ${data.toString().trim()}`)
		);

		backendProcess.on("error", err => {
			log.error("Failed to start C# Host process.", err);
			triggerCrashScreen(`Backend process error: ${err.message}`);
		});

		backendProcess.on("close", code => {
			log.info(`Backend process exited with code ${code}.`);
			triggerCrashScreen(`Backend process exited with code ${code}`);
		});
	} catch (error) {
		log.error("Fatal error trying to spawn C# Host process:", error);
		triggerCrashScreen("Exception during process spawn");
	}
};

export const stopBackend = (): void => {
	isStopping = true;
	const process = appState.get("backendProcess");

	if (process) {
		process.kill("SIGINT");
	}
};
