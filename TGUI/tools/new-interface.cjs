// Generates a complete entity-bound example using the shared transport.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const name = process.argv[2];
if (!/^[A-Z][A-Za-z0-9]{0,63}$/.test(name || '')) {
  console.error('Usage: pnpm new:interface MyInterface (PascalCase name)');
  process.exit(1);
}
function containsDefinition(dir, pattern, extension) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const file = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (!['bin', 'obj', '.git'].includes(entry.name) && containsDefinition(file, pattern, extension)) return true;
    } else if (file.endsWith(extension) && pattern.test(fs.readFileSync(file, 'utf8'))) return true;
  }
  return false;
}
if (containsDefinition(path.join(root, 'Content.Shared'), new RegExp('\\bclass\\s+' + name + 'Component\\b'), '.cs') ||
    containsDefinition(path.join(root, 'Content.Server'), new RegExp('\\bclass\\s+' + name + 'System\\b'), '.cs') ||
    containsDefinition(path.join(root, 'Resources/Prototypes'), new RegExp('^\\s*id:\\s*Computer' + name + '(?:[\\s#]|$)', 'm'), '.yml')) {
  console.error('Component, system or entity ID already exists: ' + name);
  process.exit(1);
}
const files = new Map();
files.set(`TGUI/packages/tgui/interfaces/${name}.tsx`, fs.readFileSync(path.join(root, 'TGUI/packages/tgui/interfaces/DeepLagoonDemo.tsx'), 'utf8').replaceAll('DeepLagoonDemo', name));
files.set(`Content.Shared/_DeepLagoon/WebUI/Examples/${name}Component.cs`, `namespace Content.Shared._DeepLagoon.WebUI;\n\n[RegisterComponent]\npublic sealed partial class ${name}Component : Component\n{\n    public int Count;\n}\n`);
files.set(`Content.Server/_DeepLagoon/WebUI/Examples/${name}System.cs`, fs.readFileSync(path.join(root, 'Content.Server/_DeepLagoon/WebUI/WebUiDemoSystem.cs'), 'utf8')
  .replaceAll('WebUiDemoComponent', name + 'Component').replaceAll('WebUiDemoSystem', name + 'System').replaceAll('DeepLagoonDemo', name));
files.set(`Resources/Prototypes/_DeepLagoon/WebUI/Examples/${name}.yml`, fs.readFileSync(path.join(root, 'Resources/Prototypes/_DeepLagoon/WebUI/demo.yml'), 'utf8')
  .replaceAll('ComputerTguiDemo', 'Computer' + name).replace('type: WebUiDemo', 'type: ' + name).replace('TGUI demonstration console', name + ' console'));
// Check every destination before writing any; existing contributor work is never overwritten.
for (const file of files.keys()) {
  if (fs.existsSync(path.join(root, file))) { console.error('Already exists: ' + file); process.exit(1); }
}
for (const [file, content] of files) {
  const target = path.join(root, file);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, content);
  console.log(file);
}
console.log(`Preview: tgui_preview ${name}. Server example: spawn Computer${name}. Build pnpm build and the normal content projects.`);
