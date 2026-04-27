import { reactive } from 'vue';
import { callSidecar } from '../utils/sidecar-bridge';

export interface PawsPluginManifest {
  id: string;
  name: string;
  version: string;
  author: string;
  description: string;
  permissions?: string[];
  provides?: string[];
  consumes?: string[];
  iconData?: string;
  isActive?: boolean;
}

export const pluginState = reactive({
  loadedPlugins: [] as PawsPluginManifest[],
  allInstalledPlugins: [] as PawsPluginManifest[],
  activePluginId: null as string | null,
});

export const setActivePlugin = (id: string | null) => {
  pluginState.activePluginId = id;
};

export const setLoadedPlugins = (plugins: PawsPluginManifest[]) => {
  pluginState.loadedPlugins = plugins;
};

export async function fetchPlugins(): Promise<void> {
  const result = await callSidecar<PawsPluginManifest[]>("getLoadedPlugins");
  if (result.success && result.data) {
    pluginState.loadedPlugins = result.data;
  }
}

export async function fetchAllPlugins(): Promise<void> {
  const result = await callSidecar<PawsPluginManifest[]>("getDiscoveredPlugins");
  if (result.success && result.data) {
    pluginState.allInstalledPlugins = result.data;
  }
}

export async function togglePluginActive(id: string, isActive: boolean): Promise<void> {
  await callSidecar("togglePlugin", { id, isActive });
  await fetchPlugins();
  await fetchAllPlugins();

  if (!isActive && pluginState.activePluginId === id) {
    setActivePlugin(null);
  }
}
