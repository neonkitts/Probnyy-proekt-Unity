using System.IO;
using UnityEngine;
using UnityEditor;

// Native asset import: slices generated atlases into aligned, point-filtered game sprites.
public static class ChibiArtImport
{
    [MenuItem("Chibi/Import pixel artwork")]
    public static void Import()
    {
        Texture2D fairy=Read("fairy-sheet-original.png");
        string[] names={"idle","walk1","walk2","jump","crouch","attack","hurt","sleep"};
        for(int i=0;i<8;i++)
        {
            RectInt cell=new RectInt((i%4)*fairy.width/4,(1-i/4)*fairy.height/2,fairy.width/4,fairy.height/2);
            RectInt bounds=Bounds(fairy,cell);
            int desired=i==4?36:i==7?31:64;
            float scale=Mathf.Min(60f/bounds.width,desired/(float)bounds.height);
            int w=Mathf.RoundToInt(bounds.width*scale),h=Mathf.RoundToInt(bounds.height*scale);
            var frame=new Texture2D(80,80,TextureFormat.RGBA32,false);frame.SetPixels32(new Color32[6400]);
            CopyScaled(fairy,bounds,frame,(80-w)/2,3,w,h);
            Write(frame,"fairy_"+names[i]);Object.DestroyImmediate(frame);
        }
        Texture2D tiles=Read("tiles-original.png");string[] props={"earth","peach","mint","vine","owl","flora"};
        for(int i=0;i<6;i++)
        {
            var cell=new RectInt((i%3)*tiles.width/3,(1-i/3)*tiles.height/2,tiles.width/3,tiles.height/2);
            RectInt bounds=Bounds(tiles,cell);
            if(i<3)
            {
                // Square solid portion starts below decorative flowers; top aligns to physics surface.
                bounds=new RectInt(bounds.x,cell.y+2,bounds.width,Mathf.Min(bounds.height,tiles.height/2-77));
                var tile=new Texture2D(64,64,TextureFormat.RGBA32,false);CopyScaled(tiles,bounds,tile,0,0,64,64);Write(tile,props[i]);Object.DestroyImmediate(tile);
            }
            else
            {
                int w=i==4?112:96,h=Mathf.Max(1,Mathf.RoundToInt(w*bounds.height/(float)bounds.width));
                var sprite=new Texture2D(w,h,TextureFormat.RGBA32,false);CopyScaled(tiles,bounds,sprite,0,0,w,h);Write(sprite,props[i]);Object.DestroyImmediate(sprite);
            }
        }
        Texture2D home=Read("home-original.png");var hb=Bounds(home,new RectInt(0,0,home.width,home.height));
        var ht=new Texture2D(384,384,TextureFormat.RGBA32,false);CopyScaled(home,hb,ht,0,0,384,384);Write(ht,"home");
        Texture2D forest=Read("forest-original.png");var ft=new Texture2D(768,512,TextureFormat.RGBA32,false);CopyScaled(forest,new RectInt(0,0,forest.width,forest.height),ft,0,0,768,512);Write(ft,"forest");
        Object.DestroyImmediate(fairy);Object.DestroyImmediate(tiles);Object.DestroyImmediate(home);Object.DestroyImmediate(ht);Object.DestroyImmediate(forest);Object.DestroyImmediate(ft);
        AssetDatabase.Refresh();
        foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/Art"}))
        {var imp=AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;if(imp==null)continue;imp.npotScale=TextureImporterNPOTScale.None;imp.filterMode=FilterMode.Point;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.mipmapEnabled=false;imp.alphaIsTransparency=true;imp.SaveAndReimport();}
        Debug.Log("CHIBI_PIXEL_IMPORT_PASS");
    }
    static Texture2D Read(string name){var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes("Assets/ArtSource/"+name));return t;}
    static RectInt Bounds(Texture2D tex,RectInt cell)
    {
        int left=cell.xMax,right=cell.xMin,bottom=cell.yMax,top=cell.yMin;
        for(int y=cell.yMin;y<cell.yMax;y++)for(int x=cell.xMin;x<cell.xMax;x++)if(tex.GetPixel(x,y).a>.45f){left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}
        return new RectInt(left,bottom,right-left+1,top-bottom+1);
    }
    static void CopyScaled(Texture2D src,RectInt rect,Texture2D dst,int dx,int dy,int w,int h)
    {for(int y=0;y<h;y++)for(int x=0;x<w;x++){Color c=src.GetPixel(rect.x+Mathf.Min(rect.width-1,(int)((x+.5f)*rect.width/w)),rect.y+Mathf.Min(rect.height-1,(int)((y+.5f)*rect.height/h)));c.a=c.a>.4f?1:0;dst.SetPixel(dx+x,dy+y,c);}dst.Apply();}
    static void Write(Texture2D tex,string name){File.WriteAllBytes("Assets/Resources/Art/"+name+".png",tex.EncodeToPNG());}
}
