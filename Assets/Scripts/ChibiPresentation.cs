using System;
using System.IO;
using UnityEngine;

public partial class ChibiGame
{
    [Serializable] class Preferences { public int resolution=1; public bool fullscreen=false, vsync=true, particles=true; public float volume=.32f; }
    Preferences prefs=new Preferences();
    Mode optionsReturn=Mode.Menu;
    readonly Vector2Int[] resolutions={new Vector2Int(960,540),new Vector2Int(1280,720),new Vector2Int(1600,900),new Vector2Int(1920,1080)};
    Texture2D[] enemyFrames;
    bool tutorial, tutorialStartsGame;
    float tutorialTime;
    int tutorialRoom;
    public static Rect SwingHitbox(Vector2 anchor,float phase)
    {
        float angle=Mathf.Sin(phase)*.92f;
        Vector2 tip=anchor+new Vector2(Mathf.Sin(angle),-Mathf.Cos(angle))*.77f;
        return new Rect(tip.x-.22f,tip.y-.22f,.44f,.44f);
    }
    void UpdateVines(float dt)
    {
        foreach(var v in vines)if(v.hanging&&!v.cut){v.phase+=dt*2.25f;v.r=SwingHitbox(v.anchor,v.phase);}
    }
    void Wood(Rect r,bool light=true)
    {
        Fill(r,C("#492737"));r=new Rect(r.x+4,r.y+4,r.width-8,r.height-8);Fill(r,C("#ac613e"));
        r=new Rect(r.x+4,r.y+4,r.width-8,r.height-8);Fill(r,C("#f2ba72"));
        r=new Rect(r.x+4,r.y+4,r.width-8,r.height-8);Fill(r,light?C("#ffda97"):C("#edb06b"));
        for(int i=0;i<4;i++){float x=i%2==0?r.x+2:r.xMax-6;float y=i<2?r.y+2:r.yMax-6;Fill(new Rect(x,y,4,4),C("#8b4d38"));}
    }
    void DrawSign()
    {
        // Deliberately square edges and integer-pixel wood grain.
        Wood(new Rect(200,92,880,310));
        for(int i=0;i<8;i++)Fill(new Rect(218,126+i*34,844,2),C("#edbc7d"));
        for(int i=0;i<30;i++)Fill(new Rect(230+(i*173)%810,120+(i*37)%266,18+i%4*9,2),C("#e8b179"));
        Text("FAIRY BEER",215,126,850,155,110,C("#9d4a35"),true,TextAnchor.MiddleCenter);
        Text("Островок хмельной каракатицы",235,293,810,55,30,C("#774137"),false,TextAnchor.MiddleCenter);
        for(int i=0;i<4;i++)Pic("flora",new Rect(i<2?172:1030,88+i%2*232,84,70));
    }
    void OpenOptions(Mode back){optionsReturn=back;mode=Mode.Options;}
    string PrefPath=>Path.Combine(autoRun?Path.GetTempPath():Directory.GetParent(Application.dataPath).FullName,"FairyBeer.settings.json");
    void LoadPreferences()
    {
        try{if(File.Exists(PrefPath))prefs=JsonUtility.FromJson<Preferences>(File.ReadAllText(PrefPath))??new Preferences();}catch{prefs=new Preferences();}
        prefs.resolution=Mathf.Clamp(prefs.resolution,0,resolutions.Length-1);ApplyPreferences();
    }
    void ApplyPreferences()
    {
        var r=resolutions[prefs.resolution];Screen.SetResolution(r.x,r.y,prefs.fullscreen?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed);
        QualitySettings.vSyncCount=prefs.vsync?1:0;Application.targetFrameRate=60;music.volume=Mathf.Clamp(prefs.volume,0,.6f);
    }
    void SavePreferences(){try{prefs.volume=music.volume;File.WriteAllText(PrefPath,JsonUtility.ToJson(prefs,true));}catch(Exception e){Debug.LogWarning(e.Message);}}
    void StartTutorial(bool startGame)
    {
        tutorial=true;tutorialStartsGame=startGame;tutorialTime=0;tutorialRoom=-1;mode=Mode.Play;
        lives=3;hp=100;elapsed=0;collected=0;length=70;intro=0;cameraX=0;toastTime=0;toast="";flash=0;accumulator=0;lastPadVertical=0;TickTutorial(0);
    }
    void FinishTutorial()
    {
        tutorial=false;
        if(tutorialStartsGame)NewGame(unchecked(Environment.TickCount*397),shortRoute?1100:FullLength);
        else{mode=Mode.Menu;Generate(1987,FullLength);cameraX=0;}
    }
    void TickTutorial(float dt)
    {
        tutorialTime+=dt;
        if(tutorialTime>=120){FinishTutorial();return;}
        int room=Mathf.Min(4,(int)(tutorialTime/24));
        if(room==tutorialRoom)return;tutorialRoom=room;
        blocks.Clear();vines.Clear();pterodactyls.Clear();motes.Clear();nectarBottles.Clear();taken.Clear();
        blocks.Add(new Block(-20,-4,110,4,0));px=checkpoint=4;py=vx=vy=0;cameraX=0;still=0;owlTime=-1;invuln=2;attack=attackCooldown=0;
        for(int i=0;i<5;i++)
        {
            float x=12+i*8;
            if(room==1)blocks.Add(new Block(x,0,1.3f,.8f,1));
            if(room==2)blocks.Add(new Block(x,1.05f,2.6f,1.2f,2));
            if(room==3){blocks.Add(new Block(x,1.05f,2.6f,1.2f,2));var anchor=new Vector2(x+1,1.05f);vines.Add(new Vine{hanging=true,anchor=anchor,r=SwingHitbox(anchor,0),phase=i});}
            if(room==4)pterodactyls.Add(new Pterodactyl{x=x,baseY=3.2f,phase=i});
            motes.Add(new Vector2(x+2,room==1?2.2f:.6f));
        }
    }
    void DrawTutorial()
    {
        string[] names={"1 / 5  Перепутанные шаги","2 / 5  Прыжок","3 / 5  Низкие проходы","4 / 5  Шаловливая лоза","5 / 5  Подозрительная сова"};
        string[] help={"A / стрелка влево — идти ВПРАВО. D / вправо — идти ВЛЕВО.\nПрогуляйся по площадке и собери искорки.","S / стрелка вниз — ПРЫЖОК. Перепрыгни персиковые блоки.\nУдерживай A, чтобы двигаться вперёд во время прыжка.","W / стрелка вверх — ПРИСЕСТЬ. Удерживай W и A.\nПройди под верхними блоками, затем отпусти W.","ПРОБЕЛ или J — удар бутылкой. Подойди и сруби лозу.\nЕё цветок качается и наносит урон: дождись удобного момента.","Двигайся: после 4 секунд покоя сова атакует сверху.\nУклоняйся от птеродактилей или отбивайся бутылкой."};
        Wood(new Rect(260,144,760,144));
        Text(names[tutorialRoom],282,157,680,34,25,C("#713d35"));
        Text(help[tutorialRoom],282,197,705,75,20,C("#713d35"));
        Text("ОБУЧЕНИЕ  "+Mathf.CeilToInt(120-tutorialTime)+" сек",490,305,310,35,22,C("#fff0cf"),false,TextAnchor.MiddleCenter);
        if(Button("Завершить обучение",480,642,320))FinishTutorial();
    }
    void MakePixelEnemies()
    {
        enemyFrames=new Texture2D[8];
        for(int frame=0;frame<8;frame++)
        {
            var tex=new Texture2D(48,32,TextureFormat.RGBA32,false);tex.filterMode=FilterMode.Point;tex.SetPixels(new Color[48*32]);
            Action<int,int,Color> dot=(x,y,c)=>{if(x>=0&&x<48&&y>=0&&y<32)tex.SetPixel(x,y,c);};
            Color outline=C("#293a49"),green=C("#64b976"),light=C("#b9e899"),wing=C("#479d98");
            int flap=new[]{25,28,25,20,12,8,12,20}[frame];
            for(int x=3;x<42;x++)
            {
                int distance=Math.Abs(x-23);int top=(int)Mathf.Lerp(18,flap,distance/20f);int bottom=16-distance/5;
                for(int y=Math.Min(top,bottom);y<=Math.Max(top,bottom);y++)dot(x,y,(y==top||y==bottom)?outline:(x%5==0?green:wing));
            }
            for(int y=10;y<=23;y++)for(int x=19;x<=28;x++)if((x-24)*(x-24)/30f+(y-17)*(y-17)/55f<1)dot(x,y,x<22?outline:green);
            for(int y=20;y<27;y++)for(int x=25;x<34;x++)dot(x,y,y==20||y==26||x==25?outline:light);
            for(int x=33;x<43;x++)for(int y=21;y<25-(x-33)/3;y++)dot(x,y,C("#dca766"));
            dot(31,24,outline);dot(30,24,Color.white);dot(29,27,green);dot(28,28,outline);
            for(int x=10;x<22;x++)dot(x,11+(x-10)/3,green);
            dot(22,9,outline);dot(25,9,outline);tex.Apply();enemyFrames[frame]=tex;
        }
    }
    void DrawEnemy(Rect r,float phase,float alpha=1)
    {
        GUI.color=new Color(1,1,1,alpha);GUI.DrawTexture(r,enemyFrames[(int)(phase*5)%8]);GUI.color=Color.white;
    }
    void DrawVine(Vine v)
    {
        if(!v.hanging){Pic("vine",W(v.r.x,v.r.y,v.r.width,v.r.height));return;}
        Vector2 end=v.r.center;
        for(int i=0;i<10;i++)
        {
            Vector2 p=Vector2.Lerp(v.anchor,end,i/9f);
            Fill(W(p.x-.035f,p.y,.07f,.10f),C("#6ecddd"));
            if(i%3==0)Fill(W(p.x-.10f,p.y,.20f,.07f),C("#99e5da"));
        }
        var r=W(v.r.x,v.r.y,v.r.width,v.r.height);
        Fill(new Rect(r.x+6,r.y,r.width-12,r.height),C("#e98ebf"));Fill(new Rect(r.x,r.y+6,r.width,r.height-12),C("#e98ebf"));
        Fill(new Rect(r.center.x-4,r.center.y-4,8,8),C("#fff2ad"));
    }
}
