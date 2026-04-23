<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { getCurrentWindow } from '@tauri-apps/api/window';

const appWindow = getCurrentWindow();
const isAppReady = ref(false);

const emit = defineEmits<{
  (e: 'ready'): void;
}>();

const props = defineProps({
  initPromise: {
    type: Promise,
    required: true
  },
  minDisplayTimeMs: {
    type: Number,
    default: 1200
  },
  autoShow: {
    type: Boolean,
    default: true
  }
});

const handleDrag = async (e: MouseEvent) => {
  if (e.button === 0) {
    await appWindow.startDragging();
  }
};

// Читаем цвета напрямую из localStorage СИНХРОННО до первого рендера.
// Если это первый запуск (или кэш пуст), используем дефолтные темные Paws цвета
const splashStyle = {
  backgroundColor: localStorage.getItem('paws-splash-bg') || '#121212',
  color: localStorage.getItem('paws-splash-text') || '#ffffff',
  '--dynamic-accent': localStorage.getItem('paws-splash-accent') || '#ffa4c1'
} as any;

onMounted(async () => {
  const startTime = Date.now();

  // Вызываем appWindow.show(), как только Vue отрисовал Splash Screen
  // (только если нам разрешено, иначе ждем когда App.vue сам вызовет show)
  if (props.autoShow) {
    requestAnimationFrame(async () => {
      try {
        await appWindow.show();
      } catch(e) { /* ignore */ }
    });
  }

  // Ждем, пока завершится предоставленный промис загрузки бэкенда (initConfig и т.д.)
  try {
    console.log("[SplashScreen] Awaiting initPromise...");
    await props.initPromise;
    console.log("[SplashScreen] initPromise resolved!");
  } catch(e) {
    console.error("[SplashScreen] Initialization error:", e);
  }

  // Ожидаем оставшееся время, чтобы splash screen не мельтешил
  const elapsed = Date.now() - startTime;
  if (elapsed < props.minDisplayTimeMs) {
    await new Promise(r => setTimeout(r, props.minDisplayTimeMs - elapsed));
  }

  isAppReady.value = true;
  emit('ready');
});
</script>

<template>
  <Transition name="splash">
    <div v-if="!isAppReady" class="splash-screen" @mousedown="handleDrag" :style="splashStyle">
      <div class="splash-logo">paws</div>
      <div class="splash-loader"></div>
      <div class="splash-status">stabilizing system</div>
    </div>
  </Transition>
</template>

<style scoped>
/* === Сплэш Экран === */
.splash-screen {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  /* background-color задается через инлайн :style для нулевой задержки */
  background-color: #121212; 
  z-index: 9999;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 16px;
}

.splash-logo {
  font-family: "Fredoka", var(--paws-font-primary);
  font-size: 56px;
  font-weight: 500;
  color: inherit;
  letter-spacing: -2px;
  margin-bottom: 4px;
}

.splash-loader {
  width: 48px;
  height: 4px;
  background: rgba(255,255,255,0.05);
  border-radius: 4px;
  overflow: hidden;
  position: relative;
}

.splash-loader::after {
  content: "";
  position: absolute;
  top: 0; left: 0; bottom: 0;
  width: 100%;
  background: var(--dynamic-accent, #ffa4c1); 
  border-radius: 4px;
  transform: translateX(-100%);
  animation: splash-load 2s cubic-bezier(0.4, 0, 0.2, 1) infinite;
}

.splash-status {
  font-family: var(--paws-font-primary);
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 2px;
  opacity: 0.3;
  margin-top: 8px;
  animation: pulse 2s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 0.2; }
  50% { opacity: 0.5; }
}

@keyframes splash-load {
  0% { transform: translateX(-100%) scaleX(0.2); }
  40% { transform: translateX(0%) scaleX(0.4); }
  100% { transform: translateX(100%) scaleX(0.2); }
}

.splash-leave-active {
  transition: opacity 0.5s cubic-bezier(0.33, 1, 0.68, 1), transform 0.6s cubic-bezier(0.33, 1, 0.68, 1);
}
.splash-leave-to {
  opacity: 0;
  transform: translateY(-20px) scale(1.02);
}
</style>
