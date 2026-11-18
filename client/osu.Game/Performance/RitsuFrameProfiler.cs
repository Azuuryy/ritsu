// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using osu.Framework.Platform;
using osu.Framework.Statistics;

namespace osu.Game.Performance
{
    internal sealed class RitsuFrameProfiler
    {
        private const int duration_seconds = 30;
        private readonly Storage storage;
        private readonly GameHost host;
        private FrameProfileCapture? capture;

        public RitsuFrameProfiler(Storage storage, GameHost host)
        {
            this.storage = storage;
            this.host = host;
        }

        public bool IsRunning => capture?.IsActive == true;

        public bool CanStart => capture == null;

        public async Task<string> Start(Action<Action> schedule, TimeSpan? duration = null)
        {
            if (!CanStart)
                return "A frame capture or export is already running.";

            TimeSpan captureDuration = duration ?? TimeSpan.FromSeconds(duration_seconds);
            var current = capture = new FrameProfileCapture(150_000, 150_000);
            var memory = new List<MemorySample>(duration_seconds * 2);
            using var process = Process.GetCurrentProcess();
            string name = $"capture-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
            var metadata = new
            {
                Build = typeof(RitsuFrameProfiler).Assembly.GetName().Version?.ToString(),
                OS = Environment.OSVersion.VersionString,
                LogicalProcessors = Environment.ProcessorCount,
                Renderer = host.ResolvedRenderer.ToString(),
                UpdateLimitHz = double.IsFinite(host.MaximumUpdateHz) ? host.MaximumUpdateHz : (double?)null,
            };

            try
            {
                await Task.Delay(1).ConfigureAwait(false);
                while (Stopwatch.GetElapsedTime(current.StartTimestamp) < captureDuration && current.IsActive)
                {
                    process.Refresh();
                    memory.Add(new MemorySample(Stopwatch.GetTimestamp(), GC.GetTotalMemory(false), process.WorkingSet64, GC.GetTotalPauseDuration().TotalMilliseconds));
                    await Task.Delay(500).ConfigureAwait(false);
                }

                var data = new { SchemaVersion = 1, Metadata = metadata, Trace = current.Snapshot(), Memory = memory };
                var dir = storage.GetStorageForDirectory(name);
                string json = JsonSerializer.Serialize(data);
                using (var stream = dir.GetStream("trace.json", FileAccess.Write, FileMode.CreateNew))
                using (var writer = new StreamWriter(stream))
                    writer.Write(json);
                using (var stream = dir.GetStream("report.html", FileAccess.Write, FileMode.CreateNew))
                using (var writer = new StreamWriter(stream))
                    writer.Write(report_header + json + report_footer);

                return $"Frame profile saved to {dir.GetFullPath("report.html")}";
            }
            finally
            {
                current.Dispose();
                schedule(() => capture = null);
            }
        }

        public void Stop() => capture?.Dispose();

        private sealed record MemorySample(long Timestamp, long ManagedBytes, long WorkingSetBytes, double TotalGcPauseMs);

        private const string report_header = """
            <!doctype html><html lang="en"><meta charset="utf-8"><title>Ritsu frame profile</title>
            <style>
              body{font:15px system-ui;max-width:1100px;margin:2rem auto;padding:0 1rem;background:#15141c;color:#eee}
              table{width:100%;border-collapse:collapse}td,th{padding:.35rem .6rem;text-align:left;border-bottom:1px solid #444}
              canvas{width:100%;height:220px;background:#201f2b}select,button{font:inherit}
              summary{cursor:pointer} pre{white-space:pre-wrap}
            </style>
            <h1>Ritsu frame profile</h1><p id="meta"></p><p id="loss"></p>
            <h2>Frame time</h2><label>Thread <select id="thread"></select></label>
            <canvas id="timeline" width="1100" height="220"></canvas>
            <p id="frame-detail" aria-live="polite">Click a frame in the timeline to inspect its operations.</p><table id="summary"></table>
            <h2>Frames over budget</h2><p id="budget"></p><table id="slow"></table>
            <h2>Operations</h2><p>Operations on other threads can overlap frames. Their durations are reported independently.</p>
            <table id="ops"></table><h2>Memory</h2><p>Managed heap (violet) and process working set (cyan), MiB.</p>
            <canvas id="memory" width="1100" height="220"></canvas><h2>Cache counters</h2>
            <p>Latest reported cache sizes and totals from instrumented call sites.</p><canvas id="cachechart" width="1100" height="220"></canvas><table id="caches"></table>
            <script type="application/json" id="data">
            """;

        private const string report_footer = """
            </script><script>
            const $=id=>document.getElementById(id), d=JSON.parse($('data').textContent), t=d.Trace;
            const ms=(n)=>n*1000/t.Frequency, at=n=>ms(n-t.StartTimestamp), fmt=n=>n.toFixed(2);
            const span=f=>ms(f.End-f.Start);
            $('meta').textContent=Object.entries(d.Metadata).map(([k,v])=>k+' '+v).join(' · ');
            $('loss').textContent=t.DroppedFrames+' dropped frames · '+t.DroppedOperations+' dropped operations';
            function table(id, heads, rows){
              const el=$(id); el.replaceChildren();
              for(const [i,r] of [heads,...rows].entries()){
                const tr=el.insertRow();
                for(const cell of r){const c=document.createElement(i?'td':'th'); c.textContent=cell;tr.append(c);}
              }
            }
            function group(items,key){
              const result=new Map();for(const item of items){const k=key(item);if(!result.has(k))result.set(k,[]);result.get(k).push(item);}return result;
            }
            const groups=group(t.Frames,f=>f.Thread);
            for(const name of groups.keys()) $('thread').add(new Option(name,name));
            const end=Math.max(1,...Array.from(groups.values(),g=>at(g.at(-1).End)),...d.Memory.map(m=>at(m.Timestamp)));
            function plot(id, series){
              const canvas=$(id), ctx=canvas.getContext('2d'), w=canvas.width, h=canvas.height;
              ctx.clearRect(0,0,w,h);
              let max=1;for(const [,p] of series)for(const [,v] of p)max=Math.max(max,v);
              for(const [color,points] of series){
                ctx.strokeStyle=color;ctx.beginPath();let first=true;
                for(const [x,y] of points){const a=x/end*w,b=h-15-y/max*(h-30);if(first)ctx.moveTo(a,b);else ctx.lineTo(a,b);first=false;}
                ctx.stroke();
              }
              ctx.fillStyle='#ddd';ctx.fillText(fmt(max),6,12);ctx.fillText(fmt(end/1000)+' s',w-56,h-4);
            }
            $('timeline').onclick=e=>{
              const g=groups.get($('thread').value)??[], target=(e.offsetX/e.currentTarget.clientWidth)*end;
              if(!g.length)return;
              const f=g.reduce((best,v)=>Math.abs(at(v.End)-target)<Math.abs(at(best.End)-target)?v:best);
              const same=t.Operations.filter(o=>o.Value===null&&o.ManagedThreadId===f.ManagedThreadId&&o.Start>=f.Start&&o.Start+o.Duration<=f.End);
              const other=t.Operations.filter(o=>o.Value===null&&o.ManagedThreadId!==f.ManagedThreadId&&o.Start<f.End&&o.Start+o.Duration>f.Start);
              $('frame-detail').textContent='Frame '+f.FrameIndex+' at '+fmt(at(f.Start))+' ms. On-thread: '+
                (same.map(o=>o.Name+' '+fmt(ms(o.Duration))+' ms').join(', ')||'none instrumented')+
                '. Overlapping on other threads: '+(Array.from(new Set(other.map(o=>o.Name))).join(', ')||'none instrumented')+'.';
            };
            const pct=(a,p)=>a[Math.max(0,Math.ceil(p*a.length)-1)]??0;
            table('summary',['Thread','Frames','p50 ms','p95 ms','p99 ms','Allocated bytes'],Array.from(groups,([n,g])=>{
              const a=g.map(span).sort((a,b)=>a-b);
              return [n,g.length,...[.5,.95,.99].map(p=>fmt(pct(a,p))),g.reduce((s,f)=>s+f.AllocatedBytes,0)];
            }));
            function show(){
              const g=groups.get($('thread').value)??[];
              plot('timeline',[['#ad90ff',g.map(f=>[at(f.End),span(f)])],['#75d6e3',g.map(f=>[at(f.End),f.WorkMs+f.SchedulerMs])]]);
              const slow=g.filter(f=>f.BudgetMs>0&&span(f)>f.BudgetMs).sort((a,b)=>span(b)-span(a));
              $('budget').textContent=slow.length+' over budget. Plot shows elapsed time (violet) and measured work plus scheduler (cyan).';
              table('slow',['Time ms','Frame','Produced frame','Elapsed ms','Budget ms','Work ms','Scheduler ms','Wait ms','Swap ms','GC ms'],slow.slice(0,50)
                .map(f=>[fmt(at(f.Start)),f.FrameIndex,f.ProducedFrame||'—',...[span(f),f.BudgetMs,f.WorkMs,f.SchedulerMs,f.SleepMs,f.SwapMs,f.GcMs].map(fmt)]));
            }
            $('thread').onchange=show;show();
            const ops=group(t.Operations.filter(o=>o.Value===null),o=>o.Name);
            table('ops',['Operation','Calls','Total ms','Max ms','Threads'],Array.from(ops,([n,g])=>
              [n,g.length,ms(g.reduce((s,o)=>s+o.Duration,0)),fmt(g.reduce((v,o)=>Math.max(v,ms(o.Duration)),0)),Array.from(new Set(g.map(o=>o.ManagedThreadId))).join(', ')])
              .sort((a,b)=>b[2]-a[2]).map(r=>[r[0],r[1],fmt(r[2]),r[3],r[4]]));
            const mib=1024**2;
            plot('memory',[['#ad90ff',d.Memory.map(m=>[at(m.Timestamp),m.ManagedBytes/mib])],
              ['#75d6e3',d.Memory.map(m=>[at(m.Timestamp),m.WorkingSetBytes/mib])]]);
            const caches=group(t.Operations.filter(o=>o.Value!==null),o=>o.Name);
            const colors=['#ad90ff','#75d6e3','#f8b26c'];
            plot('cachechart',Array.from(caches).filter(([n])=>n.endsWith('.size')).map(([n,g],i)=>
              [colors[i%colors.length],g.map(o=>[at(o.Start),o.Value])]));
            table('caches',['Counter','Value'],Array.from(caches,([n,g])=>[n,n.endsWith('.size')?g.at(-1).Value:g.reduce((a,o)=>a+o.Value,0)]));
            </script></html>
            """;
    }
}
