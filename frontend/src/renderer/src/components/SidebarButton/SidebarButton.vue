<script setup lang="ts">
import { RouterLink } from "vue-router";

import styles from "./SidebarButton.module.css";

interface Props {
  to: string;
}

const { to } = defineProps<Props>();
</script>

<template>
  <RouterLink :to="to">
    <button :class="styles.button">
      <slot />
    </button>
  </RouterLink>
</template>
