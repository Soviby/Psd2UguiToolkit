using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace Baum2.Editor
{
    public static class EditorUtil
    {
        public static string ImportDirectoryPath
        {
            get
            {
                if (Psd2UguiSettingsManager.Settings != null)
                    return Psd2UguiSettingsManager.Settings.importPath;
                return null;
            }
        }

        public static string ToUnityPath(string path)
        {
            path = path.Substring(path.IndexOf("Assets", System.StringComparison.Ordinal));
            if (path.EndsWith("/", System.StringComparison.Ordinal) || path.EndsWith("\\", System.StringComparison.Ordinal)) path = path.Substring(0, path.Length - 1);
            return path.Replace("\\", "/");
        }

        public static string GetBaumSpritesPath()
        {
            return Psd2UguiSettingsManager.Settings.spritesPath;
        }

        public static string GetBaumBigSpritesPath()
        {
            return Psd2UguiSettingsManager.Settings.bgPath;
        }

        public static string GetBaumPrefabsPath()
        {
            return Psd2UguiSettingsManager.Settings.prefabPath;
        }

        public static string GetBaumFontsPath()
        {
            return Psd2UguiSettingsManager.Settings.fontPath;
        }

        public static Color HexToColor(string hex)
        {
            if(string.IsNullOrEmpty(hex))
            {
                return new Color32(0, 0, 0, 255);
            }
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            return new Color32(r, g, b, 255);
        }

        public static RectTransform CopyTo(this RectTransform self, RectTransform to)
        {
            to.sizeDelta = self.sizeDelta;
            to.position = self.position;
            return self;
        }

        public static Image CopyTo(this Image self, Image to)
        {
            to.sprite = self.sprite;
            to.type = self.type;
            to.color = self.color;
            return self;
        }

        public static RawImage CopyTo(this RawImage self, RawImage to)
        {
            to.texture = self.texture;
            to.color = self.color;
            return self;
        }

        public static string ComputeMd5(string path)
        {
            byte[] b;
            using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(path))
            {
                System.Drawing.ImageConverter converter = new System.Drawing.ImageConverter();
                b = (byte[])converter.ConvertTo(bmp, typeof(byte[]));
            }
            var md5 = Encode(b);
            return md5;
        }

        public static string Encode(byte[] bytes)
        {
            byte[] output;

            // 计算md5
            using (var md5 = new MD5CryptoServiceProvider())
            {
                output = md5.ComputeHash(bytes);
            }

            var sb = new StringBuilder();
            foreach (var t in output)
                sb.Append(t.ToString("x2"));

            return sb.ToString();
        }

        public static string Encode(string input)
        {
            // 获取文本数据的字节内容
            var bytes = Encoding.UTF8.GetBytes(input);

            return Encode(bytes);
        }
    }
}
