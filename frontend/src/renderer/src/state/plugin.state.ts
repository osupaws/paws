import { reactive } from "vue";

export interface PluginManifest {
	id: string;
	name: string;
	version: string;
	description: string;
	author?: string;
	permissions?: string[];
	provides?: string[];
	consumes?: string[];
	ui: {
		entry: string;
	} | null;
	icon?: string;
	isActive: boolean;
}

export const pluginState = reactive({
	loadedPlugins: [] as PluginManifest[],
	allInstalledPlugins: [] as PluginManifest[],
	activePluginId: null as string | null
});

export async function fetchPlugins(): Promise<void> {
	try {
		const plugins = await window.api.backend.get("/api/plugins/loaded");
		pluginState.loadedPlugins = plugins;
	} catch (error) {
		console.error("Failed to fetch plugins:", error);
	}
}

export async function fetchAllPlugins(): Promise<void> {
	try {
		const plugins = await window.api.backend.get("/api/plugins/discovered");
		pluginState.allInstalledPlugins = plugins;
	} catch (error) {
		console.error("Failed to fetch all discovered plugins:", error);
	}
}

export async function togglePluginActive(id: string, isActive: boolean): Promise<void> {
	try {
		await window.api.backend.post("/api/plugins/toggle-active", { id, isActive });
		// Refresh both lists
		await fetchPlugins();
		await fetchAllPlugins();
	} catch (error) {
		console.error("Failed to toggle plugin active state:", error);
	}
}

export function setActivePlugin(id: string | null): void {
	console.log("[plugin.state] Setting Active Plugin ID:", id);
	pluginState.activePluginId = id;
}
