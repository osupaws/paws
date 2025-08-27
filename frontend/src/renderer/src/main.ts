import "@renderer/css/main.css";
import "@paws/ui/dist/index.css";

import PawsUI from "@paws/ui";
import App from "@renderer/App.vue";
import { router } from "@renderer/router/router";
import { createApp } from "vue";

createApp(App).use(router).use(PawsUI).mount("#app");
