// frontend/src/main/index.ts

import "@main/ipc/register-ipc";

import { electronApp, is } from "@electron-toolkit/utils";
import { startBackend, stopBackend } from "@main/backend/backend";
import { createMainWindow } from "@main/windows/main/main.window";
import { createSplashWindow } from "@main/windows/splash/splash.window";
import { app, BrowserWindow, ipcMain, net, protocol } from "electron";
import log from "electron-log";
import { existsSync, mkdirSync, readdirSync, readFileSync } from "fs";
import { dirname, join, normalize } from "path";

// Linter Fix: Add async return type Promise<any>
async function forwardRequest(
  endpoint: string,
  options: RequestInit = {},
): Promise<any> {
  const baseUrl = "http://localhost:5088";
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
ipcMain.handle(
  "api-get",
  (_event, endpoint): Promise<any> => forwardRequest(endpoint),
);

// Linter Fix: Add Promise<any> return type
ipcMain.handle("api-post", (_event, { endpoint, body }): Promise<any> => {
  return forwardRequest(endpoint, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
});

app.whenReady().then(async () => {
  electronApp.setAppUserModelId("org.paws.Paws");

  const pluginsBaseDir = is.dev
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
        "plugins",
      )
    : join(dirname(app.getPath("exe")), "resources", "plugins");

  if (!existsSync(pluginsBaseDir)) {
    mkdirSync(pluginsBaseDir, { recursive: true });
  }

  const pluginGuidToFolderMap = new Map<string, string>();

  try {
    const pluginFolders = readdirSync(pluginsBaseDir, { withFileTypes: true })
      .filter((dirent) => dirent.isDirectory())
      .map((dirent) => dirent.name);

    for (const folderName of pluginFolders) {
      const manifestPath = join(pluginsBaseDir, folderName, "plugin.json");
      if (existsSync(manifestPath)) {
        const manifest = JSON.parse(readFileSync(manifestPath, "utf-8"));
        if (manifest.id) {
          pluginGuidToFolderMap.set(manifest.id.toLowerCase(), folderName);
        }
      }
    }
  } catch (e) {
    log.error("Failed to build plugin folder map:", e);
  }

  // Register `paws-app://` protocol to serve shared frontend files.
  protocol.handle("paws-app", (request) => {
    const url = new URL(request.url);
    const assetPath = url.hostname; // The asset is the 'hostname' in a custom protocol

    const basePath = is.dev
      ? join(__dirname, "..", "..", "public") // In dev, serve from the source 'public' folder
      : join(__dirname, "..", "renderer"); // In prod, serve from the built 'renderer' folder

    const appFilePath = join(basePath, assetPath);

    /*
        log.info(`[paws-app] Request for: ${assetPath}`);
        log.info(`[paws-app] Base path: ${basePath}`);
        log.info(`[paws-app] Resolved path: ${appFilePath}`);
        log.info(`[paws-app] File exists: ${existsSync(appFilePath)}`);
        */

    return net.fetch(encodeURI(`file://${appFilePath.replace(/\\/g, "/")}`));
  });

  // --- PAWS Plugin Protocol ---
  // This protocol is used to serve the UI of a loaded plugin.
  protocol.handle("paws-plugin", (request) => {
    try {
      const url = new URL(request.url);
      const pluginId = url.hostname.toLowerCase();
      const folderName = pluginGuidToFolderMap.get(pluginId);

      if (!folderName) {
        log.error(
          `Protocol Error: Could not find a folder for plugin GUID ${pluginId}.`,
        );
        return new Response(null, { status: 404 });
      }

      const pluginRoot = join(pluginsBaseDir, folderName);
      const asarPath = join(pluginRoot, "ui.asar");
      const directoryPath = join(pluginRoot, "ui");

      let basePath: string;
      if (existsSync(asarPath)) {
        basePath = asarPath;
      } else if (existsSync(directoryPath)) {
        basePath = directoryPath;
      } else {
        log.error(
          `UI source not found for plugin ${pluginId}. Looked for ui.asar and ui/ directory.`,
        );
        return new Response(null, { status: 404 });
      }

      const requestedPath = decodeURIComponent(url.pathname).substring(1);
      const absolutePath = join(basePath, requestedPath);

      if (!normalize(absolutePath).startsWith(normalize(basePath))) {
        log.error(
          `Security violation: Attempt to access file outside of allowed directory. Request: ${request.url}`,
        );
        return new Response(null, { status: 403 });
      }

      return net.fetch(encodeURI(`file://${absolutePath.replace(/\\/g, "/")}`));
    } catch (error) {
      log.error(
        `Error in custom protocol handler for ${request.url}: ${error}`,
      );
      return new Response(null, { status: 500 });
    }
  });

  const splashWindow = createSplashWindow();
  createMainWindow(splashWindow);

  startBackend();

  app.on("activate", function () {
    if (BrowserWindow.getAllWindows().length === 0)
      createMainWindow(splashWindow);
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
