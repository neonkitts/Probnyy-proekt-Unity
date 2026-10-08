from pathlib import Path
import numpy as np, wave, json
R=22050; eighth=60/126/2; bars=64; total=bars*6*eighth
out=np.zeros((int((total+2)*R),2),np.float32);rng=np.random.default_rng(741)
def note(midi,start,duration,amp,kind='reed',pan=0):
 f=440*2**((midi-69)/12);n=int(duration*R);t=np.arange(n)/R
 phase=2*np.pi*f*t+(.28)*np.sin(2*np.pi*5.3*t)*np.minimum(t*5,1)
 if kind=='reed':v=(np.sin(phase)+.26*np.sin(phase*2)+.12*np.sin(phase*3)+.05*np.sin(phase*4))*.65
 elif kind=='pluck':v=(np.sin(phase)+.25*np.sin(phase*2)+.08*np.sin(phase*4))*np.exp(-t*6)
 elif kind=='bell':v=(np.sin(phase)+.2*np.sin(phase*2.01)+.08*np.sin(phase*3))*np.exp(-t*4)
 else:v=np.sin(phase)+.13*np.sin(phase*2)
 env=np.minimum(t/.013,1)*np.minimum(np.maximum(duration-t,0)/.055,1)
 if kind=='bass':env*=np.exp(-t*2)
 v=(v*env*amp).astype(np.float32);k=max(0,int(start*R));end=min(k+n,len(out));v=v[:end-k]
 out[k:end,0]+=v*np.sqrt((1-pan)/2);out[k:end,1]+=v*np.sqrt((1+pan)/2)
def drum(start,kind,amp):
 dur=.15 if kind!='kick' else .22;n=int(dur*R);t=np.arange(n)/R
 if kind=='kick':v=np.sin(2*np.pi*(58*t+38*.027*(1-np.exp(-t/.027))))*np.exp(-t*22)
 elif kind=='snare':v=(rng.normal(0,.6,n)+.35*np.sin(2*np.pi*180*t))*np.exp(-t*32)
 else:
  noise=rng.normal(0,1,n);v=np.diff(noise,prepend=0)*np.exp(-t*65)*.25
 k=int(start*R);out[k:k+n]+=v[:,None]*amp
# Original 8-bar melody in A minor, with a raised leading tone at cadences.
melodies=[[76,79,81,83,81,79],[77,81,84,83,81,77],[76,79,84,83,79,76],[74,79,83,81,79,74],[77,81,86,84,81,77],[76,80,83,86,83,80],[81,79,76,74,72,71],[71,74,76,80,83,80]]
chords=[(45,[57,60,64]),(41,[57,60,65]),(48,[55,60,64]),(43,[55,59,62]),(38,[57,62,65]),(40,[56,59,64]),(45,[57,60,64]),(40,[56,59,64])]
for bar in range(bars):
 sec=bar//16; idx=bar%8; start=bar*6*eighth;root,triad=chords[idx];energy=[.8,1,.6,1][sec]
 for step in range(6):
  at=start+step*eighth + (0.007 if step%3==1 else 0)
  m=melodies[idx][step]
  if sec==1 and bar%4>=2:m+=12
  if sec==2:m=triad[step%3]+12
  if sec==3 and step in [1,4]:m+=12
  if not(sec==2 and step%3==2):note(m,at,eighth*(1.85 if sec==2 else .87),.19*energy,'bell' if sec==2 else 'reed',-.12)
  note(triad[step%3]+(12 if sec in [1,3] else 0),at,eighth*1.9,.085,'pluck',.42)
  if step in [0,3]:
   note(root if step==0 else root+7,at,eighth*2.5,.21,'bass',-.18);drum(at,'kick',.16*energy)
  if step in [2,5]:drum(at,'snare',.075*energy)
  if sec!=2:drum(at,'hat',.058*(1 if step%3==0 else .6))
 if bar%8==7:
  for k in range(4):note(83+k, start+5*eighth+k*eighth/4,eighth/2,.07,'bell',.3)
# Gentle stereo echo adds space without washing out articulation.
for seconds,gain in [(.119,.1),(.357,.075),(.713,.035)]:
 d=int(seconds*R);out[d:,0]+=out[:-d,1]*gain;out[d:,1]+=out[:-d,0]*gain
out=out[:int(total*R)];fade=int(.035*R);out[:fade]*=np.linspace(0,1,fade)[:,None];out[-fade:]*=np.linspace(1,0,fade)[:,None]
peak=float(np.abs(out).max());out*=.86/max(peak,.001)
path=Path(__file__).resolve().parents[1]/'Assets/Resources/Audio/MoonlitReel.wav'
with wave.open(str(path),'wb') as f:f.setnchannels(2);f.setsampwidth(2);f.setframerate(R);f.writeframes((out*32767).astype('<i2').tobytes())
print(json.dumps({'duration_seconds':round(total,2),'bars':bars,'sections':4,'peak':float(np.abs(out).max()),'rms':float(np.sqrt((out*out).mean()))}))

