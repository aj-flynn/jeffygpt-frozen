#!/usr/bin/env node
import { cp, lstat, mkdir, realpath } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { installAgents } from './scripts/install-agents.mjs';

const skillRoot = dirname(fileURLToPath(import.meta.url));
const entries = [
  'SKILL.md', 'README.md', 'INSTALL.md', 'USAGE.md', 'package.json', 'install.mjs',
  'assets', 'examples', 'references', 'subagents',
];
const scriptEntries = ['render.mjs', 'install-agents.mjs'];

async function inspect(path) {
  try { return await lstat(path); }
  catch (error) { if (error.code === 'ENOENT') return null; throw error; }
}

async function requireRealDirectory(path) {
  const stat = await inspect(path);
  if (!stat || stat.isSymbolicLink() || !stat.isDirectory()) {
    throw new Error(`Expected an existing, real directory: ${path}`);
  }
}

async function allowMissingRealDirectory(path) {
  const stat = await inspect(path);
  if (stat && (stat.isSymbolicLink() || !stat.isDirectory())) {
    throw new Error(`Expected a real directory or no entry: ${path}`);
  }
}

async function main() {
  const args = process.argv.slice(2);
  const check = args.includes('--check');
  const agents = args.includes('--agents');
  const projects = args.filter(arg => !arg.startsWith('--'));
  if (projects.length !== 1 || args.some(arg => arg.startsWith('--') && !['--check', '--agents'].includes(arg))) {
    throw new Error('Usage: node documancer/install.mjs <existing-project-root> [--agents] [--check]');
  }

  const requestedRoot = resolve(projects[0]);
  await requireRealDirectory(requestedRoot);
  const projectRoot = await realpath(requestedRoot);
  const agentsRoot = join(projectRoot, '.cursor');
  const skillsRoot = join(agentsRoot, 'skills');
  const destination = join(skillsRoot, 'documancer');
  await allowMissingRealDirectory(agentsRoot);
  await allowMissingRealDirectory(skillsRoot);
  const legacy = join(skillsRoot, 'forest-documentation');
  if (await inspect(legacy)) {
    throw new Error(`Previous skill is still installed; review and migrate it before installing Documancer: ${legacy}`);
  }
  if (await inspect(destination)) {
    throw new Error(`Skill destination already exists; review it before replacing: ${destination}`);
  }
  for (const entry of entries) if (!await inspect(join(skillRoot, entry))) {
    throw new Error(`Incomplete skill archive; missing ${entry}`);
  }
  for (const entry of scriptEntries) if (!await inspect(join(skillRoot, 'scripts', entry))) {
    throw new Error(`Incomplete skill archive; missing scripts/${entry}`);
  }
  const agentPlan = agents ? await installAgents(projectRoot, { check: true }) : [];
  if (check) {
    console.log(JSON.stringify({ skill: `would create ${destination}`, agents: agentPlan }, null, 2));
    return;
  }

  await mkdir(skillsRoot, { recursive: true });
  await allowMissingRealDirectory(agentsRoot);
  await allowMissingRealDirectory(skillsRoot);
  await mkdir(destination);
  for (const entry of entries) {
    await cp(join(skillRoot, entry), join(destination, entry), { recursive: true, force: false, errorOnExist: true });
  }
  await mkdir(join(destination, 'scripts'));
  for (const entry of scriptEntries) {
    await cp(join(skillRoot, 'scripts', entry), join(destination, 'scripts', entry), { force: false, errorOnExist: true });
  }
  const installedAgents = agents ? await installAgents(projectRoot) : [];
  console.log(JSON.stringify({ skill: destination, agents: installedAgents }, null, 2));
}

main().catch(error => { console.error(error.message); process.exitCode = 1; });
