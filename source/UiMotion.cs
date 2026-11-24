using System;
using System.Collections.Generic;
using System.Diagnostics;
namespace DS3CameraDLLTrial {
// Animation follows elapsed time; the UI timer runs only while a value is changing.
sealed class UiMotion {
 sealed class Track {public float From,To;public double Start,Duration;}
 readonly Stopwatch clock=Stopwatch.StartNew();readonly Dictionary<string,Track> tracks=new Dictionary<string,Track>();
 float At(Track t){double p=Math.Min(1,Math.Max(0,(clock.Elapsed.TotalMilliseconds-t.Start)/t.Duration));double ease=1-Math.Pow(1-p,3);return t.From+(t.To-t.From)*(float)ease;}
 public float Value(string id,float target,int duration=180){Track t;if(!tracks.TryGetValue(id,out t)){tracks[id]=new Track{From=target,To=target,Start=clock.Elapsed.TotalMilliseconds,Duration=duration};return target;}if(t.To!=target){t.From=At(t);t.To=target;t.Start=clock.Elapsed.TotalMilliseconds;t.Duration=duration;}return At(t);}
 public void Start(string id,float from,float target,int duration){tracks[id]=new Track{From=from,To=target,Start=clock.Elapsed.TotalMilliseconds,Duration=duration};}
 public bool Active{get{foreach(var t in tracks.Values)if(t.From!=t.To&&clock.Elapsed.TotalMilliseconds<t.Start+t.Duration)return true;return false;}}
 public void Finish(){foreach(var t in tracks.Values)t.From=t.To;}
}
}
