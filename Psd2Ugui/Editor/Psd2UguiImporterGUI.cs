using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Baum2.Editor
{
    public class Psd2UguiImporterGUI : EditorWindow
    {
        private Psd2UguiTextureImporter.TextureInfo texInfo1;
        private Psd2UguiTextureImporter.TextureInfo texInfo2;
        private float rectSize = 500f;
        private float rectInterval = 50f;

        private void OnGUI()
        {
            float width;
            float height;
            Texture tex1 = texInfo1.originTex;
            Texture tex2 = texInfo2.originTex;
            //宽图
            if (tex1.width > tex1.height)
            {
                width = rectSize;
                height = rectSize * tex1.height / tex1.width;
            }
            else//长图
            {
                width = rectSize;
                height = rectSize * tex1.width / tex1.height;
            }
            Rect rect = new Rect(0, 0, width, height);
            GUI.DrawTexture(rect, tex1, ScaleMode.StretchToFill);
            rect = new Rect(0, height, width, height);
            GUI.TextArea(rect, string.Format("路径：{0}\n大小：{1}X{2}\n{3}", texInfo1.path, tex1.width, tex1.height, texInfo1.isPublicTex ? "公共图片" : "私有图片"));
            rect = new Rect(width + rectInterval, 0, width, height);
            GUI.DrawTexture(rect, tex2, ScaleMode.StretchToFill);
            rect = new Rect(width + rectInterval, height, width, height);
            GUI.TextArea(rect, string.Format("路径：{0}\n大小：{1}X{2}", texInfo2.path, tex2.width, tex2.height));
            minSize = new Vector2(width * 2 + rectInterval, height + height);
        }
        public void SetTex(Psd2UguiTextureImporter.TextureInfo tex1, Psd2UguiTextureImporter.TextureInfo tex2)
        {
            texInfo1 = tex1;
            texInfo2 = tex2;
        }

        public bool Test()
        {
            Show();
            return EditorUtility.DisplayDialog("??", "testing", "yes", "no");
        }
    }
}