<script setup lang="ts">
import styles from "./Field.module.css";
</script>

<template>
  <div :class="styles.field">
    <slot />
  </div>
</template>
