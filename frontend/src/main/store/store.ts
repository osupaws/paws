import type { Schema } from "electron-store";

export type StoreType = {
	isCompact: boolean;
	approvedPlugins: string[];
};

const schema: Schema<StoreType> = {
	isCompact: {
		type: "boolean",
		default: false
	},
	approvedPlugins: {
		type: "array",
		default: ["a1b2c3d4-e5f6-4a9b-8c7d-6e5f4a3b2c1d"]
	}
};

// eslint-disable-next-line @typescript-eslint/no-require-imports
const StoreInput = require("electron-store");
const Store = StoreInput.default || StoreInput;

export const store = new Store({ schema });
