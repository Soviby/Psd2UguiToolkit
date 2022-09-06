using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEngine.UI;

namespace Baum2.Editor
{
    public sealed class PrefabCreator
    {
        private static readonly string[] Versions = { "0.6.0", "0.6.1" };
        private readonly string spriteRootPath;
        private readonly string bigSpriteRootPath;
        private readonly string fontRootPath;
        private readonly string assetPath;

        public static Action<PrefabCreator, GameObject> OnCreateFunc;

        public PrefabCreator(string spriteRootPath, string bigSpriteRootPath, string fontRootPath, string assetPath)
        {
            this.spriteRootPath = spriteRootPath;
            this.bigSpriteRootPath = bigSpriteRootPath;
            this.fontRootPath = fontRootPath;
            this.assetPath = assetPath;
        }

        public GameObject Create()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }

            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath).text;
            var json = MiniJSON.Json.Deserialize(text) as Dictionary<string, object>;
            var info = json.GetDic("info");
            Validation(info);

            var canvas = info.GetDic("canvas");
            var imageSize = canvas.GetDic("image");
            var canvasSize = canvas.GetDic("size");
            var baseSize = canvas.GetDic("base");
            var renderer = new Renderer(spriteRootPath, bigSpriteRootPath, fontRootPath, imageSize.GetVector2("w", "h"), canvasSize.GetVector2("w", "h"), baseSize.GetVector2("x", "y"));
            var rootElement = ElementFactory.Generate(json.GetDic("root"), null);
            var root = rootElement.Render(renderer);

            Postprocess(root);

            //ui窗口层级
            SetupLayer(root, "UI");

            if (OnCreateFunc != null) OnCreateFunc(this, root);

            return root;
        }

        /// <summary>
        /// 设置层级
        /// </summary>
        /// <param name="go"></param>
        private void SetupLayer(GameObject go, string layerName)
        {
            go.layer = LayerMask.NameToLayer(layerName);
            for (int i = 0; i < go.transform.childCount; i++)
            {
                SetupLayer(go.transform.GetChild(i).gameObject, layerName);
            }
        }

        private void Postprocess(GameObject go)
        {
            var methods = Assembly
                .GetExecutingAssembly()
                .GetTypes()
                .Where(x => x.IsSubclassOf(typeof(BaumPostprocessor)))
                .Select(x => x.GetMethod("OnPostprocessPrefab"));
            foreach (var method in methods)
            {
                method.Invoke(null, new object[] { go });
            }
        }

        public void Validation(Dictionary<string, object> info)
        {
            var version = info.Get("version");
            if (!Versions.Contains(version)) throw new Exception(string.Format("version {0} is not supported", version));
        }
    }

    public class Renderer
    {
        private readonly string spriteRootPath;
        private readonly string bigSpriteRootPath;
        private readonly string fontRootPath;
        private readonly Vector2 imageSize;
        public Vector2 CanvasSize { get; private set; }
        private readonly Vector2 basePosition;
        private Psd2UguiTextureImporter textureImporter;

        public Renderer(string spriteRootPath, string bigSpriteRootPath, string fontRootPath, Vector2 imageSize, Vector2 canvasSize, Vector2 basePosition)
        {
            this.spriteRootPath = spriteRootPath;
            this.bigSpriteRootPath = bigSpriteRootPath;
            this.fontRootPath = fontRootPath;
            this.imageSize = imageSize;
            CanvasSize = canvasSize;
            this.basePosition = basePosition;
            textureImporter = new Psd2UguiTextureImporter();
        }

        public UnityEngine.Object GetSprite(string spriteName, string imageName, string border = "")
        {
            var fullPath = Path.Combine(spriteRootPath, spriteName) + ".png";
            var bigFullPath = Path.Combine(bigSpriteRootPath, spriteName) + ".png";
            var sprite = textureImporter.GetSprite(fullPath, border, imageName);
            if (!sprite) sprite = textureImporter.GetSprite(bigFullPath, border, imageName);
            if (!sprite) throw new Exception($"找不到图片,spriteName:{spriteName},imageName:{imageName}");
            return sprite;
        }

        public (Font, bool, Vector2) GetFont(string fontName)
        {
            var font = Psd2UguiSettingsManager.GetProjectFont(fontName);
            Assert.IsNotNull(font.Item1, string.Format("[Baum2] font \"{0}\" is not found", fontName));
            return font;
        }

        public Vector2 CalcPosition(Vector2 position, Vector2 size)
        {
            return CalcPosition(position + size / 2.0f);
        }

        private Vector2 CalcPosition(Vector2 position)
        {
            var tmp = position - basePosition;
            tmp.y *= -1.0f;
            return tmp;
        }

        public Vector2[] GetFourCorners()
        {
            var corners = new Vector2[4];
            corners[0] = CalcPosition(Vector2.zero) + (imageSize - CanvasSize) / 2.0f;
            corners[2] = CalcPosition(imageSize) - (imageSize - CanvasSize) / 2.0f;
            return corners;
        }
    }

    public class Area
    {
        public bool Empty { get; private set; }
        public Vector2 Min { get; private set; }
        public Vector2 Max { get; private set; }
        public Vector2 Avg { get { return (Min + Max) / 2.0f; } }
        public Vector2 Center { get { return (Min + Max) / 2.0f; } }
        public float Width { get { return Mathf.Abs(Max.x - Min.x); } }
        public float Height { get { return Mathf.Abs(Max.y - Min.y); } }
        public Vector2 Size { get { return new Vector2(Width, Height); } }

        public Area()
        {
            Empty = true;
        }

        public Area(Vector2 min, Vector2 max)
        {
            Min = min;
            Max = max;
            Empty = false;
        }

        public static Area FromPositionAndSize(Vector2 position, Vector2 size)
        {
            return new Area(position, position + size);
        }

        public static Area None()
        {
            return new Area();
        }

        public void Merge(Area other)
        {
            if (other.Empty) return;
            if (Empty)
            {
                Min = other.Min;
                Max = other.Max;
                Empty = false;
                return;
            }

            if (other.Min.x < Min.x) Min = new Vector2(other.Min.x, Min.y);
            if (other.Min.y < Min.y) Min = new Vector2(Min.x, other.Min.y);
            if (other.Max.x > Max.x) Max = new Vector2(other.Max.x, Max.y);
            if (other.Max.y > Max.y) Max = new Vector2(Max.x, other.Max.y);
        }
    }

    public static class JsonExtensions
    {
        public static string Get(this Dictionary<string, object> json, string key)
        {
            object v;
            if (json.TryGetValue(key, out v))
            {
                return v as string;
            }
            return "";
        }

        public static float GetFloat(this Dictionary<string, object> json, string key)
        {
            object v;
            if (json.TryGetValue(key, out v))
            {
                return (float)v;
            }
            return 0;
        }

        public static int GetInt(this Dictionary<string, object> json, string key)
        {
            object v;
            if (json.TryGetValue(key, out v))
            {
                return (int)(float)v;
            }
            return 0;
        }

        public static T Get<T>(this Dictionary<string, object> json, string key) where T : class
        {
            if (json.ContainsKey(key))
                return json[key] as T;
            return null;
        }

        public static Dictionary<string, object> GetDic(this Dictionary<string, object> json, string key)
        {
            return json[key] as Dictionary<string, object>;
        }

        public static Vector2 GetVector2(this Dictionary<string, object> json, string keyX, string keyY)
        {
            return new Vector2((float)json[keyX], (float)json[keyY]);
        }
    }
}
