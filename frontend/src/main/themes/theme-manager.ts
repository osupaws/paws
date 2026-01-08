import { net } from "electron";
import log from "electron-log";

// The shape of the theme data we expect from the C# backend API.
interface Theme {
	id: string;
	name: string;
	author?: string;
	version?: string;
	base: string;
	file: {
		hash: string;
		size: number;
		extension: string;
	} | null;
}

/**
 * Fetches the list of all custom themes from the C# backend.
 */
export async function getCustomThemes(): Promise<Theme[]> {
	log.info("Fetching custom themes from C# backend...");

	try {
		const request = await net.fetch("http://localhost:5088/api/themes");

		if (!request.ok) {
			log.error(`Failed to fetch themes. Status: ${request.status} ${request.statusText}`);
			return [];
		}

		const themes = (await request.json()) as Theme[];
		log.info(`Successfully fetched ${themes.length} themes from backend.`);
		return themes;
	} catch (error) {
		log.error("An error occurred while fetching themes:", error);
		return [];
	}
}
