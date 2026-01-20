<script setup lang="ts">
import { menuState, toggleMenu } from "@renderer/state/menu.state";

import styles from "./Titlebar.module.css";

// Методы для вызова IPC каналов
const minimizeWindow = (): void => {
  window.api.electron.minimizeWindow();
};

const closeWindow = (): void => {
  window.api.electron.closeApp();
};
</script>

<template>
  <div :class="styles.titlebar">
    <!-- Левая секция: Кнопка пользователя -->
    <div :class="styles.left">
      <button
        :class="[styles.userButton, { [styles.active]: menuState.isOpen }]"
        @click="toggleMenu"
      >
        <!-- Временно просто текст -->
        <span>user</span>
      </button>
    </div>

    <!-- Центральная секция: Логотип -->
    <div :class="styles.center">
      <span :class="styles.logo">paws</span>
    </div>

    <!-- Правая секция: Кнопки управления окна -->
    <div :class="styles.right">
      <button :class="styles.windowButton" @click="minimizeWindow">—</button>
      <button
        :class="[styles.windowButton, styles.closeButton]"
        @click="closeWindow"
      >
        ✕
      </button>
    </div>
  </div>
</template>
