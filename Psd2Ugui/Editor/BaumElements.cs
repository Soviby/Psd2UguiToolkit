using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace Baum2.Editor
{
    public static class ElementFactory
    {
        public static readonly Dictionary<string, Func<Dictionary<string, object>, Element, Element>> Generator = new Dictionary<string, Func<Dictionary<string, object>, Element, Element>>()
        {
            { "Root", (d, p) => new RootElement(d, p) },
            { "Image", (d, p) => new ImageElement(d, p) },
            { "Group", (d, p) => new GroupElement(d, p) },
            { "Text", (d, p) => new TextElement(d, p) },
            { "Button", (d, p) => new ButtonElement(d, p) },
            { "Mask", (d,p) => new MaskElement(d, p) },
            { "List", (d, p) => new ListElement(d, p) },
            { "Slider", (d, p) => new SliderElement(d, p) },
            { "Scrollbar", (d, p) => new ScrollbarElement(d, p) },
            { "Toggle", (d, p) => new ToggleElement(d, p) },
            { "Prefab", (d, p) => new PrefabElement(d, p) },
        };

        public static void AddElement(string elementType, Func<Dictionary<string, object>, Element, Element> createFunc)
        {
            if (Generator.ContainsKey(elementType)) Generator[elementType] = createFunc;
            else Generator.Add(elementType, createFunc);
        }

        public static Element Generate(Dictionary<string, object> json, Element parent)
        {
            var type = json.Get("type");
            Assert.IsTrue(Generator.ContainsKey(type), "[Baum2] Unknown type: " + type);
            return Generator[type](json, parent);
        }
    }


    public abstract class Element
    {
        public string name;
        protected string pivot;
        protected bool stretchX;
        protected bool stretchY;
        protected Vector2 canvasPosition;
        protected Vector2 sizeDelta;
        protected Element parent;

        //针对图片，是否需要设置为native
        private bool isNative;
        //是否需要九宫
        private bool isSlice;

        public abstract GameObject Render(Renderer renderer);
        public abstract Area CalcArea();


        public Element(Dictionary<string, object> json, Element parent)
        {
            this.parent = parent;
            name = json.Get("name");
            if (json.ContainsKey("pivot")) pivot = json.Get("pivot");
            if (json.ContainsKey("stretchxy") || json.ContainsKey("stretchx")) stretchX = true;
            if (json.ContainsKey("stretchxy") || json.ContainsKey("stretchy")) stretchY = true;
            isNative = json.ContainsKey("native");
            isSlice = json.ContainsKey("slice");

            if (json.ContainsKey("x") && json.ContainsKey("y")) canvasPosition = json.GetVector2("x", "y");
            if (json.ContainsKey("w") && json.ContainsKey("h")) sizeDelta = json.GetVector2("w", "h");
        }

        protected GameObject CreateUIGameObject(Renderer renderer)
        {
            var go = new GameObject(name);
            go.AddComponentByElement<RectTransform>();
            return go;
        }

        protected void SetPivot(GameObject root, Renderer renderer)
        {
            if (string.IsNullOrEmpty(pivot)) pivot = "none";

            var parentSize = parent != null ? parent.CalcArea().Size : renderer.CanvasSize;
            //如果父节点是根节点，需要自己算偏移
            bool parentIsRoot = false;
            if (parent != null)
            {
                parentIsRoot = parent.GetType() == typeof(RootElement);
            }

            var rect = root.GetComponentByElement<RectTransform>();
            var pivotMin = rect.anchorMin;
            var pivotMax = rect.anchorMax;
            var sizeDelta = rect.sizeDelta;
            var anchoredPos = rect.anchoredPosition;

            if (pivot.Contains("bottom"))
            {
                pivotMin.y = 0.0f;
                pivotMax.y = 0.0f;
                sizeDelta.y = CalcArea().Height;
                if (parentIsRoot)
                    anchoredPos.y += parentSize.y / 2;
            }
            else if (pivot.Contains("top"))
            {
                pivotMin.y = 1.0f;
                pivotMax.y = 1.0f;
                sizeDelta.y = CalcArea().Height;
                if (parentIsRoot)
                    anchoredPos.y -= parentSize.y / 2;
            }
            if (pivot.Contains("left"))
            {
                pivotMin.x = 0.0f;
                pivotMax.x = 0.0f;
                sizeDelta.x = CalcArea().Width;
                if (parentIsRoot)
                    anchoredPos.x += parentSize.x / 2;
            }
            else if (pivot.Contains("right"))
            {
                pivotMin.x = 1.0f;
                pivotMax.x = 1.0f;
                sizeDelta.x = CalcArea().Width;
                if (parentIsRoot)
                    anchoredPos.x -= parentSize.x / 2;
            }

            rect.anchorMin = pivotMin;
            rect.anchorMax = pivotMax;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPos;

            SetImageSize(root);
        }

        protected void SetStretch(GameObject root, Renderer renderer)
        {
            if (!stretchX && !stretchY) return;

            var parentSize = parent != null ? parent.CalcArea().Size : renderer.CanvasSize;
            var rect = root.GetComponentByElement<RectTransform>();
            var pivotPosMin = new Vector2(0.5f, 0.5f);
            var pivotPosMax = new Vector2(0.5f, 0.5f);
            var sizeDelta = rect.sizeDelta;

            if (stretchX)
            {
                pivotPosMin.x = 0.0f;
                pivotPosMax.x = 1.0f;
                sizeDelta.x = CalcArea().Width - parentSize.x;
            }

            if (stretchY)
            {
                pivotPosMin.y = 0.0f;
                pivotPosMax.y = 1.0f;
                sizeDelta.y = CalcArea().Height - parentSize.y;
            }

            rect.anchorMin = pivotPosMin;
            rect.anchorMax = pivotPosMax;
            rect.sizeDelta = sizeDelta;
        }

        /// <summary>
        /// 不是九宫和rawimage可以设置native
        /// </summary>
        /// <param name="go"></param>
        void SetImageSize(GameObject go)
        {
            if (!isNative)
                return;
            if (!isSlice)
            {
                var image = go.GetComponentByElement<Image>();
                if (image != null)
                {
                    image.SetNativeSize();
                }
            }
            var rawImage = go.GetComponentByElement<RawImage>();
            if (rawImage != null)
                rawImage.SetNativeSize();
        }
    }


    public sealed class PrefabElement : GroupElement
    {
        private string prefabName;
        private string rootName;
        public PrefabElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
            prefabName = json.Get("prefabName");
        }

        public override GameObject Render(Renderer renderer)
        {
            var prefabInfo = Psd2UguiSettingsManager.GetPrefab(prefabName);
            var prefab = prefabInfo.go;
            rootName = prefabInfo.rootName;
            GameObject go;
            if (prefab)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            else
            {
                go = new GameObject();
                go.AddComponentByElement<RectTransform>();
                Debug.LogError($"无法查找到相应预制体({prefabName}),正在创建的层级({name})");
            }
            go.name = name;
            SetXYWH(renderer, go.GetComponentByElement<RectTransform>(), false);
            GameObject root = string.IsNullOrWhiteSpace(rootName) ? null : go.FindInChild(rootName);
            if (root)
            {
                RenderChildren(renderer, root);
            }

            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }

        public override Area CalcArea()
        {
            return new Area(canvasPosition, canvasPosition + sizeDelta);
        }
    }

    public class GroupElement : Element
    {
        protected List<Element> elements;
        private Area areaCache;

        public GroupElement(Dictionary<string, object> json, Element parent, bool resetStretch = false) : base(json, parent)
        {
            elements = new List<Element>();
            var jsonElements = json.Get<List<object>>("elements");
            if (jsonElements != null)
                foreach (var jsonElement in jsonElements)
                {
                    var x = stretchX;
                    var y = stretchY;
                    if (resetStretch)
                    {
                        stretchX = false;
                        stretchY = false;
                    }
                    elements.Add(ElementFactory.Generate(jsonElement as Dictionary<string, object>, this));
                    stretchX = x;
                    stretchY = y;
                }
            elements.Reverse();
            areaCache = CalcAreaInternal();
        }

        public override GameObject Render(Renderer renderer)
        {
            var go = CreateSelf(renderer);

            RenderChildren(renderer, go);

            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }

        protected virtual GameObject CreateSelf(Renderer renderer, bool useChildArea = true)
        {
            var go = CreateUIGameObject(renderer);
            var rect = go.GetComponentByElement<RectTransform>();
            SetXYWH(renderer, rect, useChildArea);
            return go;
        }

        protected void SetXYWH(Renderer renderer, RectTransform tf, bool useChildArea = true)
        {
            Vector2 size;
            Vector2 anchoredPosition;
            if (useChildArea)
            {
                var area = CalcArea();
                size = area.Size;
                anchoredPosition = renderer.CalcPosition(area.Min, area.Size);
            }
            else
            {
                size = sizeDelta;
                anchoredPosition = renderer.CalcPosition(canvasPosition, sizeDelta);
            }

            tf.sizeDelta = size;
            tf.anchoredPosition = anchoredPosition;
        }

        protected void RenderChildren(Renderer renderer, GameObject root, Action<GameObject, Element> callback = null)
        {
            foreach (var element in elements)
            {
                var go = element.Render(renderer);
                var rectTransform = go.GetComponentByElement<RectTransform>();
                var sizeDelta = rectTransform.sizeDelta;
                go.transform.SetParent(root.transform, true);
                rectTransform.sizeDelta = sizeDelta;
                rectTransform.localScale = Vector3.one;
                if (callback != null) callback(go, element);
            }
        }

        private Area CalcAreaInternal()
        {
            var area = Area.None();
            foreach (var element in elements) area.Merge(element.CalcArea());
            return area;
        }

        public override Area CalcArea()
        {
            return areaCache;
        }
    }

    public class RootElement : GroupElement
    {
        private Vector2 sizeDelta;

        public RootElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
            stretchX = true;
            stretchY = true;
        }

        protected override GameObject CreateSelf(Renderer renderer, bool useChildArea = true)
        {
            var go = CreateUIGameObject(renderer);

            var rect = go.GetComponentByElement<RectTransform>();
            sizeDelta = renderer.CanvasSize;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = Vector2.zero;

            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }

        public override Area CalcArea()
        {
            return new Area(-sizeDelta / 2.0f, sizeDelta / 2.0f);
        }

    }

    public class ImageElement : Element
    {
        private string spriteName;
        private float opacity;
        private string slice;
        private string imageName;

        public ImageElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
            spriteName = json.Get("image");
            opacity = json.GetFloat("opacity");
            slice = json.ContainsKey("slice") ? json.Get("slice") : "";
            imageName = json.Get("imageName");
        }

        public override GameObject Render(Renderer renderer)
        {
            var go = CreateUIGameObject(renderer);

            var rect = go.GetComponentByElement<RectTransform>();
            rect.anchoredPosition = renderer.CalcPosition(canvasPosition, sizeDelta);
            rect.sizeDelta = sizeDelta;

            var tex = renderer.GetSprite(spriteName, imageName, slice);
            if (tex.GetType() == typeof(Sprite))
            {
                var image = go.AddComponentByElement<Image>();
                image.sprite = tex as Sprite;
                image.type = string.IsNullOrEmpty(slice) ? Image.Type.Simple : Image.Type.Sliced;
                image.color = new Color(1.0f, 1.0f, 1.0f, opacity / 100.0f);
                // 默认配置
                image.raycastTarget = false;
                image.maskable = false;
            }
            else if (tex.GetType() == typeof(Texture2D))
            {
                var image = go.AddComponentByElement<RawImage>();
                image.texture = tex as Texture2D;
                image.color = new Color(1.0f, 1.0f, 1.0f, opacity / 100.0f);
                // 默认配置
                image.raycastTarget = false;
                image.maskable = false;
            }
            else
            {
                Debug.LogError(spriteName + "导入错误");
            }

            SetStretch(go, renderer);
            SetPivot(go, renderer);

            return go;
        }

        public override Area CalcArea()
        {
            return Area.FromPositionAndSize(canvasPosition, sizeDelta);
        }
    }

    public sealed class TextElement : Element
    {
        private string message;
        private string font;
        private float fontSize;
        private string align;
        private float virtualHeight;
        private Color fontColor;

        //描边
        private bool enableStroke;
        private int strokeSize;
        private Color strokeColor;

        //投影
        private bool enableShadow;
        private int shadowDis;
        private Color shadowColor;
        private int shadowBlur;
        private int shadowAngle;

        //渐变
        private bool enableGradient;
        private int gradientAngle;
        private Color gradientColor0;
        private Color gradientColor1;

        //字体基础属性
        private bool isVertical;
        private bool isItalic;

        public TextElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
            message = json.Get("text");
            font = json.Get("font");
            font = Psd2UguiSettingsManager.RemapFontName(font);
            fontSize = json.GetFloat("size");
            align = json.Get("align");
            if (json.ContainsKey("strokeSize"))
            {
                enableStroke = true;
                strokeSize = json.GetInt("strokeSize");
                strokeColor = EditorUtil.HexToColor(json.Get("strokeColor"));
            }
            if (json.ContainsKey("shadowColor"))
            {
                enableShadow = true;
                shadowColor = EditorUtil.HexToColor(json.Get("shadowColor"));
                shadowDis = json.GetInt("shadowDis");
                shadowBlur = json.GetInt("shadowBlur");
                shadowAngle = json.GetInt("shadowAngle");
            }
            if (json.ContainsKey("gradientAngle"))
            {
                enableGradient = true;
                gradientAngle = json.GetInt("gradientAngle");
                gradientColor0 = EditorUtil.HexToColor(json.Get("gradientColor0"));
                gradientColor1 = EditorUtil.HexToColor(json.Get("gradientColor1"));
            }
            fontColor = EditorUtil.HexToColor(json.Get("color"));
            sizeDelta = json.GetVector2("w", "h");
            canvasPosition = json.GetVector2("x", "y");
            isVertical = json.ContainsKey("vertical");
            isItalic = json.ContainsKey("isItalic");
        }

        public override GameObject Render(Renderer renderer)
        {
            var go = CreateUIGameObject(renderer);

            var fontInfo = renderer.GetFont(font);
            var rect = go.GetComponentByElement<RectTransform>();
            rect.anchoredPosition = renderer.CalcPosition(canvasPosition, sizeDelta);
            rect.sizeDelta = sizeDelta + fontInfo.Item3;

            var text = go.AddComponentByElement<Text>();

            text.text = message;
            text.font = fontInfo.Item1;
            text.fontSize = Mathf.RoundToInt(fontSize);
            text.color = fontColor;

            //默认配置
            text.raycastTarget = false;
            text.maskable = false;

            //加粗
            if (fontInfo.Item2)
            {
                text.fontStyle = FontStyle.Bold;
            }

            //PS中默认是这个格式
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            if (isVertical)
            {
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            if (isItalic)
            {
                text.fontStyle = FontStyle.Italic;
            }

            var fixedPos = rect.anchoredPosition;
            //PS中字体默认位置对应tmp的中对齐
            switch (align)
            {
                case "left":
                    text.alignment = TextAnchor.MiddleLeft;
                    break;

                case "center":
                    text.alignment = TextAnchor.MiddleCenter;
                    break;

                case "right":
                    text.alignment = TextAnchor.MiddleRight;
                    break;
            }
            rect.anchoredPosition = fixedPos;

            if (enableStroke)
            {
                var outline = text.gameObject.AddComponentByElement<Outline>();
                outline.effectColor = strokeColor;
                outline.effectDistance = new Vector2(strokeSize / 2, strokeSize / 2);
            }
            if (enableShadow)
            {
                var shadow = text.gameObject.AddComponentByElement<Shadow>();
                shadow.effectColor = shadowColor;
                float offsetX = -shadowDis * Mathf.Cos(Mathf.Deg2Rad * shadowAngle);
                float offsetY = -shadowDis * Mathf.Sin(Mathf.Deg2Rad * shadowAngle);
                shadow.effectDistance = new Vector2(offsetX, offsetY);
            }

            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }

        public override Area CalcArea()
        {
            return Area.FromPositionAndSize(canvasPosition, sizeDelta);
        }
    }

    public sealed class ButtonElement : GroupElement
    {
        bool hasBG = false;
        private string bgSpriteName;
        private string bgSlice;
        private float bgOpacity = 0;
        private Vector2 bgPos = Vector2.zero;
        private Vector2 bgSize = Vector2.zero;
        private string imageName;
        private bool isMask;
        private bool isSoft;

        public ButtonElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
            elements = new List<Element>();
            var jsonElements = json.Get<List<object>>("elements");
            Element pre = null;
            isMask = json.ContainsKey("mask");
            if (isMask)
            {
                isSoft = json.Get("mask") == "soft";
            }
            if (jsonElements != null)
                foreach (var jsonElement in jsonElements)
                {
                    Dictionary<string, object> eleJson = jsonElement as Dictionary<string, object>;
                    var x = stretchX;
                    var y = stretchY;
                    stretchX = false;
                    stretchY = false;
                    Element e = ElementFactory.Generate(eleJson, this);
                    if (eleJson.Get("type") == "Image")
                    {
                        //顶替了前面的
                        if (hasBG && pre != null)
                        {
                            elements.Add(pre);
                        }

                        hasBG = true;
                        bgSpriteName = eleJson.Get("image");
                        bgSlice = eleJson.ContainsKey("slice") ? eleJson.Get("slice") : "";
                        bgOpacity = eleJson.GetFloat("opacity");
                        bgPos = eleJson.GetVector2("x", "y");
                        bgSize = eleJson.GetVector2("w", "h");
                        imageName = eleJson.Get("imageName");
                        pre = e;
                    }
                    else
                    {
                        elements.Add(e);
                    }
                    stretchX = x;
                    stretchY = y;
                }

            if (json.ContainsKey("image"))
            {
                hasBG = true;
                bgSpriteName = json.Get("image");
                bgSlice = json.ContainsKey("slice") ? json.Get("slice") : "";
                bgOpacity = json.GetFloat("opacity");
                bgPos = json.GetVector2("x", "y");
                bgSize = json.GetVector2("w", "h");
                imageName = json.Get("imageName");
            }
            elements.Reverse();
        }

        public override Area CalcArea()
        {
            return new Area(bgPos, bgPos + bgSize);
        }

        public override GameObject Render(Renderer renderer)
        {
            GameObject go = CreateUIGameObject(renderer);

            if (hasBG)
            {
                var button = go.AddComponentByElement<Button>();
                var rect = go.GetComponentByElement<RectTransform>();
                rect.anchoredPosition = renderer.CalcPosition(bgPos, bgSize);
                rect.sizeDelta = bgSize;

                var tex = renderer.GetSprite(bgSpriteName, imageName, bgSlice);
                if (tex.GetType() == typeof(Sprite))
                {
                    var image = go.AddComponentByElement<Image>();
                    image.sprite = tex as Sprite;
                    image.type = string.IsNullOrEmpty(bgSlice) ? Image.Type.Simple : Image.Type.Sliced;
                    image.color = new Color(1.0f, 1.0f, 1.0f, bgOpacity / 100.0f);
                    button.targetGraphic = image;

                    //默认配置
                    image.raycastTarget = true;
                }
                else if (tex.GetType() == typeof(Texture2D))
                {
                    var image = go.AddComponentByElement<RawImage>();
                    image.texture = tex as Texture2D;
                    image.color = new Color(1.0f, 1.0f, 1.0f, bgOpacity / 100.0f);
                    button.targetGraphic = image;
                }
                else
                {
                    Debug.LogError(bgSpriteName + "导入错误");
                }

                if (isMask)
                {
                    var mask = go.AddComponentByElement<Mask>();
                    mask.showMaskGraphic = true;
                }
            }
            else
            {
                Debug.LogWarningFormat("bunton {0} can't find a background ", name);
            }
            RenderChildren(renderer, go);
            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }
    }

    public sealed class MaskElement : GroupElement
    {
        bool hasBG = false;
        private string bgSpriteName;
        private string bgSlice;
        private float bgOpacity = 0;
        private Vector2 bgPos = Vector2.zero;
        private Vector2 bgSize = Vector2.zero;
        private string imageName;
        private bool isSoft;

        public MaskElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
            elements = new List<Element>();
            var jsonElements = json.Get<List<object>>("elements");
            Element pre = null;
            isSoft = json.Get("mask") == "soft";
            if (jsonElements != null)
                foreach (var jsonElement in jsonElements)
                {
                    Dictionary<string, object> eleJson = jsonElement as Dictionary<string, object>;
                    var x = stretchX;
                    var y = stretchY;
                    stretchX = false;
                    stretchY = false;
                    Element e = ElementFactory.Generate(eleJson, this);
                    if (eleJson.Get("type") == "Image")
                    {
                        //顶替了前面的
                        if (hasBG && pre != null)
                        {
                            elements.Add(pre);
                        }

                        hasBG = true;
                        bgSpriteName = eleJson.Get("image");
                        bgSlice = eleJson.ContainsKey("slice") ? eleJson.Get("slice") : "";
                        bgOpacity = eleJson.GetFloat("opacity");
                        bgPos = eleJson.GetVector2("x", "y");
                        bgSize = eleJson.GetVector2("w", "h");
                        imageName = eleJson.Get("imageName");
                    }
                    else
                    {
                        elements.Add(e);
                    }
                    stretchX = x;
                    stretchY = y;
                    pre = e;
                }
            elements.Reverse();
        }

        public override Area CalcArea()
        {
            return new Area(bgPos, bgPos + bgSize);
        }

        public override GameObject Render(Renderer renderer)
        {
            GameObject go = CreateUIGameObject(renderer);

            if (hasBG)
            {
                var mask = go.AddComponentByElement<Mask>();
                mask.showMaskGraphic = true;
                var rect = go.GetComponentByElement<RectTransform>();
                rect.anchoredPosition = renderer.CalcPosition(bgPos, bgSize);
                rect.sizeDelta = bgSize;

                var tex = renderer.GetSprite(bgSpriteName, imageName, bgSlice);
                if (tex.GetType() == typeof(Sprite))
                {
                    var image = go.AddComponentByElement<Image>();
                    image.sprite = tex as Sprite;
                    image.type = string.IsNullOrEmpty(bgSlice) ? Image.Type.Simple : Image.Type.Sliced;
                    image.color = new Color(1.0f, 1.0f, 1.0f, bgOpacity / 100.0f);
                }
                else if (tex.GetType() == typeof(Texture2D))
                {
                    var image = go.AddComponentByElement<RawImage>();
                    image.texture = tex as Texture2D;
                    image.color = new Color(1.0f, 1.0f, 1.0f, bgOpacity / 100.0f);
                }
                else
                {
                    Debug.LogError(bgSpriteName + "导入错误");
                }
            }
            else
            {
                Debug.LogWarningFormat("bunton {0} can't find a background ", name);
            }
            RenderChildren(renderer, go);
            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }
    }


    public sealed class ListElement : GroupElement
    {
        private string scroll;

        public ListElement(Dictionary<string, object> json, Element parent) : base(json, parent, true)
        {
            if (json.ContainsKey("scroll")) scroll = json.Get("scroll");
        }

        public override GameObject Render(Renderer renderer)
        {
            var go = CreateSelf(renderer, false);

            var content = new GameObject("Content");
            content.AddComponentByElement<RectTransform>();
            content.transform.SetParent(go.transform);

            SetupScroll(go, content);
            SetMaskImage(renderer, go, content);

            var items = CreateItems(renderer, go);

            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }

        private void SetupScroll(GameObject go, GameObject content)
        {
            var scrollRect = go.AddComponentByElement<ScrollRect>();
            scrollRect.content = content.GetComponentByElement<RectTransform>();
            scrollRect.viewport = content.GetComponentByElement<RectTransform>();

            if (scroll == "vertical")
            {
                scrollRect.vertical = true;
                scrollRect.horizontal = false;
                content.AddComponentByElement<VerticalLayoutGroup>();
            }
            else if (scroll == "horizontal")
            {
                scrollRect.vertical = false;
                scrollRect.horizontal = true;
                content.AddComponentByElement<HorizontalLayoutGroup>();
            }
        }

        private void SetMaskImage(Renderer renderer, GameObject go, GameObject content)
        {
            var dummyMaskImage = CreateDummyMaskImage(renderer);
            dummyMaskImage.transform.SetParent(go.transform);
            go.GetComponentByElement<RectTransform>().CopyTo(content.GetComponentByElement<RectTransform>());
            content.GetComponentByElement<RectTransform>().localPosition = Vector3.zero;
            var img = ImageProcessingUtility.GetImage(dummyMaskImage);

            MaskableGraphic maskImage = null;
            if (img.GetType() == BaumElementHelper.GetComponentTypeByElement<Image>())
            {
                maskImage = go.AddComponentByElement<Image>();
                Image curImg = img as Image;
                curImg.CopyTo(maskImage as Image);
            }
            else if (img.GetType() == BaumElementHelper.GetComponentTypeByElement<RawImage>())
            {
                maskImage = go.AddComponentByElement<RawImage>();
                RawImage curImg = img as RawImage;
                curImg.CopyTo(maskImage as RawImage);
            }

            GameObject.DestroyImmediate(dummyMaskImage);

            maskImage.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            go.AddComponentByElement<RectMask2D>();
        }

        private GameObject CreateDummyMaskImage(Renderer renderer)
        {
            var maskElement = elements.Find(x => (x is ImageElement));// && x.name.Equals("Area", StringComparison.OrdinalIgnoreCase)));
            GameObject maskImage;
            if (maskElement != null)
            {
                elements.Remove(maskElement);
                maskImage = maskElement.Render(renderer);
                maskImage.SetActive(false);
            }
            else
            {
                maskImage = new GameObject();
                maskImage.AddComponentByElement<RectTransform>();
                maskImage.AddComponentByElement<Image>();
            }
            return maskImage;
        }

        private List<GameObject> CreateItems(Renderer renderer, GameObject go)
        {
            var items = new List<GameObject>();
            foreach (var element in elements)
            {
                var item = element as GroupElement;
                if (item == null) throw new Exception(string.Format("{0}'s element {1} is not group", name, element.name));

                var itemObject = item.Render(renderer);
                itemObject.transform.SetParent(go.transform.Find("Content").transform);

                var rect = itemObject.GetComponentByElement<RectTransform>();
                var originalPosition = rect.anchoredPosition;
                if (scroll == "vertical")
                {
                    rect.anchorMin = new Vector2(0.5f, 1.0f);
                    rect.anchorMax = new Vector2(0.5f, 1.0f);
                    rect.anchoredPosition = new Vector2(originalPosition.x, -rect.rect.height / 2f);
                }
                else if (scroll == "horizontal")
                {
                    rect.anchorMin = new Vector2(0.0f, 0.5f);
                    rect.anchorMax = new Vector2(0.0f, 0.5f);
                    rect.anchoredPosition = new Vector2(rect.rect.width / 2f, originalPosition.y);
                }

                items.Add(itemObject);
            }
            return items;
        }

        public override Area CalcArea()
        {
            return new Area(canvasPosition, canvasPosition + sizeDelta);
        }
    }


    public sealed class SliderElement : GroupElement
    {
        public SliderElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
        }

        public override GameObject Render(Renderer renderer)
        {
            var go = CreateSelf(renderer);

            RectTransform fillRect = null;
            RenderChildren(renderer, go, (g, element) =>
            {
                var image = element as ImageElement;
                if (fillRect != null || image == null) return;

                ImageProcessingUtility.GetImage(g).raycastTarget = false;
                if (element.name.Equals("Fill", StringComparison.OrdinalIgnoreCase)) fillRect = g.GetComponentByElement<RectTransform>();
            });

            var slider = go.AddComponentByElement<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            if (fillRect != null)
            {
                fillRect.localScale = Vector2.zero;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.anchoredPosition = Vector2.zero;
                fillRect.sizeDelta = Vector2.zero;
                fillRect.localScale = Vector3.one;
                slider.fillRect = fillRect;
            }

            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }
    }

    public sealed class ScrollbarElement : GroupElement
    {
        public ScrollbarElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
        }

        public override GameObject Render(Renderer renderer)
        {
            var go = CreateSelf(renderer);

            RectTransform handleRect = null;
            RenderChildren(renderer, go, (g, element) =>
            {
                var image = element as ImageElement;
                if (handleRect != null || image == null) return;
                if (element.name.Equals("Handle", StringComparison.OrdinalIgnoreCase)) handleRect = g.GetComponentByElement<RectTransform>();
                ImageProcessingUtility.GetImage(g).raycastTarget = false;
            });

            var scrollbar = go.AddComponentByElement<Scrollbar>();
            var handleImage = handleRect == null ? null : ImageProcessingUtility.GetImage(handleRect);
            if (handleImage != null)
            {
                handleRect.anchoredPosition = Vector2.zero;
                handleRect.anchorMin = new Vector2(0.0f, 0.0f);
                handleRect.anchorMax = new Vector2(1.0f, 0.0f);

                scrollbar.direction = Scrollbar.Direction.BottomToTop;
                scrollbar.value = 1.0f;
                scrollbar.targetGraphic = handleImage;
                scrollbar.handleRect = handleRect;

                handleRect.sizeDelta = Vector2.zero;
            }

            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }
    }

    public sealed class ToggleElement : GroupElement
    {
        public ToggleElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
        }

        public override GameObject Render(Renderer renderer)
        {
            var go = CreateSelf(renderer);

            Graphic lastImage = null;
            Graphic checkImage = null;
            RenderChildren(renderer, go, (g, element) =>
            {
                var image = element as ImageElement;
                if (image == null) return;
                if (lastImage == null) lastImage = ImageProcessingUtility.GetImage(g);
                if (element.name.Contains("Check") || element.name.Contains("check")) checkImage = ImageProcessingUtility.GetImage(g);
            });

            var toggle = go.AddComponentByElement<Toggle>();
            toggle.targetGraphic = lastImage;
            toggle.graphic = checkImage;

            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }
    }


    public sealed class NullElement : Element
    {
        public NullElement(Dictionary<string, object> json, Element parent) : base(json, parent)
        {
        }

        public override GameObject Render(Renderer renderer)
        {
            var go = CreateUIGameObject(renderer);
            SetStretch(go, renderer);
            SetPivot(go, renderer);
            return go;
        }

        public override Area CalcArea()
        {
            return Area.None();
        }
    }
}
