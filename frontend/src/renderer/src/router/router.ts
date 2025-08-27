import HomePage from "@renderer/pages/HomePage/HomePage.vue";
import PluginPage from "@renderer/pages/PluginPage/PluginPage.vue";
import SettingsPage from "@renderer/pages/SettingsPage/SettingsPage.vue";
import { createMemoryHistory, createRouter, RouteRecordRaw } from "vue-router";

const routes: RouteRecordRaw[] = [
  { path: "/", component: HomePage },
  { path: "/plugin", component: PluginPage },
  { path: "/settings", component: SettingsPage },
];

export const router = createRouter({
  history: createMemoryHistory(),
  routes,
});
