import { createApp } from "vue";
import "@osupaws/paws-ui/dist/index.css";
import "./assets/main.css";
import App from "./App.vue";
import router from "./router";
import { vPawsTooltip } from "@osupaws/paws-ui";

const app = createApp(App);
app.use(router);
app.directive("paws-tooltip", vPawsTooltip);
app.mount("#app");
