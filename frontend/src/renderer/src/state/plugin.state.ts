import { reactive } from "vue";

export interface PluginManifest {
	id: string;
	name: string;
	version: string;
	description: string;
	ui: {
		entry: string;
	} | null;
}

export const pluginState = reactive({
	loadedPlugins: [] as PluginManifest[],
	activePluginId: null as string | null
});

export async function fetchPlugins(): Promise<void> {
	try {
		const plugins = await window.api.backend.get("/api/plugins/loaded");
		pluginState.loadedPlugins = plugins;

		// If no active plugin is selected, pick the first one as default (optional)
		if (!pluginState.activePluginId && plugins.length > 0) {
			// pluginState.activePluginId = plugins[0].id; // Don't auto-select for now
		}
	} catch (error) {
		console.error("Failed to fetch plugins:", error);
	}
}

export function setActivePlugin(id: string | null): void {
	pluginState.activePluginId = id;
}
