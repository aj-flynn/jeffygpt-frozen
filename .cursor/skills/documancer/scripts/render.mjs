import {readFile, writeFile, mkdir} from 'node:fs/promises';
import {resolve, dirname, relative, isAbsolute} from 'node:path';
import {pathToFileURL} from 'node:url';

const assets = new URL('../assets/', import.meta.url);
const company = 'Flynn Technology LLC';
const escape = value => String(value).replace(/[&<>"']/g, char => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[char]));
const fail = (path, message) => { throw new Error(`${path}: ${message}`); };
function object(value, keys, path) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) fail(path, 'expected an object');
  for (const key of Object.keys(value)) if (!keys.includes(key)) fail(`${path}.${key}`, 'unknown field');
}
function string(value, path) { if (typeof value !== 'string' || !value.trim()) fail(path, 'expected a nonempty string'); }
function array(value, path, nonempty = true) { if (!Array.isArray(value) || (nonempty && !value.length)) fail(path, 'expected '+(nonempty?'a nonempty':'an')+' array'); }
function strings(value, path) { array(value,path);value.forEach((v,i)=>string(v,`${path}[${i}]`)); }
function href(value, path) {
  string(value,path);
  if (value !== value.trim() || /[\u0000-\u0020\u007f\\]/.test(value) || value.startsWith('//')) fail(path,'unsupported URL');
  if (/^[a-z][a-z0-9+.-]*:/i.test(value)) {
    let url; try { url = new URL(value); } catch { fail(path,'invalid URL'); }
    if (!['http:','https:','mailto:'].includes(url.protocol)) fail(path,'unsupported URL scheme');
    if (url.username || url.password) fail(path,'credentials must not be embedded in links');
  } else if (value.split(/[/?#]/)[0].includes(':')) fail(path,'invalid relative URL');
}
export function validate(doc) {
  object(doc,['schemaVersion','mode','title','lead','identity','overview','brand','related','sources','sections','setup','tests','evidence'],'document');
  if (doc.schemaVersion !== 1) fail('schemaVersion','expected 1');
  if (!['technical','user-guide','uat'].includes(doc.mode)) fail('mode','expected technical, user-guide or uat');
  for (const key of ['title','lead']) string(doc[key],key);
  object(doc.identity,['id','version','date','source','repositoryRoot','branch','commit','readiness'],'identity');
  for (const key of ['id','version','date','source']) string(doc.identity[key],`identity.${key}`);
  for (const [key,value] of Object.entries(doc.identity)) string(value,`identity.${key}`);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(doc.identity.date) || !Number.isFinite(Date.parse(doc.identity.date)) || new Date(doc.identity.date).toISOString().slice(0,10)!==doc.identity.date) fail('identity.date','expected a valid YYYY-MM-DD date');
  const ids=new Set(['overview','main','document-data','font-license','doc-search','theme-choice','theme-status','result-counts','generate-summary','summary-state','summary-text','print-summary']);const links=[];
  function id(value,path,pattern) {string(value,path);if(!pattern.test(value))fail(path,'invalid ID');if(ids.has(value.toLowerCase()))fail(path,'duplicate or reserved ID');ids.add(value.toLowerCase());}
  function link(value,path) {object(value,['label','href'],path);string(value.label,path+'.label');href(value.href,path+'.href');links.push(value.href);}
  function blocks(value,path,depth=0) {
    array(value,path);if(depth>12)fail(path,'block nesting exceeds 12 levels');
    value.forEach((b,i)=>{
      const p=`${path}[${i}]`;if(!b || typeof b!=='object')fail(p,'expected a block');
      const fields={paragraph:['text'],heading:['text'],list:['items'],steps:['items'],code:['text','language'],table:['caption','columns','rows'],callout:['title','text'],details:['title','blocks'],links:['items']}[b.type];
      if(!fields)fail(p+'.type','unsupported block type');object(b,['type',...fields],p);
      if(['paragraph','heading','code','callout'].includes(b.type))string(b.text,p+'.text');
      if(['callout','details'].includes(b.type))string(b.title,p+'.title');
      if(b.type==='code'&&b.language!==undefined)string(b.language,p+'.language');
      if(['list','steps'].includes(b.type))strings(b.items,p+'.items');
      if(b.type==='links'){array(b.items,p+'.items');b.items.forEach((x,j)=>link(x,`${p}.items[${j}]`));}
      if(b.type==='details')blocks(b.blocks,p+'.blocks',depth+1);
      if(b.type==='table'){string(b.caption,p+'.caption');strings(b.columns,p+'.columns');array(b.rows,p+'.rows');b.rows.forEach((row,j)=>{array(row,`${p}.rows[${j}]`);if(row.length!==b.columns.length)fail(p+'.rows','row width must match columns');row.forEach(v=>{if(typeof v!=='string')fail(p+'.rows','cells must be strings');});});}
    });
  }
  if(doc.brand!==undefined){object(doc.brand,['name','logo'],'brand');if(doc.brand.name!==undefined)string(doc.brand.name,'brand.name');if(doc.brand.logo!==undefined&&!['flynn','none'].includes(doc.brand.logo))fail('brand.logo','expected flynn or none');if(doc.brand.logo==='flynn'&&(doc.brand.name??company)!==company)fail('brand.logo','Flynn wordmark requires the Flynn company name');}
  for(const key of ['related','sources'])if(doc[key]!==undefined){array(doc[key],key,false);doc[key].forEach((v,i)=>link(v,`${key}[${i}]`));}
  blocks(doc.overview,'overview');
  if(doc.mode==='uat'){
    if(doc.sections!==undefined)fail('sections','not supported in UAT mode');
    for(const reserved of ['setup','evidence','summary'])ids.add(reserved);
    blocks(doc.setup,'setup');array(doc.tests,'tests');doc.tests.forEach((_,i)=>ids.add('test-'+i+'-notes'));
    doc.tests.forEach((t,i)=>{const p=`tests[${i}]`;object(t,['id','title','brief','prerequisites','steps','expected','problemSigns','reset','rehearsal'],p);id(t.id,p+'.id',/^[A-Za-z][A-Za-z0-9-]*$/);for(const k of ['title','brief','expected','problemSigns','reset','rehearsal'])string(t[k],p+'.'+k);blocks(t.prerequisites,p+'.prerequisites');strings(t.steps,p+'.steps');});
    if(doc.evidence!==undefined){array(doc.evidence,'evidence',false);doc.evidence.forEach((e,i)=>{const p=`evidence[${i}]`;object(e,['title','status','detail','history'],p);for(const k of ['title','detail'])string(e[k],p+'.'+k);if(!['passed','failed','blocked','untested'].includes(e.status))fail(p+'.status','invalid evidence status');if(e.history!==undefined)string(e.history,p+'.history');});}
  }else{
    for(const key of ['setup','tests','evidence'])if(doc[key]!==undefined)fail(key,'only supported in UAT mode');
    array(doc.sections,'sections');doc.sections.forEach((s,i)=>{const p=`sections[${i}]`;object(s,['id','title','blocks'],p);id(s.id,p+'.id',/^[a-z][a-z0-9-]*$/);string(s.title,p+'.title');blocks(s.blocks,p+'.blocks');});
  }
  const targets=new Set(['overview',...(doc.mode==='uat'?['setup','summary',...(doc.evidence?.length?['evidence']:[]),...doc.tests.map(t=>t.id)]:doc.sections.map(s=>s.id))]);
  for(const url of links)if(url.startsWith('#')){let target;try{target=decodeURIComponent(url.slice(1));}catch{fail('link','invalid encoded anchor');}if(!targets.has(target))fail('link',`unknown anchor ${url}`);}
  return doc;
}

const linkHTML=(l,newTab=false)=>`<a href="${escape(l.href)}"${newTab?' target="_blank" rel="noopener noreferrer"':''}>${escape(l.label)}${newTab?'<span class="sr-only"> (opens in a new tab)</span>':''}</a>`;
const listHTML=(items,ordered=false)=>`<${ordered?'ol class="steps"':'ul class="prose-list"'}>${items.map(x=>`<li>${escape(x)}</li>`).join('')}</${ordered?'ol':'ul'}>`;
function blockHTML(blocks,renderLink=linkHTML){return blocks.map(b=>{
  switch(b.type){
    case 'paragraph':return `<p>${escape(b.text)}</p>`;
    case 'heading':return `<h3>${escape(b.text)}</h3>`;
    case 'list':case 'steps':return listHTML(b.items,b.type==='steps');
    case 'code':return `<div class="code-example"><div class="code-toolbar"><span>${escape(b.language??'Code')}</span><button class="copy-code" type="button">Copy code</button></div><pre tabindex="0"><code>${escape(b.text)}</code></pre></div>`;
    case 'callout':return `<aside class="note"><strong>${escape(b.title)}</strong><p>${escape(b.text)}</p></aside>`;
    case 'details':return `<details><summary>${escape(b.title)}</summary>${blockHTML(b.blocks,renderLink)}</details>`;
    case 'links':return `<ul class="prose-list">${b.items.map(l=>`<li>${renderLink(l)}</li>`).join('')}</ul>`;
    case 'table':return `<div class="table-wrap" tabindex="0" role="region" aria-label="${escape(b.caption)}"><table><caption>${escape(b.caption)}</caption><thead><tr>${b.columns.map(c=>`<th scope="col">${escape(c)}</th>`).join('')}</tr></thead><tbody>${b.rows.map(row=>`<tr>${row.map((c,i)=>i===0?`<th scope="row">${escape(c)}</th>`:`<td>${escape(c)}</td>`).join('')}</tr>`).join('')}</tbody></table></div>`;
  }
}).join('\n');}
const section=(id,title,body)=>`<section id="${escape(id)}"><h2>${escape(title)}</h2>${body}</section>`;
const identityLabels={id:'Document',version:'Version',date:'Date',source:'Source identity',repositoryRoot:'Checkout',branch:'Branch',commit:'Commit',readiness:'Readiness'};
export async function renderDocument(input,{companions=[]}={}){
  const doc=validate(input);
  const allowed=companions.filter(c=>c.mode!==doc.mode && c.mode!=='uat');
  const related=allowed.map(c=>({href:c.href,label:doc.related?.find(l=>l.href===c.href)?.label??({'technical':'Technical reference','user-guide':'User guide'}[c.mode])}));
  const destination=l=>companions.find(c=>new URL(l.href,'https://document.invalid/').pathname===new URL(c.href,'https://document.invalid/').pathname && new URL(l.href,'https://document.invalid/').origin==='https://document.invalid');
  const renderLink=l=>{const target=destination(l);if(doc.mode!=='uat'&&target?.mode==='uat')return escape(l.label);return linkHTML(l,doc.mode==='uat'&&['technical','user-guide'].includes(target?.mode));};
  const blocks=items=>blockHTML(items,renderLink);
  const read=name=>readFile(new URL(name,assets),'utf8');
  const [tokens,base,forest,forms,theme,runtime,font,license]=await Promise.all([read('tokens.css'),read('base.css'),read('forest.css'),read('document.css'),read('theme.js'),read('document.js'),readFile(new URL('source-sans-3-variable.woff2',assets)),read('OFL.txt')]);
  const css=tokens.replace('url("fonts/source-sans-3-variable.woff2")',`url("data:font/woff2;base64,${font.toString('base64')}")`)+base.replace(/@import[^;]+;/g,'')+forest+forms;
  const brandName=doc.brand?.name??company;const showLogo=(doc.brand?.logo??(brandName===company?'flynn':'none'))==='flynn';
  let brand=escape(brandName);
  if(showLogo){const logos=await Promise.all(['dark','light'].map(async mode=>`<img class="brand-${mode}" src="data:image/svg+xml;base64,${Buffer.from(await read(`flynn-wordmark-on-${mode}.svg`)).toString('base64')}" width="204" height="49" alt="${escape(brandName)}">`));brand=logos.join('');}
  const identityLines=Object.entries(doc.identity).map(([k,v])=>`${identityLabels[k]}: ${v}`).join('\n');
  let body=`<section id="overview" class="doc-intro"><h1>${escape(doc.title)}</h1><p class="lead">${escape(doc.lead)}</p><dl class="document-identity">${Object.entries(doc.identity).map(([k,v])=>`<div><dt>${identityLabels[k]}</dt><dd>${escape(v)}</dd></div>`).join('')}</dl><h2>Overview</h2>${blocks(doc.overview)}${doc.sources?.length?'<h3>Sources</h3><ul class="prose-list">'+doc.sources.map(l=>`<li>${renderLink(l)}</li>`).join('')+'</ul>':''}</section>`;
  const nav=[['overview','Overview']];
  if(doc.mode==='uat'){
    nav.push(['setup','Shared setup']);body+=section('setup','Shared setup',blocks(doc.setup)+'<aside class="note"><strong>Keep this page open</strong><p>Results and notes are held in this tab only. Generate and keep the summary before closing or reloading. Agent rehearsal and automated checks do not record human acceptance.</p></aside>');
    for(const [index,t] of doc.tests.entries()){
      nav.push([t.id,`${t.id} · ${t.title}`]);
      body+=section(t.id,`${t.id} · ${t.title}`,`<div class="test-case" data-test="${escape(t.id)}" data-title="${escape(t.title)}"><p>${escape(t.brief)}</p><h3>Prerequisites</h3>${blocks(t.prerequisites)}<h3>Test plan</h3>${listHTML(t.steps,true)}<p><strong>Pass when:</strong> ${escape(t.expected)}</p><p><strong>Problem signs:</strong> ${escape(t.problemSigns)}</p><p><strong>Reset or dependencies:</strong> ${escape(t.reset)}</p><p><strong>Agent rehearsal:</strong> ${escape(t.rehearsal)}</p><fieldset><legend>Result for ${escape(t.id)}</legend><div class="result-options">${['Passed','Failed','Skipped','Blocked','Untested'].map(s=>`<label><input type="radio" name="test-${index}-result" value="${s}" ${s==='Untested'?'checked':''}> ${s}</label>`).join('')}</div></fieldset><label class="notes-label" for="test-${index}-notes">Notes for ${escape(t.id)}</label><textarea id="test-${index}-notes" rows="3" placeholder="What happened? Include unexpected behavior or anything that prevented the test."></textarea><p class="print-result"></p><pre class="print-notes"></pre></div>`);
    }
    if(doc.evidence?.length){nav.push(['evidence','Automated evidence']);body+=section('evidence','Automated evidence','<p>Read-only verification; excluded from human acceptance totals.</p>'+doc.evidence.map(e=>`<div class="evidence-entry"><h3>${escape(e.title)}</h3><p><strong>${escape(e.status)}</strong> — ${escape(e.detail)}</p>${e.history?'<p><strong>History:</strong> '+escape(e.history)+'</p>':''}</div>`).join(''));}
    nav.push(['summary','Summary']);body+=section('summary','Summary','<p id="result-counts" aria-live="polite"></p><p>Failed or blocked tests need attention. Skipped and untested tests remain unaccepted.</p><button class="primary-button" id="generate-summary" type="button">Generate summary</button><p id="summary-state" role="status">Generating also copies the summary to your clipboard.</p><label class="notes-label" for="summary-text">Generated summary</label><textarea id="summary-text" rows="12" readonly placeholder="Your summary will appear here."></textarea><pre id="print-summary"></pre>');
  }else for(const s of doc.sections){nav.push([s.id,s.title]);body+=section(s.id,s.title,blocks(s.blocks));}
  const summaryData={title:doc.title,identity:identityLines,evidence:doc.evidence??[]};
  const safeJSON=JSON.stringify(summaryData).replace(/</g,'\\u003c').replace(/>/g,'\\u003e').replace(/&/g,'\\u0026');
  return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="generator" content="Documancer 1.3.0"><title>${escape(doc.title)} · ${escape(brandName)}</title><script>${theme}</script><style>${css}</style></head><body class="docs forest forest-handbook"><a class="skip" href="#main">Skip to documentation</a><header class="site-header"><a class="brand-link" href="#overview">${brand}</a><span class="header-title">Documentation</span>${related.length?'<nav class="document-links" aria-label="Related documents">'+related.map(l=>renderLink(l)).join('')+'</nav>':''}</header><main id="main" class="doc-layout"><aside class="sidebar"><div class="sidebar-inner"><div class="sidebar-scroll"><div class="collection-name">${escape(doc.title)}</div><label class="search-label" for="doc-search">Find in this document</label><input id="doc-search" type="search" placeholder="Search topics" autocomplete="off"><p class="search-feedback" aria-live="polite"></p><details class="handbook-menu" open><summary>Browse documentation</summary><nav aria-label="Documentation">${nav.map(([id,title])=>`<a href="#${escape(id)}">${escape(title)}</a>`).join('')}</nav></details></div><div class="theme-control"><label for="theme-choice">Appearance</label><select id="theme-choice" aria-describedby="theme-status"><option value="system">System</option><option value="dark">Dark</option><option value="light">Light</option></select><span id="theme-status" class="sr-only" aria-live="polite"></span></div></div></aside><article class="article"><noscript><p>Document content is available. Enable JavaScript for search, appearance controls, copying and UAT summary generation.</p></noscript>${body}</article></main><footer class="doc-footer"><span>${escape(brandName)}</span><span>${escape(doc.identity.id)} · ${escape(doc.identity.version)}</span><a href="#overview">Back to overview</a></footer><div class="live-message" role="status" aria-live="polite"></div><script type="application/json" id="document-data">${safeJSON}</script><script>${runtime}</script><script type="text/plain" id="font-license">${license.replace(/<\/script/gi,'<\\/script')}</script></body></html>`;
}

// A set is the publication boundary: only its members become companion links.
export async function renderSet(manifestPath,outputDirectory){
  const manifestFile=resolve(manifestPath),outputRoot=resolve(outputDirectory);
  const manifest=JSON.parse((await readFile(manifestFile,'utf8')).replace(/^\uFEFF/,''));
  object(manifest,['documents'],'set');array(manifest.documents,'set.documents');
  const outputs=new Set(),modes=new Set();const entries=[];
  for(const [i,entry] of manifest.documents.entries()){
    const p='set.documents['+i+']';object(entry,['source','output'],p);string(entry.source,p+'.source');string(entry.output,p+'.output');
    if(isAbsolute(entry.output)||entry.output.includes('\\')||entry.output.split('/').some(x=>x==='..'||x==='.'||!x)||!entry.output.endsWith('.html')||/[?#:%]/.test(entry.output))fail(p+'.output','expected a relative HTML path without traversal or URL delimiters');
    const key=entry.output.toLowerCase();if(outputs.has(key))fail(p+'.output','duplicate output');outputs.add(key);
    const input=resolve(dirname(manifestFile),entry.source),output=resolve(outputRoot,entry.output);
    if(input.toLowerCase()===output.toLowerCase())fail(p+'.output','cannot overwrite input');
    const doc=validate(JSON.parse((await readFile(input,'utf8')).replace(/^\uFEFF/,'')));
    if(modes.has(doc.mode))fail(p+'.source','a set supports one document per mode');modes.add(doc.mode);
    entries.push({doc,output});
  }
  const generated=await Promise.all(entries.map(async entry=>({output:entry.output,html:await renderDocument(entry.doc,{companions:entries.filter(e=>e!==entry).map(e=>({mode:e.doc.mode,href:relative(dirname(entry.output),e.output).replaceAll('\\','/').split('/').map(encodeURIComponent).join('/')}))})})));
  for(const entry of generated){await mkdir(dirname(entry.output),{recursive:true});await writeFile(entry.output,entry.html,'utf8');}
  return generated.map(e=>e.output);
}
if(process.argv[1]&&import.meta.url===pathToFileURL(resolve(process.argv[1])).href){
  try{
    if(process.argv[2]==='--set'){
      if(process.argv.length!==5)throw new Error('Usage: node render.mjs --set <set.json> <output-directory>');
      for(const output of await renderSet(process.argv[3],process.argv[4]))console.log('Rendered '+output);
    }else{
      if(process.argv.length!==4)throw new Error('Usage: node render.mjs <source.json> <output.html> OR --set <set.json> <output-directory>');
      const input=resolve(process.argv[2]),output=resolve(process.argv[3]);
      if(input.toLowerCase()===output.toLowerCase())throw new Error('Input and output paths must differ');
      if(!output.toLowerCase().endsWith('.html'))throw new Error('Output must have an .html extension');
      const doc=JSON.parse((await readFile(input,'utf8')).replace(/^\uFEFF/,''));
      const html=await renderDocument(doc);await mkdir(dirname(output),{recursive:true});await writeFile(output,html,'utf8');console.log('Rendered '+output);
    }
  }catch(error){console.error(error.message);process.exitCode=1;}
}
