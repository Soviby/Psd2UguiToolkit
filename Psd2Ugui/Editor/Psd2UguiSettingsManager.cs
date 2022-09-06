using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Baum2.Editor
{
    public static class Psd2UguiSettingsManager
    {
        private static string settingsPath = "Assets/Editor/Psd2Ugui/Psd2UguiSettings.asset";
        private static string effectSettingPath = "TMPEffectSettings";

        public static HashSet<string> needDelete = new HashSet<string>();

        static HashSet<string> m_loadedBgMd5 = new HashSet<string>();
        /// <summary>
        /// 已经在项目里的bg md5
        /// </summary>
        public static HashSet<string> loadedBgMd5
        {
            get
            {
                if (m_loadedBgMd5.Count == 0)
                {
                    var guids = AssetDatabase.FindAssets("t:texture2d", new string[] { EditorUtil.GetBaumBigSpritesPath() });
                    foreach (var guid in guids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        m_loadedBgMd5.Add(EditorUtil.ComputeMd5(path));
                    }
                }
                return m_loadedBgMd5;
            }
        }

        private static Psd2UguiSettings m_settings;
        public static Psd2UguiSettings Settings
        {
            get
            {
                if (m_settings == null)
                {
                    m_settings = AssetDatabase.LoadAssetAtPath<Psd2UguiSettings>(settingsPath);
                }
                if (m_settings == null)
                {
                    var dir = System.IO.Path.GetDirectoryName(settingsPath);
                    if (!System.IO.Directory.Exists(dir))
                    {
                        System.IO.Directory.CreateDirectory(dir);
                    }
                    if (!System.IO.File.Exists(settingsPath))
                        AssetDatabase.CreateAsset(new Psd2UguiSettings(), settingsPath);
                    m_settings = AssetDatabase.LoadAssetAtPath<Psd2UguiSettings>(settingsPath);
                }
                return m_settings;
            }
        }

        [MenuItem("Tools/Soviby/Psd2Ugui设置")]
        public static void FindPsd2UguiSetting()
        {
            Selection.activeObject = Settings;
        }


        /// <summary>
        /// 根据预制体名，得到预制体。
        /// </summary>
        /// <param name="psName"></param>
        /// <returns></returns>
        public static (GameObject go, string rootName) GetPrefab(string prefabName)
        {
            foreach (var n in Settings.prefabMap)
            {
                if (n.prefabName == prefabName)
                    return (n.prefab, n.rootName);
            }
            return (null, null);
        }

        /// <summary>
        /// 根据ps中的字体名字找到项目中相应字体，如果没有就使用default字体
        /// </summary>
        /// <param name="psName"></param>
        /// <returns></returns>
        public static (Font, bool, Vector2) GetProjectFont(string psName)
        {
            foreach (var n in Settings.fontMap)
            {
                if (n.psFontName == psName)
                    return (n.projectFont, n.isBold, n.sizeDeltaBorder);
            }
            return (Settings.defaultFont, false, Vector2.zero);
        }

        /// <summary>
        /// 将ps导出名字映射到显示名字
        /// </summary>
        /// <param name="exportName"></param>
        /// <returns></returns>
        public static string RemapFontName(string exportName)
        {
            foreach (var n in Settings.fontNameMap)
            {
                if (n.psExportName == exportName)
                    return n.psFontName;
            }
            return exportName;
        }
    }
}
