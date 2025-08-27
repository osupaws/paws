<script setup lang="ts">
import { PawsHeading, PawsInput, PawsMultiSwitch } from "@paws/ui";
import Card from "@renderer/components/Card/Card.vue";
import Field from "@renderer/components/UI/Field/Field.vue";
import FolderIcon from "@renderer/components/UI/Icon/FolderIcon.vue";

import styles from "./SettingsPage.module.css";

const tab = ["settings", "plugins"];
</script>

<template>
  <div>
    <div :class="styles.switch">
      <PawsMultiSwitch model-value="settings" :options="tab" size="large" />
    </div>

    <div :class="styles.content">
      <div :class="styles.contentBlock">
        <Card title="paths">
          <Field>
            <PawsInput title="lazer">
              <template #icon>
                <FolderIcon width="20px" height="20px" />
              </template>
            </PawsInput>
          </Field>

          <Field>
            <PawsInput title="stable">
              <template #icon>
                <FolderIcon width="20px" height="20px" />
              </template>
            </PawsInput>
          </Field>
        </Card>

        <Card title="colors">
          <Field>
            <PawsHeading> theme </PawsHeading>
            <span> PawsSelect incomplete </span>
          </Field>

          <Field>
            <PawsHeading> accent </PawsHeading>
            <span> PawsColorPicker incomplete </span>
          </Field>
        </Card>
      </div>

      <div :class="styles.contentBlock">
        <Card title="global toggles">
          <Field>
            <PawsHeading>write confirmation</PawsHeading>
            <PawsMultiSwitch
              model-value="always ask"
              :options="['always ask', 'don\'t ask']"
              size="small"
            />
          </Field>

          <Field>
            <PawsHeading>launch at startup</PawsHeading>
            <PawsMultiSwitch
              model-value="yes please"
              :options="['yes please', 'no please']"
              size="small"
            />
          </Field>

          <Field>
            <PawsHeading>
              dev is cute (can he gather anon statistics?)
            </PawsHeading>
            <PawsMultiSwitch
              model-value="yes"
              :options="['yes', 'no sry']"
              size="small"
            />
          </Field>
        </Card>
      </div>
    </div>
  </div>
</template>
