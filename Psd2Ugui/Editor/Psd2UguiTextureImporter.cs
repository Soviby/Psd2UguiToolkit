using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace Baum2.Editor
{
    public class Psd2UguiTextureImporter
    {
        /// <summary>
        /// 图片导入数据
        /// </summary>
        public class TextureInfo
        {
            /// <summary>
            /// 图片名字，也是判断相同图片的依据
            /// </summary>
            public string name;
            /// <summary>
            /// 原始图片
            /// </summary>
            public Texture2D originTex;
            /// <summary>
            /// 路径
            /// </summary>
            public string path;
            /// <summary>
            /// 是否是公共图集中的
            /// </summary>
            public bool isPublicTex;
            /// <summary>
            /// 根据像素生成的md5，判断相同图片的第二依据
            /// </summary>
            public string md5;
        }

        private List<string> publicAtlasPath;
        /// <summary>
        /// 加载过的图片，名字和md5双索引
        /// </summary>
        private Dictionary<string, List<TextureInfo>> loadedTexs;

        /// <summary>
        /// 注册已有图片的接口
        /// </summary>
        /// <param name="name"></param>
        /// <param name="textureInfo"></param>
        private void AddTextureInfo(string name, TextureInfo textureInfo)
        {
            if (loadedTexs.ContainsKey(name))
            {
                loadedTexs[name].Add(textureInfo);
            }
            else
            {
                loadedTexs.Add(name, new List<TextureInfo>() { textureInfo });
            }
        }

        public Psd2UguiTextureImporter()
        {
            loadedTexs = new Dictionary<string, List<TextureInfo>>();
            
            //公共图集自动计入
            List<string> publicAtlasPath = Psd2UguiSettingsManager.Settings.publicAtlas;
            if (publicAtlasPath.Count > 0)
            {
                string[] folders = new string[publicAtlasPath.Count];
                for (int i = 0; i < folders.Length; i++)
                {
                    folders[i] = publicAtlasPath[i];
                }
                var guids = AssetDatabase.FindAssets("t:texture2d", folders);
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var textureInfo = LoadTexture(path, true);
                    AddTextureInfo(textureInfo.name, textureInfo);
                    AddTextureInfo(textureInfo.md5, textureInfo);
                }
            }
        }

        /// <summary>
        /// 加载图片信息
        /// </summary>
        /// <param name="path"></param>
        /// <param name="isPublic"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private TextureInfo LoadTexture(string path, bool isPublic, string name = "")
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) return null;
            if (isPublic && Psd2UguiSettingsManager.Settings.useAtlas)
            {
                //如果是公共图集，就需要获取图片文件名作为图片名字
                name = System.IO.Path.GetFileName(path).Split('.')[0];
            }
            TextureInfo textureInfo = new TextureInfo
            {
                originTex = tex,
                path = path,
                isPublicTex = isPublic,
                name = name,
                md5 = EditorUtil.ComputeMd5(path)
            };
            return textureInfo;
        }

        public Object GetSprite(string fullPath, string border, string imageName)
        {
            //避免重复纹理，如有重复，就复用之前的
            fullPath = fullPath.Replace("\\", "/");
            TextureInfo loadedInfo = null;

            var curInfo = LoadTexture(fullPath, false, imageName);
            if (curInfo == null) return null;

            //先根据md5进行判断，再根据名字
            if (loadedInfo == null && loadedTexs.ContainsKey(curInfo.md5))
            {
                foreach (var tmpInfo in loadedTexs[curInfo.md5])
                {
                    //有重复的，就复用之前的路径
                    if (tmpInfo.path != curInfo.path && DoReplace(tmpInfo, curInfo))
                    {
                        loadedInfo = tmpInfo;
                        break;
                    }
                }
            }

            if (loadedInfo == null && loadedTexs.ContainsKey(imageName))
            {
                foreach (var tmpInfo in loadedTexs[imageName])
                {
                    //有重复的，就复用之前的路径
                    if (tmpInfo.path != curInfo.path && DoReplace(tmpInfo, curInfo))
                    {
                        loadedInfo = tmpInfo;
                        break;
                    }
                }
            }

            //如果loadedInfo还是null，说明不是重复纹理，加入加载纹理记录，否则给用户判断是否需要删除
            if (loadedInfo == null)
            {
                AddTextureInfo(imageName, curInfo);
                AddTextureInfo(curInfo.md5, curInfo);
            }
            else
            {
                fullPath = loadedInfo.path;
                Psd2UguiSettingsManager.needDelete.Add(curInfo.path);
                //AssetDatabase.DeleteAsset(curInfo.path);
            }

            if (!string.IsNullOrEmpty(border) && border != "true")
            {
                string[] ns = border.Split('|');
                Vector4 bd;
                //如果不是数字的话，ps导出的时候后报错了，这边不做容错
                if (ns.Length == 1)
                {
                    bd = new Vector4(float.Parse(ns[0]), float.Parse(ns[0]), float.Parse(ns[0]), float.Parse(ns[0]));
                }
                else if (ns.Length == 4)
                {
                    bd = new Vector4(float.Parse(ns[0]), float.Parse(ns[1]), float.Parse(ns[2]), float.Parse(ns[3]));
                }
                else
                {
                    Debug.LogErrorFormat("slice data error : {0}", border);
                    return null;
                }
                TextureImporter ti = AssetImporter.GetAtPath(fullPath) as TextureImporter;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteBorder = bd;
                AssetDatabase.WriteImportSettingsIfDirty(fullPath);
                AssetDatabase.ImportAsset(fullPath);
            }

            //兼容sprite和texture
            Object texObj;
            texObj = AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
            if (texObj == null)
                texObj = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
            return texObj;
        }

        /// <summary>
        /// 检查是否有相似的大小，相差大小小于两个连续2次幂的差属于相似大小
        /// </summary>
        /// <param name="tex1"></param>
        /// <param name="tex2"></param>
        /// <returns></returns>
        public bool IsSimilarSize(Texture2D tex1, Texture2D tex2)
        {
            int tex1Size = Mathf.Max(tex1.width, tex1.height);
            int tex2Size = Mathf.Max(tex2.width, tex2.height);
            var texSizes = ImageProcessingUtility.texSizes;
            for (int i = 1; i < texSizes.Length; i++)
            {
                if (texSizes[i - 1] <= tex1Size && tex1Size <= texSizes[i] && texSizes[i - 1] <= tex2Size && tex2Size <= texSizes[i])
                    return true;
            }
            return false;
        }

        private string warningTxt = "是否删除右边导入图并复用左边项目已有图";
        private string warningTxt2 = "公共图集有相同图片，请检查是否相同";
        private string warningTxt3 = "同一个界面有相同图片，请检查是否相同";
        private string deleteTxt = "相同，可复用左边图片";
        private string remainTxt = "不同，继续使用右边图片";
        private bool DoReplace(TextureInfo loadedTex, TextureInfo curTex)
        {
            //现在图片窗口显示不了，先都直接复用
            return true;

            var importerGUI = new Psd2UguiImporterGUI();
            importerGUI.SetTex(loadedTex, curTex);
            importerGUI.Show();
            string warning;
            if (!loadedTex.isPublicTex)
            {
                warning = warningTxt3;
            }
            else
            {
                warning = warningTxt2;
            }
            if (EditorUtility.DisplayDialog(warningTxt, warning, deleteTxt, remainTxt))
            {
                importerGUI.Close();
                return true;
            }
            else
            {
                importerGUI.Close();
                return false;
            }
        }
    }
}
