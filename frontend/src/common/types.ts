export interface Theme {
	id: string; // The globally unique ID, e.g., 'paws-dark' or 'my-custom-theme'
	name: string;
	author?: string;
	version?: string;
	base: string;
	file: {
		hash: string;
		size: number;
		extension: string;
	} | null;
	isCustom: boolean;
}
