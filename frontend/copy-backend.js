const fs = require("fs-extra");
const path = require("path");

const buildConfig = process.env.PAWS_BUILD_CONFIGURATION || "Debug";
const explicitPath = process.env.PAWS_BACKEND_PATH;
// If PAWS_BACKEND_PATH is set, use it. Otherwise construct path based on config.
// Note: constructing the path needs to be relative to __dirname unless absolute.
const defaultSource = `../Paws.DotNet/Paws.Host/bin/${buildConfig}/net8.0`;
const sourcePathToResolve = explicitPath || defaultSource;

const sourceDir = path.resolve(__dirname, sourcePathToResolve);
const targetDir = path.resolve(__dirname, "resources/Paws.Backend");

console.log(`Copying backend from: ${sourceDir}`);
console.log(`To: ${targetDir}`);

fs.emptyDirSync(targetDir);

try {
	fs.copySync(sourceDir, targetDir, {
		dereference: true,
		filter: (src, dest) => {
			return true;
		}
	});
	console.log("Successfully copied C# backend to resources directory.");
} catch (err) {
	console.error("Error copying C# backend:", err);
	process.exit(1);
}
