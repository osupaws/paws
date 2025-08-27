<script setup lang="ts">
import { computed } from "vue";

import styles from "./Card.module.css";

interface Props {
  title?: string;
}

const { title } = defineProps<Props>();

const hasTitle = computed(() => title !== undefined && title !== "");
</script>

<template>
  <div :class="styles.card">
    <div v-if="hasTitle" :class="styles.cardTitle">
      {{ title }}
    </div>

    <div
      :class="[styles.cardContent, { [styles.cardContentNoTitle]: !hasTitle }]"
    >
      <slot />
    </div>
  </div>
</template>
