using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Editor
{
    public static class SpriteAtlasCreator 
    {
        [MenuItem("Tools/Editors/Migrate Sprite Packer to Sprite Atlas")]
        public static void InspectAllAtlasesUnthreaded()
        {
            string[] pathsToAssets = AssetDatabase.FindAssets("t:Texture", new[] {"Assets"});

            if (pathsToAssets.Length == 0)
            {
                Debug.LogError("No texture found in the project.");
            }
            else
            {
                var tagDict = new Dictionary<string,AtlasData>();
                
                foreach (string path in pathsToAssets)
                {
                    var stringPath = AssetDatabase.GUIDToAssetPath(path);

                    var ti = AssetImporter.GetAtPath(stringPath) as TextureImporter;

                    if (ti != null && !string.IsNullOrEmpty(ti.spritePackingTag))
                    {
                        if (tagDict.ContainsKey(ti.spritePackingTag))
                        {
                            var atlasData = tagDict[ti.spritePackingTag];
                            
                            atlasData.AddTexture(stringPath, ti);
                        }
                        else
                        {
                            AddNewAtlas(stringPath, ti, tagDict);
                        }
                    }
                }

                foreach (var entry in tagDict)
                {
                    CreateAtlas(entry);
                }
            }
        }
        
        private static void AddNewAtlas(string stringPath, TextureImporter ti, Dictionary<string, AtlasData> tagDict)
        {
            var atlasData = new AtlasData();
            atlasData.AddTexture(stringPath,ti);
            tagDict[ti.spritePackingTag] = atlasData;
        }

        public static void CreateAtlas(KeyValuePair<string,AtlasData> atlasData)
        {
            int atlasNum = 0;
            string name;
            
            if (!AssetDatabase.IsValidFolder("Assets/SpriteAtlas"))
            {
                AssetDatabase.CreateFolder("Assets", "SpriteAtlas");
            }

            foreach (var group in atlasData.Value.groups)
            {
                SpriteAtlas spriteAtlas = new SpriteAtlas();
                
                if (group.android.overridden)
                {
                    spriteAtlas.SetPlatformSettings(group.android);
                }
                
                if (group.iphone.overridden)
                {
                    spriteAtlas.SetPlatformSettings(group.iphone);
                }
                
                // if the group have non-alpha textures, I set RGB24 as a default.
                if ( !group.hasAlpha )
                {
                    group.normal.format = TextureImporterFormat.RGB24;
                }

                spriteAtlas.SetPlatformSettings(group.normal);

                if (atlasNum > 0)
                {
                    name = atlasData.Key + " (Group " + atlasNum.ToString() + ")";
                }
                else
                {
                    name = atlasData.Key;
                }

                AssetDatabase.CreateAsset(spriteAtlas, "Assets/SpriteAtlas/" + name + ".spriteatlas");
            
                foreach (var texturePath in group.textures)
                {
                    Object s = AssetDatabase.LoadAssetAtPath<Object>(texturePath);
                    if (s != null)
                    {
                        SpriteAtlasExtensions.Add(spriteAtlas,new []{s});
                    }
                }

                atlasNum++;
            }
            
            AssetDatabase.SaveAssets();
        }

    }

    // Atlas contains the Settings of the atlas referred to each platform settings and the list of textures.
    public class Atlas
    {
        public List<string> textures;
        public TextureImporterPlatformSettings iphone;
        public TextureImporterPlatformSettings android;
        public TextureImporterPlatformSettings normal;
        public bool hasAlpha;

        public Atlas(bool alpha)
        {
            textures = new List<string>();
            hasAlpha = alpha;
        }
    }
    
    // AtlasData has the list of groups in an atlas as used for the sprite packer.
    public class AtlasData
    {
        public List<Atlas> groups;

        public AtlasData()
        {
            groups = new List<Atlas>();
        }

        public void AddTexture(string stringPath, TextureImporter ti)
        {
            
            var tiAnd = ti.GetPlatformTextureSettings("Android");
            var tiIos = ti.GetPlatformTextureSettings("iPhone");

            foreach (var group in groups)
            {
                if (group.android.overridden == tiAnd.overridden && group.iphone.overridden == tiIos.overridden) 
                {
                    if (tiAnd.overridden && tiIos.overridden)
                    {
                        if (@group.android.textureCompression != tiAnd.textureCompression ||
                            @group.android.format != tiAnd.format ||
                            @group.iphone.textureCompression != tiIos.textureCompression ||
                            @group.iphone.format != tiIos.format) continue;
                    }
                    else if (tiAnd.overridden)
                    {
                        if (@group.android.textureCompression != tiAnd.textureCompression ||
                            @group.android.format != tiAnd.format) continue;
                    }
                    else if (tiIos.overridden)
                    {
                        if (@group.iphone.textureCompression != tiIos.textureCompression ||
                            @group.iphone.format != tiIos.format) continue;
                    }
                    else if (@group.normal.textureCompression != ti.textureCompression || 
                             group.hasAlpha != ti.DoesSourceTextureHaveAlpha()) continue;
                    
                    group.textures.Add(stringPath);
                    return;
                }
            }

            var newAtlas =  new Atlas(ti.DoesSourceTextureHaveAlpha());
            newAtlas.textures.Add(stringPath);
            newAtlas.iphone = tiIos;
            newAtlas.android = tiAnd;
            newAtlas.normal = ti.GetDefaultPlatformTextureSettings();
            groups.Add(newAtlas);
        }
    }

}