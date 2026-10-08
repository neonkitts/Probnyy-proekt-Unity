using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Deterministic, self-contained 2D platformer. All gameplay uses world units, independent of resolution.
public partial class ChibiGame : MonoBehaviour
{
    public const float FullLength = 4800, Speed = 5.1f, Gravity = 20, JumpSpeed = 8.2f;
    public enum Mode { Menu, Story, Options, Play, Pause, Dead, Loading, Ending }
    [Serializable] public class SaveData { public int seed, lives, hp, collected; public float checkpoint, elapsed, length; public List<int> taken = new List<int>(); public List<int> cut = new List<int>(); }
    public struct Block { public Rect r; public int type; public Block(float x,float y,float w,float h,int t) { r=new Rect(x,y,w,h);type=t; } }
    public class Vine { public Rect r; public bool cut, hanging; public Vector2 anchor; public float phase; }
    public class Pterodactyl { public float x, baseY, phase, fade; public bool defeated, hit; }
    public List<Block> blocks = new List<Block>();
    public List<Vine> vines = new List<Vine>();
    public List<Pterodactyl> pterodactyls = new List<Pterodactyl>();
    public List<Vector2> nectarBottles = new List<Vector2>();
    public List<Vector2> motes = new List<Vector2>();
    public HashSet<int> taken = new HashSet<int>();
    readonly Dictionary<string,Texture2D> art = new Dictionary<string,Texture2D>();
    readonly string[] chapters = { "Лунная пристань", "Сады перевёрнутых шагов", "Шёпот голубых ив", "Персиковые сны", "Последний фонарь" };
    public Mode mode;
    public int seed, lives=3, hp=100, collected;
    public float length=FullLength, px=4, py, vx, vy, checkpoint=4, elapsed;
    float cameraX, still, owlTime=-1, owlX, invuln, attack, attackCooldown, coyote, jumpBuffer, bubble, toastTime, intro, loading, flash, musicClock;
    float accumulator, lastPadVertical; int facing=1; bool grounded, crouch, owlHit, muted, shortRoute;
    string toast=""; Font body,title; Texture2D circle,sky; AudioSource music,sfx; AudioClip jumpSound,hitSound,chime,swish,burp;
    string savePath; bool capture; float captureAt; string capturePath; bool autoRun; int autoStage; float autoClock;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot() { if(FindAnyObjectByType<ChibiGame>()==null) new GameObject("Chibi • Game").AddComponent<ChibiGame>(); }
    void Awake()
    {
        Application.targetFrameRate=60; QualitySettings.vSyncCount=1;
        if(Camera.main==null) { var c=new GameObject("Camera").AddComponent<Camera>();c.tag="MainCamera";c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=Color.black; }
        foreach(var n in new[]{"fairy_idle","fairy_walk1","fairy_walk2","fairy_jump","fairy_crouch","fairy_attack","fairy_hurt","fairy_sleep","forest","flora","earth","peach","mint","home","vine","owl","lantern"}) art[n]=Resources.Load<Texture2D>("Art/"+n);
        body=Resources.Load<Font>("Fonts/Pixel");title=body;
        MakePixelEnemies();
        circle=new Texture2D(64,64,TextureFormat.RGBA32,false);var cc=new Color[4096];for(int y=0;y<64;y++)for(int x=0;x<64;x++)cc[y*64+x]=new Color(1,1,1,Mathf.Clamp01((31-Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f)))*.8f));circle.SetPixels(cc);circle.Apply();
        sky=new Texture2D(1,256);for(int y=0;y<256;y++)sky.SetPixel(0,y,Color.Lerp(C("#c4a1c5"),C("#292441"),y/255f));sky.Apply();
        music=gameObject.AddComponent<AudioSource>();sfx=gameObject.AddComponent<AudioSource>();music.loop=true;music.volume=.32f;sfx.volume=.32f;
        music.clip=Resources.Load<AudioClip>("Audio/MoonlitReel");if(music.clip)music.Play();jumpSound=Tone(510,810,.18f);hitSound=Tone(180,65,.24f);chime=Tone(740,1100,.22f);swish=Tone(300,110,.12f);burp=Tone(130,70,.15f);
        // Keep saves beside the standalone on the project drive: C: may be full.
        savePath=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Chibi-v2.save.json");
        var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length;i++) { if(args[i]=="--capture"&&i+1<args.Length){capture=true;capturePath=args[++i];captureAt=3;} if(args[i]=="--autotest")autoRun=true; }
        if(capture||autoRun)savePath=Path.Combine(Path.GetTempPath(),"Chibi-runtime-check.save.json");
        Debug.Log("FAIRY_BEER_VERSION "+Application.version+" input=separate-keyboard-gamepad");
        LoadPreferences();mode=Mode.Menu; Generate(1987,FullLength); cameraX=0;
        if(capture||autoRun){NewGame(1987,FullLength);intro=0;Debug.Log("CHIBI_V2 obstacles="+blocks.FindAll(b=>b.type!=0).Count+" vines="+vines.Count+" musicSeconds="+(music.clip?music.clip.length:0));}
    }
    public void Generate(int mapSeed,float mapLength)
    {
        seed=mapSeed;length=mapLength;shortRoute=mapLength<FullLength;blocks.Clear();vines.Clear();pterodactyls.Clear();nectarBottles.Clear();motes.Clear();taken.Clear();
        var r=new System.Random(seed);float groundStart=-15;
        int count=Mathf.FloorToInt((length-36)/7.5f);
        
        for(int i=0;i<count;i++)
        {
            float x=14+i*7.5f+(float)r.NextDouble()*.45f;
            bool safe=Mathf.Abs(x-Mathf.Round(x/320)*320)<9;
            if(safe)continue;
            int type=i<3?i%2:r.Next(9);
            if(type==0) { float h=.7f+(float)r.NextDouble()*.25f;blocks.Add(new Block(x,0,1.4f,h,1)); }
            if(type==1) { blocks.Add(new Block(x,1.05f,2.8f,1.2f,2)); }
            if(type==2) { blocks.Add(new Block(groundStart,-4,x-groundStart,4,0));groundStart=x+1.65f+(float)r.NextDouble()*.45f; }
            if(type==3) { blocks.Add(new Block(x,0,1.1f,.55f,2));blocks.Add(new Block(x+2.7f,0,1.1f,.9f,1)); }
            if(type==4) { blocks.Add(new Block(x,1.05f,2.6f,1.2f,2));blocks.Add(new Block(x+4.7f,0,1.2f,.7f,1)); }
            if(type==5) { blocks.Add(new Block(x,0,1.3f,.4f,1));blocks.Add(new Block(x+1.3f,0,1.3f,.8f,2));blocks.Add(new Block(x+4.6f,0,1.1f,.55f,1)); }
            if(type==6) { blocks.Add(new Block(x,0,1.4f,.65f,3));blocks.Add(new Block(x+3.6f,0,1.1f,.85f,4)); }
            if(type==7) { blocks.Add(new Block(x,1.05f,1.2f,1.2f,4));blocks.Add(new Block(x+2,1.05f,1.2f,1.2f,4)); }
            if(type==8) { blocks.Add(new Block(x,0,.8f,.45f,4));blocks.Add(new Block(x+1.8f,0,.8f,.7f,4));blocks.Add(new Block(x+4.2f,0,1.2f,.5f,3)); }
            motes.Add(new Vector2(x+.7f,type==1||type==4?.55f:2.1f));
            if(type>=3)motes.Add(new Vector2(x+3.5f,2.2f));
        }
        blocks.Add(new Block(groundStart,-4,length+30-groundStart,4,0));
        // Attach vines to existing isolated obstacles. Never add emergency blocks:
        // a lower block underneath an overhang could otherwise make an impassable tunnel.
        var hosts=blocks.FindAll(b=>b.type!=0 && (b.r.y>0 || b.r.width>1));
        hosts.Sort((a,b)=>a.r.x.CompareTo(b.r.x));
        for(int i=0;i<9;i++)
        {
            var candidates=i%2==0?hosts.FindAll(b=>b.r.y>0):hosts;
            var host=candidates[Mathf.Clamp((int)((i+.5f)*candidates.Count/9),0,candidates.Count-1)].r;
            vines.Add(new Vine{r=host.y>0?new Rect(host.center.x-.22f,.12f,.44f,.44f):new Rect(host.x-.04f,host.yMax,host.width+.08f,.52f),hanging=host.y>0,anchor=new Vector2(host.center.x,host.y),phase=i*1.7f});
        }
        // The green pterodactyls patrol open gaps. They dive only when the fairy reaches their territory.
        for(float x=64+(float)r.NextDouble()*18;x<length-40;x+=52+(float)r.NextDouble()*31)
        {
            pterodactyls.Add(new Pterodactyl{x=x,baseY=3.2f+(float)r.NextDouble()*1.3f,phase=(float)r.NextDouble()*6.28f});
            if(r.NextDouble()>.42) pterodactyls.Add(new Pterodactyl{x=x+2.4f+(float)r.NextDouble()*2.2f,baseY=4.3f+(float)r.NextDouble()*1.1f,phase=(float)r.NextDouble()*6.28f});
        }
        for(float x=55+(float)r.NextDouble()*20;x<length-30;x+=145+(float)r.NextDouble()*55)
            nectarBottles.Add(new Vector2(x,.28f));
        blocks.Sort((a,b)=>a.r.x.CompareTo(b.r.x));
    }
    public void NewGame(int s,float len) {Generate(s,len);px=checkpoint=4;py=0;vx=vy=0;lives=3;hp=100;elapsed=0;collected=0;cameraX=0;still=0;owlTime=-1;invuln=2;intro=6;mode=Mode.Play;Save();}
    void Fresh() {tutorial=false;NewGame(unchecked(Environment.TickCount*397),shortRoute?1100:FullLength);mode=Mode.Story;intro=0;}
    void Save()
    {
        if(tutorial)return;
        try { var v=new SaveData{seed=seed,lives=lives,hp=hp,collected=collected,checkpoint=checkpoint,elapsed=elapsed,length=length,taken=new List<int>(taken)};for(int i=0;i<vines.Count;i++)if(vines[i].cut)v.cut.Add(i);File.WriteAllText(savePath,JsonUtility.ToJson(v,true)); } catch(Exception e){Debug.LogWarning("Save unavailable: "+e.Message);}
    }
    void Continue()
    {
        try {var v=JsonUtility.FromJson<SaveData>(File.ReadAllText(savePath));if(v.lives<1||v.length<100)throw new Exception("Invalid save");Generate(v.seed,v.length);lives=v.lives;hp=v.hp;collected=v.collected;elapsed=v.elapsed;checkpoint=px=v.checkpoint;py=vy=vx=0;cameraX=px-9;taken=new HashSet<int>(v.taken);foreach(int i in v.cut)if(i>=0&&i<vines.Count)vines[i].cut=true;mode=Mode.Play;invuln=2;still=0;owlTime=-1;intro=0;Notify("Фонарь помнит дорогу. С возвращением!");}catch{Fresh();}
    }
    void Notify(string s){toast=s;toastTime=4;}
    bool Held(KeyCode a,KeyCode b){return Input.GetKey(a)||Input.GetKey(b);}
    bool Down(KeyCode a,KeyCode b){return Input.GetKeyDown(a)||Input.GetKeyDown(b);}
    // Fairy Beer deliberately reverses horizontal input. Keyboard is kept separate from
    // Unity's default Horizontal axis, which otherwise also contains A/D and cancels it out.
    public static float MovementIntent(bool leftKey,bool rightKey,float stick)
    {
        if(leftKey || rightKey) return (leftKey?1f:0f)-(rightKey?1f:0f);
        return Mathf.Abs(stick)<.2f?0f:-Mathf.Clamp(stick,-1f,1f);
    }
    void Update()
    {
        float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
        if(Input.GetKeyDown(KeyCode.F11)){prefs.fullscreen=!Screen.fullScreen;ApplyPreferences();SavePreferences();}
        if(Input.GetKeyDown(KeyCode.M)){muted=!muted;AudioListener.volume=muted?0:1;}
        if(Input.GetKeyDown(KeyCode.Escape)){if(mode==Mode.Options){SavePreferences();mode=optionsReturn;}else if(mode==Mode.Play){mode=Mode.Pause;Save();}else if(mode==Mode.Pause)mode=Mode.Play;}
        if(mode==Mode.Loading){loading+=dt;if(loading>2.7f)mode=Mode.Ending;return;}
        if(mode==Mode.Ending && autoRun && autoStage==3){autoStage=4;StartCoroutine(FinishSmoke());}
        if(mode!=Mode.Play)return;
        if(tutorial)TickTutorial(dt);
        elapsed+=dt;intro=Mathf.Max(0,intro-dt);toastTime=Mathf.Max(0,toastTime-dt);flash=Mathf.Max(0,flash-dt);invuln=Mathf.Max(0,invuln-dt);attack=Mathf.Max(0,attack-dt);attackCooldown=Mathf.Max(0,attackCooldown-dt);
        float move=MovementIntent(Held(KeyCode.A,KeyCode.LeftArrow),Held(KeyCode.D,KeyCode.RightArrow),Input.GetAxisRaw("ChibiPadHorizontal"));
        float padVertical=Input.GetAxisRaw("ChibiPadVertical");
        bool duck=Held(KeyCode.W,KeyCode.UpArrow)||padVertical>.5f;
        bool padJumpPressed=padVertical<-.5f && lastPadVertical>=-.5f;
        lastPadVertical=padVertical;
        if(Down(KeyCode.S,KeyCode.DownArrow)||Input.GetKeyDown(KeyCode.JoystickButton0)||padJumpPressed)jumpBuffer=.15f;
        if(Down(KeyCode.Space,KeyCode.J)||Input.GetKeyDown(KeyCode.JoystickButton2))Attack();
        if(autoRun) AutoInput(dt,ref move,ref duck);
        bool obstructed=OverlapSolid(new Rect(px-.32f,py+.72f,.64f,.84f));crouch=duck||(crouch&&obstructed);
        if(move!=0)facing=move>0?1:-1;
        accumulator+=dt;while(accumulator>=1f/120){Step(1f/120,move);accumulator-=1f/120;}
        if(Mathf.Abs(vx)<.12f&&grounded)still+=dt;else still=0;
        if(still>4&&owlTime<0&&(!tutorial||tutorialTime>=96)){owlTime=0;owlX=px;owlHit=false;still=0;Play(hitSound);}
        if(owlTime>=0){owlTime+=dt;if(owlTime>=.48f&&!owlHit){owlHit=true;if(Mathf.Abs(px-owlX)<.95f){Damage(80);Notify("Подозрительная сова: −80. Не стой на месте!");}}if(owlTime>=1)owlTime=-1;}
        UpdateVines(dt);
        var pr=PlayerRect();foreach(var v in vines)if(!v.cut&&pr.Overlaps(v.r)){Damage(30);break;}
        foreach(var enemy in pterodactyls)
        {
            if(enemy.defeated) {enemy.fade+=dt;continue;}
            enemy.phase+=dt*2.4f;
            float dive=Mathf.Clamp01(1-Mathf.Abs(px-enemy.x)/8f);
            float ey=enemy.baseY-Mathf.Sin(enemy.phase)*.35f-dive*1.8f;
            var er=new Rect(enemy.x-.75f,ey-.35f,1.5f,.8f);
            if(attack>0 && new Rect(facing>0?px:px-1.8f,py+.1f,1.8f,1.8f).Overlaps(er)) { enemy.defeated=true; Play(chime); Notify("Птеродактиль улетел в туман"); }
            else if(!enemy.hit && pr.Overlaps(er)){enemy.hit=true;Damage(35);Notify("Зелёный птеродактиль: −35");}
            if(Mathf.Abs(px-enemy.x)>13) enemy.hit=false;
        }
        for(int i=0;i<motes.Count;i++)if(!taken.Contains(i)&&Vector2.Distance(new Vector2(px,py+.7f),motes[i])<1){taken.Add(i);collected++;Play(chime);if(collected%10==0){hp=Mathf.Min(100,hp+20);Notify("10 искорок: +20 здоровья");}}
        for(int i=0;i<nectarBottles.Count;i++)if(!taken.Contains(10000+i)&&Vector2.Distance(new Vector2(px,py+.6f),nectarBottles[i])<.8f){taken.Add(10000+i);hp=Mathf.Min(100,hp+30);Play(chime);Notify("Лунное пиво: +30 здоровья");}
        float next=Mathf.Floor(px/320)*320;if(next>checkpoint&&py>=-.01f){checkpoint=next;hp=Mathf.Min(100,hp+20);Save();Notify("Фонарь зажжён • +20 здоровья • путь сохранён");Play(chime);}
        bubble+=dt;if(bubble>=3){bubble=0;Play(burp);}
        cameraX=Mathf.Lerp(cameraX,Mathf.Clamp(px-9,0,length-22),1-Mathf.Exp(-5*dt));
        if(!tutorial&&px>=length-2&&py<1){mode=Mode.Loading;loading=0;try{File.Delete(savePath);}catch{}Play(chime);}
        if(tutorial)px=Mathf.Min(px,60);
        if(capture){captureAt-=dt;if(captureAt<=0){capture=false;ScreenCapture.CaptureScreenshot(capturePath);}}
    }
    public Rect PlayerRect(){return new Rect(px-.32f,py,.64f,crouch?.74f:1.58f);}
    bool OverlapSolid(Rect rect){foreach(var b in blocks)if(rect.Overlaps(b.r))return true;return false;}
    public void Step(float dt,float move)
    {
        coyote=grounded?.12f:Mathf.Max(0,coyote-dt);jumpBuffer=Mathf.Max(0,jumpBuffer-dt);
        if(jumpBuffer>0&&coyote>0&&!crouch){vy=JumpSpeed;grounded=false;coyote=0;jumpBuffer=0;Play(jumpSound);}
        vx=Mathf.MoveTowards(vx,move*(crouch?2.9f:Speed),dt*(grounded?36:22));
        px+=vx*dt;var pr=PlayerRect();foreach(var b in blocks)if(pr.Overlaps(b.r)){if(vx>0)px=b.r.xMin-.321f;else if(vx<0)px=b.r.xMax+.321f;vx=0;pr=PlayerRect();}
        px=Mathf.Max(.4f,px);vy-=Gravity*dt;py+=vy*dt;grounded=false;pr=PlayerRect();
        foreach(var b in blocks)if(pr.Overlaps(b.r)){if(vy<=0){py=b.r.yMax;grounded=true;}else py=b.r.yMin-pr.height-.001f;vy=0;pr=PlayerRect();}
        if(py< -6){Damage(50,true);if(mode==Mode.Play){px=checkpoint;py=0;vx=vy=0;invuln=2;still=0;Notify("Осторожно, край острова! −50 здоровья");}}
    }
    public void Damage(int amount,bool force=false)
    {
        if(tutorial){if(invuln<=0){invuln=1.4f;flash=.25f;Notify("Учебная площадка: жизни не тратятся");}return;}
        if(mode!=Mode.Play||(!force&&invuln>0))return;hp-=amount;invuln=1.4f;flash=.35f;Play(hitSound);
        if(hp<=0){lives--;if(lives<=0){hp=0;mode=Mode.Dead;try{File.Delete(savePath);}catch{}return;}hp=100;px=checkpoint;py=vy=vx=0;still=0;owlTime=-1;invuln=3;Notify("Зелье туманности… Дымка стала гуще");Save();}
    }
    public void Attack()
    {
        if(attackCooldown>0)return;attack=.28f;attackCooldown=.42f;Play(swish);Rect hit=new Rect(facing>0?px:px-1.8f,py+.1f,1.8f,1.8f);
        foreach(var v in vines)if(!v.cut&&hit.Overlaps(v.r)){v.cut=true;Play(chime);Notify("Лоза уступила дорогу");}
        foreach(var enemy in pterodactyls)if(!enemy.defeated&&hit.Overlaps(new Rect(enemy.x-.8f,enemy.baseY-2.1f,1.6f,2.3f))){enemy.defeated=true;Play(chime);Notify("Птеродактиль улетел в туман");}
    }
    void AutoInput(float dt,ref float move,ref bool duck)
    {
        // Built-in executable smoke journey, never enabled during ordinary play.
        autoClock+=dt;move=1;duck=false;
        foreach(var b in blocks)if(b.type!=0&&b.r.xMin>px&&b.r.xMin-px<2.4f){if(b.r.yMin>0)duck=true;else if(grounded)jumpBuffer=.15f;}
        foreach(var b in blocks)if(b.type==0&&px>b.r.xMin&&px<b.r.xMax&&b.r.xMax-px<1.4f&&grounded)jumpBuffer=.15f;
        foreach(var v in vines)if(!v.cut&&Mathf.Abs(v.r.center.x-px)<1.7f)Attack();
        if(autoClock>14&&autoStage==0){Debug.Log("CHIBI_SMOKE movement="+px+" hp="+hp);Damage(100,true);autoStage++;}
        if(autoClock>15&&autoStage==1){Damage(100,true);Debug.Log("CHIBI_SMOKE fog lives="+lives);autoStage++;}
        if(autoClock>16&&autoStage==2){px=length-1;py=0;Debug.Log("CHIBI_SMOKE finish reached");autoStage++;}
    }
    System.Collections.IEnumerator FinishSmoke()
    {
        yield return new WaitForEndOfFrame();
        string folder=Path.GetDirectoryName(capturePath ?? Path.Combine(Application.dataPath,"ending.png"));
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,"ending.png"));
        Debug.Log("CHIBI_SMOKE ending displayed");
        yield return new WaitForSecondsRealtime(1);
        mode=Mode.Menu;cameraX=0;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,"menu.png"));
        yield return new WaitForSecondsRealtime(1);
        OpenOptions(Mode.Menu);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,"settings.png"));
        yield return new WaitForSecondsRealtime(1);
        StartTutorial(false);tutorialTime=72;TickTutorial(0);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,"tutorial.png"));
        yield return new WaitForSecondsRealtime(1);
        tutorialTime=96;TickTutorial(0);px=10;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,"enemies.png"));
        yield return new WaitForSecondsRealtime(1);
        tutorial=false;mode=Mode.Menu;
        Debug.Log("CHIBI_SMOKE PASS");
        yield return new WaitForSecondsRealtime(1);
        Application.Quit();
    }
    void Play(AudioClip clip){if(sfx&&clip)sfx.PlayOneShot(clip);}
    AudioClip Tone(float a,float b,float seconds)
    {
        int n=(int)(22050*seconds);var data=new float[n];float phase=0;for(int i=0;i<n;i++){float t=i/(float)n;phase+=Mathf.Lerp(a,b,t)*2*Mathf.PI/22050;data[i]=Mathf.Sin(phase)*Mathf.Sin(t*Mathf.PI)*.3f;}var clip=AudioClip.Create("Chibi tone",n,1,22050,false);clip.SetData(data,0);return clip;
    }
    static Color C(string s){ColorUtility.TryParseHtmlString(s,out var c);return c;}
    void Fill(Rect r,Color c){GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;}
    void Ellipse(Rect r,Color c){GUI.color=c;GUI.DrawTexture(r,circle);GUI.color=Color.white;}
    void Pic(string n,Rect r,float alpha=1){GUI.color=new Color(1,1,1,alpha);if(art.TryGetValue(n,out var t)&&t)GUI.DrawTexture(r,t);GUI.color=Color.white;}
    void Text(string s,float x,float y,float w,float h,int size,Color color,bool serif=false,TextAnchor align=TextAnchor.UpperLeft)
    {
        var style=new GUIStyle{font=serif?title:body,fontSize=size,normal={textColor=color},alignment=align,wordWrap=true};
        if(serif){var shadow=new GUIStyle(style);shadow.normal.textColor=new Color(.06f,.025f,.12f,.9f);GUI.Label(new Rect(x+2,y+3,w,h),s,shadow);}
        GUI.Label(new Rect(x,y,w,h),s,style);
    }
    bool Button(string s,float x,float y,float w=300,bool primary=false)
    {
        Rect r=new Rect(x,y,w,50);bool hover=r.Contains(Event.current.mousePosition);Wood(r,!hover);
        Text(s,x+12,y+8,w-24,34,22,C("#793e32"),false,TextAnchor.MiddleCenter);return GUI.Button(r,GUIContent.none,GUIStyle.none);
    }
    Rect W(float x,float y,float w,float h){return new Rect(Mathf.Round((x-cameraX)*24)*2,Mathf.Round((550-(y+h)*48)/2)*2,Mathf.Round(w*24)*2,Mathf.Round(h*24)*2);}
    void OnGUI()
    {
        GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));
        DrawWorld(mode==Mode.Menu||mode==Mode.Story||mode==Mode.Options||mode==Mode.Ending);
        if(mode==Mode.Play||mode==Mode.Pause||mode==Mode.Dead) DrawHUD();
        if(tutorial&&mode==Mode.Play)DrawTutorial();
        if(mode==Mode.Menu) DrawMenu();
        if(mode==Mode.Story) DrawStory();
        if(mode==Mode.Options) DrawOptions();
        if(mode==Mode.Pause){Fill(new Rect(0,0,1280,720),new Color(.09f,.07f,.16f,.83f));Text("Тихая передышка",340,145,600,65,42,C("#f7dbcd"),true,TextAnchor.MiddleCenter);Text("Сова тоже отдыхает. Путь сохранён у последнего фонаря.",320,220,640,45,19,C("#c9c0d7"),false,TextAnchor.MiddleCenter);if(Button("Вернуться на тропу",490,290,300,true))mode=Mode.Play;if(Button("Настройки",490,355))OpenOptions(Mode.Pause);if(Button("Новая случайная карта",490,420))Fresh();if(Button("В главное меню",490,485)){Save();tutorial=false;mode=Mode.Menu;cameraX=0;}}
        if(mode==Mode.Dead){Fill(new Rect(0,0,1280,720),new Color(.1f,.08f,.16f,.85f));Text("Остров оказался крепче",290,180,700,75,40,C("#f7d7d7"),true,TextAnchor.MiddleCenter);Text("Все три жизни потеряны. Новая попытка — новая тропа.",310,265,660,60,20,C("#c6bed3"),false,TextAnchor.MiddleCenter);if(Button("Попробовать ещё раз",475,365,330,true))Fresh();if(Button("Главное меню",475,430,330)){mode=Mode.Menu;cameraX=0;}}
        if(mode==Mode.Loading){Fill(new Rect(0,0,1280,720),C("#262238"));Text("Дом уже близко…",340,285,600,90,38,C("#f5d1cc"),true,TextAnchor.MiddleCenter);Fill(new Rect(490,405,300,3),C("#51455e"));Fill(new Rect(490,405,300*Mathf.Clamp01(loading/2.7f),3),C("#f0cba9"));}
        if(mode==Mode.Ending)DrawEnding();
    }
    void DrawWorld(bool decorative)
    {
        float t=Time.unscaledTime;
        float panorama=(decorative?t*2:cameraX*5)%1280;
        int page=Mathf.FloorToInt((decorative?t*2:cameraX*5)/1280);
        for(int i=0;i<2;i++)
        {
            Rect bg=new Rect(i*1280-panorama,0,1280,720);
            if((page+i)%2!=0){var matrix=GUI.matrix;GUIUtility.ScaleAroundPivot(new Vector2(-1,1),bg.center);Pic("forest",bg);GUI.matrix=matrix;}
            else Pic("forest",bg);
        }
        Fill(new Rect(0,0,1280,720),new Color(.10f,.06f,.20f,decorative?.13f:.23f));
        if(decorative)
        {
            for(int i=0;i<11;i++)Pic("earth",new Rect(i*128,630,128,128));
            Pic("home",new Rect(635,5,625,625));
            Pic(mode==Mode.Ending?"fairy_sleep":"fairy_idle",mode==Mode.Ending?new Rect(855,526,104,104):new Rect(855,510,120,120));
            return;
        }
        foreach(var b in blocks)
        {
            if(b.r.xMax<cameraX-2||b.r.xMin>cameraX+34)continue;
            string a=b.type==0?"earth":b.type==1?"peach":"mint";
            if(b.type==0){float start=Mathf.Max(b.r.xMin,Mathf.Floor(cameraX/2.666667f)*2.666667f);for(float x=start;x<b.r.xMax&&x<cameraX+34;x+=2.666667f)Pic(a,W(x,-4,Mathf.Min(2.666667f,b.r.xMax-x),4));}
            else {var obstacle=W(b.r.x,b.r.y,b.r.width,b.r.height);Fill(new Rect(obstacle.x-2,obstacle.y-2,obstacle.width+4,obstacle.height+4),new Color(.05f,.03f,.11f,.8f));Pic(a,obstacle);
                if(b.type==3){Fill(new Rect(obstacle.x,obstacle.y,obstacle.width,10),C("#d585b5"));for(int k=0;k<4;k++)Fill(new Rect(obstacle.x+5+k*14,obstacle.y+3,5,4),C("#fff0be"));}
                if(b.type==4){for(int k=0;k<4;k++)Fill(new Rect(obstacle.x+4+k*9,obstacle.y+4,4,obstacle.height-8),C("#98e6ef"));}
            }
        }
        for(float fx=Mathf.Floor(cameraX/5)*5;fx<cameraX+34;fx+=5)
        {
            bool onGround=false;foreach(var g in blocks)if(g.type==0&&fx>g.r.xMin+.4f&&fx<g.r.xMax-1){onGround=true;break;}
            if(onGround)Pic("flora",W(fx,-.05f,.8f,.6f),.78f);
        }
        for(float x=Mathf.Floor(cameraX/320)*320;x<cameraX+35;x+=320)if(x>0){Pic("lantern",W(x-.4f,0,.9f,1.6f));if(x<=checkpoint)Ellipse(W(x-.2f,.9f,.5f,.5f),new Color(1,.85f,.55f,.3f));}
        foreach(var v in vines)if(!v.cut&&v.r.x>cameraX-2&&v.r.x<cameraX+34)DrawVine(v);
        foreach(var enemy in pterodactyls)
        {
            if((enemy.defeated&&enemy.fade>=.6f) || enemy.x<cameraX-2 || enemy.x>cameraX+34) continue;
            float dive=Mathf.Clamp01(1-Mathf.Abs(px-enemy.x)/8f);
            float ey=enemy.baseY-Mathf.Sin(enemy.phase)*.35f-dive*1.8f;
            Rect e=W(enemy.x-.9f,ey-.4f,1.8f,1.05f);
            if(enemy.defeated)e.y-=enemy.fade*130;
            DrawEnemy(e,enemy.phase,enemy.defeated?1-enemy.fade/.6f:1);
        }
        for(int i=0;i<nectarBottles.Count;i++)if(!taken.Contains(10000+i) && nectarBottles[i].x>cameraX-2 && nectarBottles[i].x<cameraX+34)
        {
            Rect b=W(nectarBottles[i].x-.13f,nectarBottles[i].y,.26f,.6f);
            Fill(new Rect(b.x,b.y+10,b.width,b.height-10),C("#a95d35"));Fill(new Rect(b.x+4,b.y+14,b.width-8,b.height-18),C("#f3be55"));Fill(new Rect(b.x+6,b.y,b.width-12,14),C("#d8ddba"));
        }
        for(int i=0;i<motes.Count;i++)if(!taken.Contains(i)&&motes[i].x>cameraX-2&&motes[i].x<cameraX+34){var p=motes[i];float yy=p.y+Mathf.Sin(t*2+i)*.10f;Ellipse(W(p.x-.16f,yy,.32f,.32f),new Color(1,.85f,.52f,.22f));Ellipse(W(p.x-.07f,yy+.09f,.14f,.14f),C("#fff0bd"));}
        if(length-cameraX<40)Pic("home",W(length-5,0,10,10));
        if(mode!=Mode.Loading){string pose=flash>0?"hurt":attack>0?"attack":crouch?"crouch":!grounded?"jump":Mathf.Abs(vx)>.3f?((int)(t*9)%2==0?"walk1":"walk2"):"idle";Rect p=W(px-1.18f,py-.10f,2.36f,2.36f);float alpha=invuln>0&&Mathf.Sin(t*25)<0?.45f:1;Ellipse(new Rect(p.x+13,p.y+p.height-6,p.width-26,5),new Color(.02f,.01f,.07f,.48f));if(facing<0){var old=GUI.matrix;GUIUtility.ScaleAroundPivot(new Vector2(-1,1),p.center);Pic("fairy_"+pose,p,alpha);GUI.matrix=old;}else Pic("fairy_"+pose,p,alpha);if(attack>0){Rect a=W(px+(facing>0?.4f:-1.8f),py+.7f,1.4f,.12f);Fill(a,new Color(.75f,1,.85f,attack*2));}}
        if(bubble<1.2f)for(int i=0;i<3;i++){float b=bubble+i*.19f;Rect r=W(px+.4f+b*.3f,py+1.4f+b,.12f+b*.07f,.12f+b*.07f);Ellipse(r,new Color(.75f,1,.9f,(1-b/1.8f)*.65f));}
        if(owlTime>=0){float y=6-Mathf.Sin(owlTime*Mathf.PI)*5;DrawEnemy(W(owlX-1.3f,y,2.6f,1.4f),Time.unscaledTime*2);if(owlTime<.5f)Text("!",W(owlX,2,1,1).x,W(owlX,2,1,1).y,40,40,34,C("#ffcf9d"),true);}
        for(int i=0;i<(prefs.particles?22:0);i++){float x=(i*83+t*(4+i%4))%1280;float y=330+(i*37)%280+Mathf.Sin(t*.7f+i)*15;Ellipse(new Rect(x,y,3,3),new Color(1,.85f,.7f,.4f));}
        if(lives<3){float a=(3-lives)*.115f;Fill(new Rect(0,0,1280,720),new Color(.62f,.63f,.68f,a));for(int i=0;i<6;i++)Ellipse(new Rect((i*283+t*12)%1650-350,190+Mathf.Sin(t*.25f+i)*130,650,220),new Color(.7f,.7f,.74f,a*.27f));}
        if(flash>0)Fill(new Rect(0,0,1280,720),new Color(.85f,.29f,.43f,flash*.5f));
    }
    void DrawHUD()
    {
        Color cream=C("#fae5db"),mutedText=C("#bbb3cc");
        Fill(new Rect(25,23,410,91),new Color(.13f,.11f,.21f,.85f));Text("FAIRY BEER",44,31,150,32,18,cream,true);Text(chapters[Mathf.Clamp((int)(px/length*5),0,4)],202,38,220,30,16,mutedText);
        Fill(new Rect(45,78,365,4),C("#5c4c68"));Fill(new Rect(45,78,365*Mathf.Clamp01(px/length),4),C("#edc4a5"));Text($"ДОМОЙ  {Mathf.FloorToInt(px/length*100)}%     •     ИСКОРКИ  {collected}",45,88,365,24,12,mutedText);
        Fill(new Rect(1010,23,245,107),new Color(.13f,.11f,.21f,.88f));Text("ЖИЗНИ",1027,34,85,24,12,mutedText);Text(new string('♥',lives)+new string('♡',3-lives),1120,28,120,36,28,C("#f5a9c5"));Text($"ЗДОРОВЬЕ   {hp} / 100",1027,70,210,23,14,cream);Fill(new Rect(1027,100,210,5),C("#59485e"));Fill(new Rect(1027,100,210*hp/100f,5),C("#a7d5b8"));
        Text($"{(int)elapsed/60:00}:{(int)elapsed%60:00}    •    ESC  пауза",760,37,225,30,15,mutedText,false,TextAnchor.MiddleRight);
        if(lives<3)Text("Зелье туманности  "+(3-lives)+" / 2",1008,138,247,30,14,C("#e3d9e9"),false,TextAnchor.MiddleRight);
        if(still>2.5f)Text("Не засыпай: сова уже рядом…",430,580,420,40,20,C("#ffe1b9"),false,TextAnchor.MiddleCenter);
        if(toastTime>0){Fill(new Rect(310,602,660,43),new Color(.18f,.14f,.25f,.94f));Text(toast,325,610,630,30,18,cream,false,TextAnchor.MiddleCenter);}
        if(intro>1&&!tutorial){float a=Mathf.Min(1,intro-1);Fill(new Rect(210,258,860,182),new Color(.08f,.035f,.15f,a*.92f));Text("Fairy Beer",250,276,780,56,41,new Color(.98f,.77f,.60f,a),true,TextAnchor.MiddleCenter);Text("Островок хмельной каракатицы",280,329,720,34,24,new Color(.98f,.85f,.72f,a),true,TextAnchor.MiddleCenter);Text("Фея Лира сбежала с лунного пира: её сакура-дом ждёт до рассвета.",250,374,780,25,16,new Color(.88f,.82f,.94f,a),false,TextAnchor.MiddleCenter);Text("Но остров путает шаги, а в бутылке осталось последнее хмельное зелье.",250,401,780,25,16,new Color(.88f,.82f,.94f,a),false,TextAnchor.MiddleCenter);}
    }
    void DrawMenu()
    {
        DrawSign();
        if(Button("Новая игра",200,450,208,true))Fresh();
        GUI.enabled=File.Exists(savePath);
        if(Button("Продолжить",424,450,208)) {tutorial=false;Continue();}
        GUI.enabled=true;
        if(Button("Настройки",648,450,208))OpenOptions(Mode.Menu);
        if(Button("Выход",872,450,208))Application.Quit();
        if(Button("Обучение",500,530,280))StartTutorial(false);
    }
    void DrawStory()
    {
        Fill(new Rect(0,0,1280,720),new Color(.045f,.02f,.11f,.60f));
        Fill(new Rect(78,72,610,576),new Color(.08f,.035f,.16f,.92f));
        Text("Fairy Beer",118,106,520,66,45,C("#ffd0ac"),true);
        Text("Ночь, когда Лира\nзаблудилась",118,177,500,85,30,C("#f0b9d5"),true);
        Text("Лира — домашняя фея старой сакуры. На лунном\nпиру в таверне она допоздна танцевала с бутылкой\nхмельного зелья и пропустила последний огонёк\nк своему дому.",118,284,510,105,18,C("#eee2f1"));
        Text("До рассвета ей нужно пересечь Островок хмельной\nкаракатицы. Ивы путают направление, цветущие лозы\nне пускают дальше, а зелёные птеродактили охотятся\nза теми, кто идёт один.",118,410,510,105,18,C("#ddd3ec"));
        Text("Бутылка — её последний свет, лекарство и оружие.",118,539,510,30,16,C("#ffd6a5"));
        if(Button("В путь • обучение 2 минуты",118,580,470,true))StartTutorial(true);
    }
    void DrawOptions()
    {
        Fill(new Rect(0,0,1280,720),new Color(.08f,.04f,.12f,.68f));
        Wood(new Rect(130,48,1020,626));
        Color ink=C("#713d35");
        Text("Настройки",169,73,750,60,40,ink);
        Text("Графика",169,150,390,36,28,ink);
        if(Button(prefs.fullscreen?"Полный экран":"Оконный режим",169,201,412)){prefs.fullscreen=!prefs.fullscreen;ApplyPreferences();}
        var r=resolutions[prefs.resolution];
        if(Button("Разрешение: "+r.x+" x "+r.y,169,262,412)){prefs.resolution=(prefs.resolution+1)%resolutions.Length;ApplyPreferences();}
        if(Button(prefs.vsync?"VSync: включён":"VSync: выключен",169,323,412)){prefs.vsync=!prefs.vsync;ApplyPreferences();}
        if(Button(prefs.particles?"Частицы: включены":"Частицы: выключены",169,384,412))prefs.particles=!prefs.particles;
        Text("Музыка",179,455,140,32,22,ink);
        music.volume=GUI.HorizontalSlider(new Rect(326,469,234,20),music.volume,0,.6f);
        if(Button(muted?"Звук: выключен":"Звук: включён",169,509,412)){muted=!muted;AudioListener.volume=muted?0:1;}
        Text("Управление",633,150,450,36,28,ink);
        string[] keys={"A / влево","D / вправо","S / вниз","W / вверх","Пробел / J","Esc","F11","M"};
        string[] actions={"Идти вправо","Идти влево","Прыжок","Пригнуться","Удар бутылкой","Пауза","Полный экран","Звук"};
        for(int i=0;i<keys.Length;i++){Text(keys[i],633,203+i*39,185,35,22,ink);Text(actions[i],839,203+i*39,270,35,22,ink);}
        Text("Фея пьяна: направления перепутаны!",633,533,440,50,20,ink);
        if(Button("Сохранить и назад",440,600,400,true)){SavePreferences();mode=optionsReturn;}
    }
    void DrawEnding()
    {
        Fill(new Rect(0,0,655,720),new Color(.12f,.10f,.20f,.92f));Text("РАССВЕТ НАД ОСТРОВОМ",65,91,560,35,13,C("#cdb1c8"));Text("Дом,\nмилый дом",59,164,570,180,61,C("#f5d4cb"),true);Text("Лира успела до рассвета. Лунный пир в таверне\nподождёт — сегодня она дома, под сакурой.",66,373,535,90,23,C("#cfc0d6"));Text($"Путь: {(int)elapsed/60} мин {(int)elapsed%60} сек  •  Искр: {collected}  •  Жизней: {lives}",66,488,540,40,18,C("#e8c9ad"));if(Button("Ещё одна сказка",66,568,370,true))Fresh();if(Button("Меню",66,630,180)){mode=Mode.Menu;cameraX=0;}
    }
    void OnApplicationFocus(bool focused)
    {
        if(!focused && mode==Mode.Play && !autoRun){mode=Mode.Pause;vx=0;jumpBuffer=0;lastPadVertical=0;Save();}
    }
    void OnApplicationQuit(){if(mode==Mode.Play||mode==Mode.Pause)Save();}
}

