import { reactive } from 'vue';

export interface PawsPluginManifest {
  id: string;
  name: string;
  version: string;
  author: string;
  description: string;
}

export const pluginState = reactive({
  loadedPlugins: [] as PawsPluginManifest[],
  activePluginId: null as string | null,
});

export const setActivePlugin = (id: string | null) => {
  pluginState.activePluginId = id;
};

export const setLoadedPlugins = (plugins: PawsPluginManifest[]) => {
  pluginState.loadedPlugins = plugins;
};
