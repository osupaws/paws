import { app } from "electron";
import * as fs from "fs";
import * as path from "path";

// Define the type locally as this is a main process file
// and cannot access renderer-specific type definitions.
interface Theme {
  id: string;
  name: string;
  base: string;
  file: string;
}

const log = (message: string, ...args: any[]): void => {
  console.log(`[ThemeManager] ${message}`, ...args);
};

// Gets the path to the user's custom themes directory, creating it if it doesn't exist.
function getCustomThemesPath(): string {
  const userDataPath = app.getPath("userData");
  const customThemesPath = path.join(userDataPath, "themes");
  if (!fs.existsSync(customThemesPath)) {
    log(`Custom themes directory not found. Creating at: ${customThemesPath}`);
    fs.mkdirSync(customThemesPath, { recursive: true });
  }
  return customThemesPath;
}

// Parses a single theme directory.
function parseTheme(themeDir: string): Theme | null {
  const manifestPath = path.join(themeDir, "theme.json");
  if (!fs.existsSync(manifestPath)) {
    log(
      `Skipping directory: No theme.json found in ${path.basename(themeDir)}`,
    );
    return null;
  }

  try {
    const manifestContent = fs.readFileSync(manifestPath, "utf-8");
    const manifest = JSON.parse(manifestContent);

    // Basic validation to ensure required fields are present
    if (!manifest.id || !manifest.name || !manifest.base || !manifest.file) {
      log(
        `Skipping theme in ${path.basename(themeDir)}: Manifest is missing required fields (id, name, base, file).`,
      );
      return null;
    }

    // Construct the file path relative to the themes directory
    manifest.file = `${path.basename(themeDir)}/${manifest.file}`;

    log(`Successfully parsed theme: ${manifest.name}`);
    return manifest as Theme;
  } catch (error) {
    log(`Error parsing theme.json in ${path.basename(themeDir)}:`, error);
    return null;
  }
}

/**
 * Scans the user's data directory for custom themes and returns them as an array.
 */
export function getCustomThemes(): Theme[] {
  log("Scanning for custom themes...");
  const themesPath = getCustomThemesPath();
  const themeDirs = fs
    .readdirSync(themesPath, { withFileTypes: true })
    .filter((dirent) => dirent.isDirectory())
    .map((dirent) => path.join(themesPath, dirent.name));

  const customThemes: Theme[] = [];
  for (const dir of themeDirs) {
    const theme = parseTheme(dir);
    if (theme) {
      customThemes.push(theme);
    }
  }

  log(`Found ${customThemes.length} valid custom themes.`);
  return customThemes;
}
