using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Baum2.Editor
{
    [CreateAssetMenu(fileName = "Psd2UguiSettings", menuName = "UI/Psd2UguiSettings")]
    public class Psd2UguiSettings : ScriptableObject
    {
        [Header("基础配置")]
        [Tooltip("导入数据存放路径")]
        public string importPath = "Assets/Editor/Psd2Ugui/Studio/Import/";
        [Tooltip("图片存放路径")]
        public string spritesPath = "Assets/Editor/Psd2Ugui/Studio/WindowAssets";
        [Tooltip("大图存放路径")]
        public string bgPath = "Assets/Editor/Psd2Ugui/Studio/Texture";
        [Tooltip("字体存放路径(已不再使用)")]
        public string fontPath = "Assets/Editor/Psd2Ugui/Studio/Fonts";
        [Tooltip("预制体生成路径")]
        public string prefabPath = "Assets/Editor/Psd2Ugui/Studio/WindowPrefabs";

        [Space]
        [Header("字体配置")]
        public Font defaultFont;
        [Tooltip("ps中字体名字和项目中字体名字的映射")]
        public List<FontMapStruct> fontMap = new List<FontMapStruct>();
        [Tooltip("ps中字体显示名字到导出名字的映射")]
        public List<FontNameMapStruct> fontNameMap = new List<FontNameMapStruct>();

        [Space]
        [Header("图片配置")]
        [Tooltip("公共图集路径")]
        public List<string> publicAtlas = new List<string>();
        [Tooltip("用于相似度比较的图片大小，越大越精确但越慢"), Min(8)]
        public int blurSize = 16;
        [Tooltip("大于这个大小就会被当成rawimage使用")]
        public Vector2 biggestSprite = new Vector2(512, 512);
        [Tooltip("是否使用图集")]
        public bool useAtlas = false;

        [Space]
        [Header("预制体配置")]
        [Tooltip("预制体KV")]
        public List<PrefabMapStruct> prefabMap = new List<PrefabMapStruct>();
    }

    [System.Serializable]
    public struct FontMapStruct
    {
        public string psFontName;
        public Font projectFont;
        public bool isBold;
        public Vector2 sizeDeltaBorder;
    }

    [System.Serializable]
    public struct FontNameMapStruct
    {
        public string psFontName;
        public string psExportName;
    }

    [System.Serializable]
    public struct PrefabMapStruct
    {
        [Tooltip("备注")]
        public string description;
        public string prefabName;
        public string rootName;
        public GameObject prefab;
    }
}
