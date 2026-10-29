import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';

const origin='http://127.0.0.1:5178', checks=[];
async function check(name,run){await run();checks.push({name,passed:true});}
const plugin={format:1,id:'local.verification-upload',name:'Round-trip theme',author:'Verification',version:'1.0.0',mainCategory:'Themes',subcategory:'Minimal',description:'A local-only community round-trip check.',kind:'theme',tags:['minimal','dark'],options:[{key:'accent',label:'Accent',type:'color',default:'#83DFD0'}]};
let cookie='';
async function post(mod,requestOrigin=origin){return fetch(origin+'/api/plugins',{method:'POST',headers:{'Content-Type':'application/json',Origin:requestOrigin,...(cookie?{Cookie:cookie}:{})},body:JSON.stringify(mod)});}
try {
  await check('Public homepage serves the category layout',async()=>{const r=await fetch(origin);assert.equal(r.status,200);assert.match(await r.text(),/A desktop that/);});
  await check('Anonymous uploads are rejected',async()=>assert.equal((await post(plugin)).status,401));
  await check('Catalog pagination rejects invalid input',async()=>assert.equal((await fetch(origin+'/api/catalog?page=-1')).status,400));
  const signIn=await fetch(origin+'/signin-with-chatgpt?return_to=/upload',{redirect:'manual'});cookie=signIn.headers.getSetCookie().map(c=>c.split(';')[0]).join('; ');assert.ok(cookie,'Local preview sign-in cookie');
  await check('Signed-in upload persists a real package',async()=>{const r=await post(plugin);assert.equal(r.status,200,await r.text());});
  await check('Public catalog contains the uploaded package and its tags',async()=>{const list=await(await fetch(origin+'/api/catalog?page=0')).json();const found=list.find(m=>m.id===plugin.id);assert.equal(found.mainCategory,'Themes');assert.deepEqual(found.tags,['minimal','dark']);});
  await check('Package downloads preserve the manifest',async()=>{const r=await fetch(origin+'/api/download?id='+plugin.id);assert.equal(r.status,200);assert.match(r.headers.get('content-disposition'),/\.json/);const found=await r.json();assert.equal(found.name,plugin.name);assert.equal(found.options[0].default,'#83DFD0');});
  await check('Creator can update their own package',async()=>{const r=await post({...plugin,version:'1.0.1'});assert.equal(r.status,200);const list=await(await fetch(origin+'/api/plugins',{headers:{Cookie:cookie}})).json();assert.equal(list.find(m=>m.id===plugin.id).version,'1.0.1');});
  for(const [name,patch] of [
    ['Unknown main categories are rejected',{mainCategory:'Invalid'}],
    ['Subcategories cannot cross main categories',{subcategory:'Windows'}],
    ['Built-in package IDs are protected',{id:'harbor.midnight'}],
    ['Unsupported executable capabilities are rejected',{kind:'tweak',format:3,component:'inject-dll'}],
    ['Malformed images are rejected',{previewImage:'data:image/png;base64,AAAA'}],
    ['Invalid widget sizes are rejected',{format:2,kind:'widget',component:'dashboard',layout:{type:'text',fontSize:800}}],
    ['Duplicate interactive keys are rejected',{format:2,kind:'widget',component:'dashboard',layout:{type:'column',children:[{type:'input',key:'x'},{type:'check',key:'x'}]}}],
  ]) await check(name,async()=>assert.equal((await post({...plugin,...patch})).status,400));
  await check('Cross-origin writes are rejected',async()=>assert.equal((await post(plugin,'https://another.example')).status,403));
  await check('Unsigned creator lists disclose no account data',async()=>assert.equal((await fetch(origin+'/api/plugins')).status,401));
  await check('Signed-in creator can remove their own test package',async()=>{const r=await fetch(origin+'/api/plugins?id='+plugin.id,{method:'DELETE',headers:{Cookie:cookie,Origin:origin}});assert.equal(r.status,200);const list=await(await fetch(origin+'/api/catalog')).json();assert.ok(!list.some(m=>m.id===plugin.id));});
  await check('Built-in downloads contain valid categorized presets',async()=>{const r=await fetch(origin+'/api/download?id=harbor.clock');assert.equal(r.status,200);const mod=await r.json();assert.equal(mod.MainCategory,'Tools');assert.ok(mod.Tags.length>=3);});
}catch(error){checks.push({name:'Community verification finished',passed:false,detail:String(error)});process.exitCode=1;}
finally{if(cookie)await fetch(origin+'/api/plugins?id='+plugin.id,{method:'DELETE',headers:{Cookie:cookie,Origin:origin}}).catch(()=>{});await writeFile('../checks/community-v0.5.json',JSON.stringify({localPreview:true,productionDataChanged:false,checks},null,2));console.log(JSON.stringify(checks));}
