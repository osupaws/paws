import { createRouter, createWebHashHistory } from "vue-router";
import DashboardView from "../views/DashboardView.vue";

const router = createRouter({
  // В десктопном Tauri приложении лучше использовать HashHistory,
  // так как нет реального сервера, чтобы отдавать index.html на все пути (HTML5 History).
  history: createWebHashHistory(),
  routes: [
    {
      path: "/",
      name: "home",
      component: DashboardView,
    },
  ],
});

export default router;
