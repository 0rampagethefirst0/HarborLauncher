"use client";
import { useEffect, useRef, useState } from "react";
import type { Manifest } from "./lib/manifest";
import demos from "../data/demos.json";

const mediaVersion = `home-1.6-${demos.Frames}-${demos.FramesPerSecond}`;
const entries = new Map(demos.Items.map(item => [item.Id, item]));
export default function NativeDemo({mod}:{mod:Manifest}) {
  const entry = entries.get(mod.id);
  const [playing,setPlaying] = useState(false), [frame,setFrame] = useState(0);
  const id=useRef("");
  useEffect(()=> { id.current=crypto.randomUUID(); const stop=(event:Event)=>{if((event as CustomEvent).detail!==id.current)setPlaying(false);};window.addEventListener("harbor-demo",stop);return()=>window.removeEventListener("harbor-demo",stop); },[]);
  useEffect(()=> {
    if(!playing)return;
    const stop=()=>{if(document.hidden||!document.hasFocus())setPlaying(false);};
    const started=performance.now();
    let clock=0,previous=-1; const tick=(now:number)=>{const next=Math.floor((now-started)/demos.FrameMilliseconds);if(next!==previous){previous=next;setFrame(next);}clock=requestAnimationFrame(tick);}; clock=requestAnimationFrame(tick);
    document.addEventListener("visibilitychange",stop);window.addEventListener("blur",stop);
    return()=>{cancelAnimationFrame(clock);document.removeEventListener("visibilitychange",stop);window.removeEventListener("blur",stop);};
  },[playing]);
  function start(){if(!entry||matchMedia("(prefers-reduced-motion: reduce)").matches)return;window.dispatchEvent(new CustomEvent("harbor-demo",{detail:id.current}));setFrame(0);setPlaying(true);}
  if(mod.previewImage)return <div className="native-demo"><img src={mod.previewImage} alt={`${mod.name} preview`} loading="lazy"/></div>;
  if(!entry)return <div className="native-demo missing-preview"><span>Creator preview not supplied</span><small>{mod.kind === "widget" ? "Open this plugin in Harbor to preview its layout." : "Add a banner in the creator to show your changes."}</small></div>;
  const index=frame%(demos.Frames+60), display=Math.min(index,demos.Frames-1), x=display%demos.Columns, y=Math.floor(display/demos.Columns);
  return <div className="native-demo" onMouseEnter={start} onMouseLeave={()=>setPlaying(false)} onFocus={start} onBlur={()=>setPlaying(false)}>
    <img src={"/previews/"+entry.Poster+"?v="+mediaVersion} alt={entry.Caption} loading="lazy"/>
    {playing&&<div aria-hidden="true" className="demo-frames" style={{backgroundImage:`url('/previews/${entry.Film}?v=${mediaVersion}')`,backgroundSize:`${demos.Columns*100}% ${(demos.Frames/demos.Columns)*100}%`,backgroundPosition:`${x/(demos.Columns-1)*100}% ${y/(demos.Frames/demos.Columns-1)*100}%`}}/>}
    <span className="demo-play-label">{playing?`${demos.FramesPerSecond} FPS preview`:"Hover to play"}</span>
  </div>;
}
