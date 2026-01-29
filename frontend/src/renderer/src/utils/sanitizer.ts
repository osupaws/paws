import DOMPurify from "isomorphic-dompurify";

/**
 * Sanitizes a raw SVG string to prevent XSS attacks while allowing standard SVG tags and attributes.
 * @param rawSvg The potentially unsafe SVG string.
 * @returns A sanitized SVG string safe for v-html.
 */
export function sanitizeSvg(rawSvg: string): string {
	return DOMPurify.sanitize(rawSvg, {
		USE_PROFILES: { svg: true, svgFilters: true },
		ADD_TAGS: ["use"],
		ADD_ATTR: ["href", "xlink:href", "target"]
	});
}
