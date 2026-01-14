<script setup lang="ts">
import Content from "@renderer/components/regions/Content/Content.vue";
import Header from "@renderer/components/regions/Header/Header.vue";

import styles from "./Layout.module.css";
</script>

<template>
  <div :class="styles.layout">
    <Header />
    <Content plugin-src="" />
  </div>
</template>
