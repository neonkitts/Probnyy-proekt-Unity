using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using System;
using System.IO;
using System.Linq;

public static class ChibiBuild
{
    [MenuItem("Chibi/Build Windows game")]
    public static void Build()
    {
        ChibiArtImport.Import();
        PlayerSettings.companyName="Moonwillow";PlayerSettings.productName="Fairy Beer";
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
        PlayerSettings.resizableWindow=true;PlayerSettings.bundleVersion="2.2.0";
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{UnityEngine.Rendering.GraphicsDeviceType.Direct3D11});
        var pixel=AssetImporter.GetAtPath("Assets/Resources/Fonts/Pixel.ttf") as TrueTypeFontImporter;
        if(pixel!=null){pixel.fontRenderingMode=FontRenderingMode.HintedRaster;pixel.SaveAndReimport();}
        ConfigureGamepadAxes();
        // Legacy keyboard API; this project has no external packages.
        var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var input=settings.FindProperty("activeInputHandler");if(input!=null){input.intValue=0;settings.ApplyModifiedProperties();}
        foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/Art"}))
        {var importer=AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;if(importer==null)continue;importer.npotScale=TextureImporterNPOTScale.None;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.maxTextureSize=2048;importer.SaveAndReimport();}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Directory.CreateDirectory("Assets/Scenes");
        var camera=new GameObject("Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.gameObject.AddComponent<AudioListener>();camera.clearFlags=CameraClearFlags.SolidColor;
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/Island.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Island.unity",true)};
        Validate();
        Directory.CreateDirectory("Builds/Windows");
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Island.unity"},locationPathName="Builds/Windows/FairyBeer.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        Debug.Log("CHIBI_BUILD "+result.summary.result+" bytes="+result.summary.totalSize);
        if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed");
    }
    [MenuItem("Chibi/Validate generation and gameplay")]
    public static void Validate()
    {
        var go=new GameObject("Test game");var game=go.AddComponent<ChibiGame>();int checks=0;
        Assert(ChibiGame.MovementIntent(true,false,-1)==1,"A/left must move right, even against legacy axis");checks++;
        Assert(ChibiGame.MovementIntent(false,true,1)==-1,"D/right must move left, even against legacy axis");checks++;
        Assert(ChibiGame.MovementIntent(true,true,0)==0,"opposite keys cancel");checks++;
        Assert(ChibiGame.MovementIntent(false,false,-1)==1,"left stick reversed");checks++;
        Assert(ChibiGame.MovementIntent(false,false,1)==-1,"right stick reversed");checks++;
        Assert(ChibiGame.MovementIntent(false,false,.1f)==0,"stick drift dead zone");checks++;
        Assert(ChibiGame.MovementIntent(false,false,0)==0,"idle input");checks++;
        // Reproduce held keyboard input through the same mapper used by Update.
        game.Generate(31,ChibiGame.FullLength);game.mode=ChibiGame.Mode.Play;game.px=4;game.py=game.vx=game.vy=0;
        for(int i=0;i<120;i++)game.Step(1f/120,ChibiGame.MovementIntent(true,false,-1));
        Assert(game.px>8,"A input actually advances position");checks++;
        float right=game.px;for(int i=0;i<120;i++)game.Step(1f/120,ChibiGame.MovementIntent(false,true,1));
        Assert(game.px<right-3,"D input actually moves position back");checks++;

        for(int seed=0;seed<100;seed++)
        {
            game.Generate(seed,ChibiGame.FullLength);
            Assert(game.blocks.Count(b=>b.type!=0)>650,"dense obstacle course");checks++;
            Assert(game.vines.Count>=6&&game.vines.Count<=10,"vine count");checks++;
            Assert(game.pterodactyls.Count>=60,"enemy flock density");checks++;
            Assert(game.nectarBottles.Count>=20,"healing pickup density");checks++;
            foreach(var b in game.blocks.Where(b=>b.type!=0)){Assert(b.r.y>0?b.r.y>=1.02f:b.r.height<=.96f,"obstacle clearance");checks++;}
            var ground=game.blocks.Where(b=>b.type==0).OrderBy(b=>b.r.x).ToArray();
            for(int i=1;i<ground.Length;i++){Assert(ground[i].r.xMin-ground[i-1].r.xMax<2.11f,"jumpable gaps");checks++;}
            float signature=game.blocks.Sum(b=>b.r.x+b.r.width);game.Generate(seed,ChibiGame.FullLength);Assert(Mathf.Abs(signature-game.blocks.Sum(b=>b.r.x+b.r.width))<.1f,"deterministic seed");checks++;
        }
        var swingLeft=ChibiGame.SwingHitbox(new Vector2(10,1.05f),-Mathf.PI/2);
        var swingRight=ChibiGame.SwingHitbox(new Vector2(10,1.05f),Mathf.PI/2);
        Assert(swingLeft.center.x<10&&swingRight.center.x>10,"swing crosses anchor");checks++;
        Assert(swingLeft.yMin>=0&&swingRight.yMax<1.05f,"vine stays below host and above ground");checks++;
        game.mode=ChibiGame.Mode.Play;game.lives=3;game.hp=100;game.checkpoint=4;game.Damage(100,true);Assert(game.lives==2&&game.hp==100,"first life");game.Damage(100,true);Assert(game.lives==1&&game.hp==100,"second life");game.Damage(100,true);Assert(game.mode==ChibiGame.Mode.Dead,"game over");checks+=3;
        game.Generate(31,ChibiGame.FullLength);game.px=4;game.py=0;game.vx=game.vy=0;game.mode=ChibiGame.Mode.Play;
        for(int i=0;i<120;i++)game.Step(1f/120,1);Assert(game.px>8,"right movement and floor");Assert(Mathf.Abs(game.py)<.01f,"ground collision");checks+=2;
        // Exercise trajectories against geometry, not just numeric generation bounds.
        var jumpField=typeof(ChibiGame).GetField("jumpBuffer",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var duckField=typeof(ChibiGame).GetField("crouch",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        game.blocks.Clear();game.blocks.Add(new ChibiGame.Block(-10,-4,40,4,0));game.blocks.Add(new ChibiGame.Block(8,0,1,.95f,1));
        game.px=4;game.py=game.vx=game.vy=0;
        for(int i=0;i<66;i++)game.Step(1f/120,1);
        jumpField.SetValue(game,.15f);
        float peak=0;for(int i=0;i<160;i++){game.Step(1f/120,1);peak=Mathf.Max(peak,game.py);}
        Assert(peak>1.5f&&game.px>10,"jump clears tallest lower block");checks++;
        game.blocks.Clear();game.blocks.Add(new ChibiGame.Block(-10,-4,40,4,0));game.blocks.Add(new ChibiGame.Block(6,1.05f,2.6f,1.2f,2));
        game.px=4;game.py=game.vx=game.vy=0;
        for(int i=0;i<180;i++)game.Step(1f/120,1);
        Assert(game.px<5.7f,"standing blocked by overhang");checks++;
        duckField.SetValue(game,true);for(int i=0;i<180;i++)game.Step(1f/120,1);
        Assert(game.px>8.6f&&Mathf.Abs(game.py)<.01f,"duck clears overhang");checks++;
        game.px=4;game.vines.Clear();game.vines.Add(new ChibiGame.Vine{r=new Rect(4.6f,.7f,1,.5f)});game.Attack();Assert(game.vines[0].cut,"bottle cuts vine");checks++;
        foreach(var asset in new[]{"fairy_idle","fairy_walk1","fairy_walk2","fairy_attack","fairy_crouch","earth","peach","mint","home","forest","flora","fairy_sleep","vine","owl"}){Assert(Resources.Load<Texture2D>("Art/"+asset)!=null,"art "+asset);checks++;}
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var tempSave=Path.Combine(Path.GetTempPath(),"Chibi-tutorial-validation.json");File.WriteAllText(tempSave,"existing progress");
        typeof(ChibiGame).GetField("savePath",flags).SetValue(game,tempSave);
        typeof(ChibiGame).GetMethod("StartTutorial",flags).Invoke(game,new object[]{false});
        game.Damage(100,true);Assert(game.hp==100&&game.lives==3,"tutorial protects lives");checks++;
        typeof(ChibiGame).GetMethod("Save",flags).Invoke(game,null);Assert(File.ReadAllText(tempSave)=="existing progress","tutorial preserves save");checks++;
        var tick=typeof(ChibiGame).GetMethod("TickTutorial",flags);
        for(int i=1;i<5;i++){tick.Invoke(game,new object[]{24f});Assert((int)typeof(ChibiGame).GetField("tutorialRoom",flags).GetValue(game)==i,"tutorial stage progression");checks++;}
        tick.Invoke(game,new object[]{23.9f});Assert(game.mode==ChibiGame.Mode.Play,"tutorial lasts 120 seconds");checks++;
        tick.Invoke(game,new object[]{.11f});Assert(game.mode==ChibiGame.Mode.Menu,"tutorial returns to menu at 120 seconds");checks++;
        Assert(File.ReadAllText(tempSave)=="existing progress","tutorial exit preserves save");checks++;
        File.Delete(tempSave);
        UnityEngine.Object.DestroyImmediate(go);Directory.CreateDirectory("QA");File.WriteAllText("QA/validation.txt","PASS: "+checks+" assertions. 100 seeds; vine counts; obstacle clearances; gap bounds; determinism; lives; movement; grounding; keyboard inversion regression; gamepad isolation; opposing keys; drift dead zone; jump trajectory; standing collision; crouch clearance; bottle attack; art.\n");Debug.Log("CHIBI_TESTS_PASS "+checks);
    }
    static void ConfigureGamepadAxes()
    {
        var inputSettings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0]);
        var axes=inputSettings.FindProperty("m_Axes");
        for(int axis=0;axis<2;axis++)
        {
            string name=axis==0?"ChibiPadHorizontal":"ChibiPadVertical";SerializedProperty entry=null;
            for(int i=0;i<axes.arraySize;i++)if(axes.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name").stringValue==name)entry=axes.GetArrayElementAtIndex(i);
            if(entry==null){axes.InsertArrayElementAtIndex(axes.arraySize);entry=axes.GetArrayElementAtIndex(axes.arraySize-1);}
            entry.FindPropertyRelative("m_Name").stringValue=name;
            foreach(string field in new[]{"descriptiveName","descriptiveNegativeName","negativeButton","positiveButton","altNegativeButton","altPositiveButton"})entry.FindPropertyRelative(field).stringValue="";
            entry.FindPropertyRelative("type").intValue=2;entry.FindPropertyRelative("axis").intValue=axis;entry.FindPropertyRelative("joyNum").intValue=0;
            entry.FindPropertyRelative("gravity").floatValue=0;entry.FindPropertyRelative("dead").floatValue=.2f;entry.FindPropertyRelative("sensitivity").floatValue=1;
            entry.FindPropertyRelative("snap").boolValue=false;entry.FindPropertyRelative("invert").boolValue=axis==1;
        }
        var physical=inputSettings.FindProperty("m_UsePhysicalKeys");if(physical!=null)physical.boolValue=true;
        inputSettings.ApplyModifiedProperties();AssetDatabase.SaveAssets();
    }
    static void Assert(bool ok,string message){if(!ok)throw new Exception("CHIBI_TEST_FAILED "+message);}
}
