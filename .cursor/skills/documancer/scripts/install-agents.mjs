import {lstat,readFile,mkdir,writeFile,realpath} from 'node:fs/promises';
import {resolve,join} from 'node:path';
import {pathToFileURL} from 'node:url';

export const agentNames=['documancer-map','documancer-analyze','documancer-write','documancer-review'];
const source=new URL('../subagents/',import.meta.url);
async function inspect(path){try{return await lstat(path)}catch(error){if(error.code==='ENOENT')return null;throw error}}
async function safeDirectory(path){const stat=await inspect(path);if(stat&&(stat.isSymbolicLink()||!stat.isDirectory()))throw new Error(`Expected a real directory, not a link or file: ${path}`)}

// Installs only namespaced Cursor agent files. No global or project config is modified.
export async function installAgents(projectRoot,{check=false}={}){
  const requested=resolve(projectRoot);await safeDirectory(requested);
  const root=await realpath(requested);
  const cursor=join(root,'.cursor'),destination=join(cursor,'agents');
  await safeDirectory(cursor);await safeDirectory(destination);
  for(const name of agentNames){
    const legacy=join(destination,name.replace('documancer-','forest-docs-')+'.md');
    if(await inspect(legacy))throw new Error(`Previous agent profile is still installed; review and migrate it first: ${legacy}`);
  }
  const plan=[];
  for(const name of agentNames){
    const text=await readFile(new URL(name+'.md',source),'utf8');
    if(!text.replaceAll('\r\n','\n').startsWith('---\n')||!text.includes(`name: ${name}`))throw new Error(`Invalid Cursor agent profile: ${name}`);
    const target=join(destination,name+'.md');const stat=await inspect(target);
    if(stat&&(stat.isSymbolicLink()||!stat.isFile()))throw new Error(`Refusing linked or non-file target: ${target}`);
    if(stat&&(await readFile(target,'utf8'))!==text)throw new Error(`Existing agent differs; review and merge it explicitly before installing: ${target}`);
    plan.push({target,text,action:stat?'unchanged':'create'});
  }
  // Preflight every conflict before creating any files. Exclusive writes also prevent silent overwrite.
  if(!check){
    await mkdir(destination,{recursive:true});await safeDirectory(cursor);await safeDirectory(destination);
    for(const item of plan)if(item.action==='create')await writeFile(item.target,item.text,{encoding:'utf8',flag:'wx'});
  }
  return plan.map(({target,action})=>({target,action:check?'would '+action:action}));
}

if(process.argv[1]&&import.meta.url===pathToFileURL(resolve(process.argv[1])).href){
  try{
    const args=process.argv.slice(2);const check=args[0]==='--check';if(check)args.shift();
    if(args.length!==1||args[0].startsWith('--'))throw new Error('Usage: node install-agents.mjs [--check] <existing-project-root>');
    console.log(JSON.stringify(await installAgents(args[0],{check}),null,2));
  }catch(error){console.error(error.message);process.exitCode=1;}
}
