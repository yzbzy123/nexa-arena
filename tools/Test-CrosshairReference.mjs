import fs from 'node:fs'
import vm from 'node:vm'
import { createRequire } from 'node:module'
const require=createRequire(import.meta.url)
const ts=require('../web-ui/node_modules/typescript/lib/typescript.js')
const audit=JSON.parse(fs.readFileSync(new URL('./cs2-crosshair-audit.json',import.meta.url),'utf8'))
const referenceSource=await (await fetch(audit.references[1])).text()
const codecPart=referenceSource.slice(0,referenceSource.indexOf('// ---- Viewer resolution'))
if(!codecPart.includes('function decodeCs2Crosshair') || !codecPart.includes('function encodeCrosshair'))throw Error('Reference schema changed')
const codec={};vm.runInNewContext(codecPart+';this.parse=decodeCrosshair;this.encode=encodeCrosshair;',codec,{timeout:1500})
const render={window:{}}
const renderSource=await(await fetch(audit.references[2])).text()
vm.runInNewContext(renderSource,render,{timeout:1500})
const compiled=ts.transpileModule(fs.readFileSync(new URL('../web-ui/src/lib/crosshair-renderer.ts',import.meta.url),'utf8'),{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText
const ours={exports:{}};vm.runInNewContext(compiled,ours)
let cases=0
for(const preset of audit.presets){
  const old=codec.parse(preset.oldCode),current=codec.parse(preset.code)
  if(codec.encode(old)!==preset.code)throw Error('Current encoding mismatch: '+preset.name)
  if(codec.encode(current)!==preset.code)throw Error('Current roundtrip mismatch: '+preset.name)
  const d={screenHeight:current.screenHeight,style:current.style,r:current.red,g:current.green,b:current.blue,a:current.alpha,
    outlineR:current.outlineRed,outlineG:current.outlineGreen,outlineB:current.outlineBlue,outlineA:current.outlineAlpha,
    thickness:current.thickness,outlineMode:current.outlineMode,gap:current.gap,length:current.length,
    centerDot:current.centerDotEnabled,tStyle:current.tStyleEnabled,dynamicSpreadLimit:current.dynamicSpreadLimit,splitDistance:current.splitDistance}
  for(const height of [768,960,1080,1440])for(const outlineMode of [0,1,2])for(const centerDot of [false,true])for(const tStyle of [false,true]){
    const modified={...d,outlineMode,centerDot,tStyle}
    const actual=ours.exports.staticCrosshairPixels(ours.exports.rescaleCrosshair(modified,height),80)
    const cfg={...current,outlineMode,centerDotEnabled:centerDot,tStyleEnabled:tStyle}
    const ref=render.window.CS2Crosshair
    const built=ref.buildPrimitives(cfg,{screenHeight:height,cx:40,cy:40})
    const expected=ref.rasterize(built.prims,80,80,0,0,[32,41,54])
    const difference=actual.findIndex((value,index)=>value!==expected[index])
    if(actual.length!==expected.length||difference>=0)throw Error('Pixel geometry/color mismatch: '+preset.name+' '+height+' '+outlineMode+' '+centerDot+' '+tStyle+' at byte '+difference+' actual='+actual[difference]+' reference='+expected[difference])
    cases++
  }
}
console.log('PASS: eight current CS exports match the independent encoder; '+cases+' static pixel/color cases match the reference for the installed game depot.')
