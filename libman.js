const { execSync } = require("child_process");

const action = process.argv[2]; // "install" or "uninstall"
const lib = process.argv[3]; // library name

if (!action || !lib) {
  console.error("Usage: npm run lib <install|uninstall> <library>");
  process.exit(1);
}

if (!["install", "uninstall"].includes(action)) {
  console.error('Action must be "install" or "uninstall"');
  process.exit(1);
}

let cmd;

if (action === "install") {
  // Install needs provider and destination
  cmd = `powershell -Command "pushd IfsahApp\\Web; libman install ${lib} -p cdnjs -d wwwroot/lib/${lib}; popd"`;
} else {
  // Uninstall only needs library name
  cmd = `powershell -Command "pushd IfsahApp\\Web; libman uninstall ${lib}; popd"`;
}

try {
  execSync(cmd, { stdio: "inherit" });
} catch (err) {
  console.error(err.message);
  process.exit(1);
}
