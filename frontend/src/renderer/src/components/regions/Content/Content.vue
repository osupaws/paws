<script setup lang="ts">
import Container from "@renderer/components/Container/Container.vue";
import { RouterView } from "vue-router";

import styles from "./Content.module.css";
</script>

<template>
  <div :class="styles.content">
    <Container>
      <RouterView />
    </Container>
  </div>
</template>
