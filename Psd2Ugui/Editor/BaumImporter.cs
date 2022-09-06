using System;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using OnionRing;
using Object = UnityEngine.Object;
using System.Threading.Tasks;
using UnityEditor.U2D;

namespace Baum2.Editor
{
    public sealed class Importer : AssetPostprocessor
    {
        private static bool doCreate = true;

        public override int GetPostprocessOrder() { return 1000; }

        public static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (string.IsNullOrEmpty(EditorUtil.ImportDirectoryPath)) return;
            var changed = false;
            // Create Directory
            foreach (var asset in importedAssets)
            {
                if (!asset.Contains(EditorUtil.ImportDirectoryPath)) continue;
                if (!string.IsNullOrEmpty(Path.GetExtension(asset))) continue;

                //重复导入，需要用户确认
                string finalPath = asset.Replace(EditorUtil.ImportDirectoryPath, EditorUtil.GetBaumSpritesPath());
                if (AssetDatabase.LoadAssetAtPath<Object>(finalPath) != null)
                {
                    if (!EditorUtility.DisplayDialog("warning", string.Format("{0}已存在，是否覆盖", finalPath), "是", "否"))
                    {
                        doCreate = false;
                        return;
                    }
                    else
                    {
                        doCreate = true;
                    }
                }
                else
                {
                    doCreate = true;
                }

                CreateSpritesDirectory(asset);
                changed = true;
            }

            // 这是之前读取像素颜色计算九宫格划分的代码，不太直观而且正确性不太好
            // 以后直接从ps导出切好九宫格的小图
            // Slice Sprite
            foreach (var asset in importedAssets)
            {
                if (!doCreate)
                    return;
                if (!asset.Contains(EditorUtil.ImportDirectoryPath)) continue;
                if (!asset.EndsWith(".png", System.StringComparison.Ordinal)) continue;
                var assetPath = SliceSprite(asset);
                AssetDatabase.ImportAsset(assetPath);
                changed = true;
            }

            if (changed)
            {
                Debug.Log("AssetDatabase.Refresh();");
                AssetDatabase.Refresh();
            }

            EditorApplication.delayCall += () =>
            {
                if (!doCreate)
                    return;
                // Delete Directory
                int curProgress = 0;
                foreach (var asset in importedAssets)
                {
                    curProgress++;
                    EditorUtility.DisplayProgressBar("提示", "正在删除旧资源", ((float)curProgress) / ((float)importedAssets.Length));
                    if (!asset.Contains(EditorUtil.ImportDirectoryPath)) continue;
                    if (!string.IsNullOrEmpty(Path.GetExtension(asset))) continue;
                    Debug.LogFormat("[Baum2] Delete Directory: {0}", EditorUtil.ToUnityPath(asset));
                    AssetDatabase.DeleteAsset(EditorUtil.ToUnityPath(asset));
                    changed = true;
                }

                // Create Prefab
                curProgress = 0;
                foreach (var asset in importedAssets)
                {
                    curProgress++;
                    EditorUtility.DisplayProgressBar("提示", "正在导入新资源", ((float)curProgress) / ((float)importedAssets.Length));
                    if (!asset.Contains(EditorUtil.ImportDirectoryPath)) continue;
                    if (!asset.EndsWith(".layout.txt", System.StringComparison.Ordinal)) continue;

                    var name = Path.GetFileName(asset).Replace(".layout.txt", "");
                    var spriteRootPath = EditorUtil.ToUnityPath(Path.Combine(EditorUtil.GetBaumSpritesPath(), name));
                    var bigSpriteRootPath = EditorUtil.ToUnityPath(Path.Combine(EditorUtil.GetBaumBigSpritesPath(), name));
                    var fontRootPath = EditorUtil.ToUnityPath(EditorUtil.GetBaumFontsPath());
                    var creator = new PrefabCreator(spriteRootPath, bigSpriteRootPath, fontRootPath, asset);
                    var go = creator.Create();
                    var savePath = EditorUtil.ToUnityPath(Path.Combine(EditorUtil.GetBaumPrefabsPath(), name + ".prefab"));
#if UNITY_2018_3_OR_NEWER
                    PrefabUtility.SaveAsPrefabAsset(go, savePath);
#else
                    Object originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(savePath);
                    if (originalPrefab == null) originalPrefab = PrefabUtility.CreateEmptyPrefab(savePath);
                    PrefabUtility.ReplacePrefab(go, originalPrefab, ReplacePrefabOptions.ReplaceNameBased);
#endif
                    GameObject.DestroyImmediate(go);
                    Debug.LogFormat("[Baum2] Create Prefab: {0}", savePath);

                    AssetDatabase.DeleteAsset(EditorUtil.ToUnityPath(asset));

                    //删除多余的图片
                    foreach (var p in Psd2UguiSettingsManager.needDelete)
                    {
                        AssetDatabase.DeleteAsset(p);
                    }
                    Psd2UguiSettingsManager.needDelete.Clear();
                    Psd2UguiSettingsManager.loadedBgMd5.Clear();
                }
                EditorUtility.ClearProgressBar();
            };
        }

        private static void CreateSpritesDirectory(string asset)
        {
            var directoryName = Path.GetFileName(Path.GetFileName(asset));
            var directoryPath = EditorUtil.GetBaumSpritesPath();
            var directoryFullPath = Path.Combine(directoryPath, directoryName);
            if (Directory.Exists(directoryFullPath))
            {
                // Debug.LogFormat("[Baum2] Delete Exist Sprites: {0}", EditorUtil.ToUnityPath(directoryFullPath));
                foreach (var filePath in Directory.GetFiles(directoryFullPath, "*.png", SearchOption.TopDirectoryOnly)) File.Delete(filePath);
            }
            else
            {
                // Debug.LogFormat("[Baum2] Create Directory: {0}", EditorUtil.ToUnityPath(directoryPath) + "/" + directoryName);
                string path = EditorUtil.ToUnityPath(directoryPath);
                string folderName = Path.GetFileName(directoryFullPath);
                string fullUnityPath = string.Format("{0}/{1}", path, folderName);
                AssetDatabase.CreateFolder(path, folderName);

                // 创建图集
                if (Psd2UguiSettingsManager.Settings.useAtlas)
                {
                    System.Func<Task> delay = async () =>
                    {
                        await Task.Delay(System.TimeSpan.FromSeconds(0.1f));
                        DefaultAsset folder = (DefaultAsset)AssetDatabase.LoadAssetAtPath(fullUnityPath, typeof(DefaultAsset));
                        if (folder == null)
                        {
                            Debug.LogErrorFormat("can't find {0}", fullUnityPath);
                        }
                        else
                        {
                            CreateAtlas(fullUnityPath, folder);
                        }
                    };
                    delay();
                }
            }
        }

        private static void CreateAtlas(string path, UnityEngine.Object obj)
        {
            string fPath = GetFolderPath(path);
            string atlasPath = fPath + "0_atlas_" + obj.name + ".spriteatlas";
            UnityEngine.U2D.SpriteAtlas atlas = new UnityEngine.U2D.SpriteAtlas();
            var setting = atlas.GetPackingSettings();
            setting.enableTightPacking = false;
            setting.enableRotation = false;
            atlas.SetPackingSettings(setting);
            atlas.Add(new UnityEngine.Object[] { obj });
            //之前有的会自动覆盖
            AssetDatabase.CreateAsset(atlas, atlasPath);
        }

        private static string GetFolderPath(string path)
        {
            string sPath = path.Replace("\\", "/");
            if (!sPath.Contains("."))
            {
                //是个文件夹
                return sPath + "/";
            }
            string[] ss = sPath.Split('/');
            string folderPath = "";
            if (ss != null && ss.Length > 0)
            {
                folderPath = sPath.Substring(0, sPath.Length - ss[ss.Length - 1].Length);
            }
            return folderPath;
        }

        private static string SliceSprite(string asset)
        {
            var directoryName = Path.GetFileName(Path.GetDirectoryName(asset));
            var directoryPath = Path.Combine(EditorUtil.GetBaumSpritesPath(), directoryName);
            var bigSpriteDirectoryPath = Path.Combine(EditorUtil.GetBaumBigSpritesPath(), directoryName);
            var fileName = Path.GetFileName(asset);
            var noSlice = !fileName.EndsWith("-slice.png", StringComparison.Ordinal);
            //兼容旧版的noslice标记
            fileName = fileName.Replace("-noslice.png", ".png");
            fileName = fileName.Replace("-slice.png", ".png");

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(asset);
            var slicedTexture = new SlicedTexture(texture, new Boarder(0, 0, 0, 0));
            var isBigImage = BaumElementHelper.Image.IsBigTexture(slicedTexture.Texture);
            var newPath = isBigImage ? Path.Combine(bigSpriteDirectoryPath, fileName) : Path.Combine(directoryPath, fileName);

            if (!noSlice)
            {
                slicedTexture = TextureSlicer.Slice(texture);
            }

            //创建存放大图的文件夹
            if (!Directory.Exists(bigSpriteDirectoryPath))
            {
                AssetDatabase.CreateFolder(EditorUtil.GetBaumBigSpritesPath(), directoryName);
            }

            if (PreprocessTexture.SlicedTextures == null) PreprocessTexture.SlicedTextures = new Dictionary<string, SlicedTexture>();
            PreprocessTexture.SlicedTextures[fileName] = slicedTexture;

            //原图就用拷贝的方式，因为编码会改变像素的md5
            if (noSlice)
            {
                File.Copy(asset, newPath, true);
            }
            else
            {
                byte[] pngData = slicedTexture.Texture.EncodeToPNG();
                File.WriteAllBytes(newPath, pngData);
            }

            if (!noSlice)
            {
                Object.DestroyImmediate(slicedTexture.Texture);
            }

            // Debug.LogFormat("[Baum2] Slice: {0} -> {1}", EditorUtil.ToUnityPath(asset), EditorUtil.ToUnityPath(newPath));
            return EditorUtil.ToUnityPath(newPath);
        }
    }
}
