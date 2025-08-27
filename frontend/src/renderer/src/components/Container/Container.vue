<script setup lang="ts">
import styles from "./Container.module.css";

interface Props {
  outerClassnames?: string;
  innerClassnames?: string;
}

const { outerClassnames, innerClassnames } = defineProps<Props>();
</script>

<template>
  <div :class="[styles.containerOuter, outerClassnames]">
    <div :class="[styles.containerInner, innerClassnames]">
      <slot />
    </div>
  </div>
</template>
