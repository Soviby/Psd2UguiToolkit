using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Baum2.Editor
{
    /// <summary>
    /// 重复纹理检测工具，说明文档如下
    /// </summary>
    public class ImageProcessingUtility
    {
        private const float threshold = 0.9f;
        private static float[,] mDctMatrix;
        private static float[,] mTransDctMatrix;
        public static int[] texSizes = new int[12]
        {
        1,2,4,8,16,32,64,128,256,512,1024,2048
        };

        public static MaskableGraphic GetImage(GameObject go)
        {
            return GetImage(go.transform);
        }

        public static MaskableGraphic GetImage(Component go)
        {
            MaskableGraphic img = go.GetComponentByElement<Image>();
            if (img == null)
            {
                img = go.GetComponentByElement<RawImage>();
            }
            if (img == null)
            {
                img = go.GetComponent<MaskableGraphic>();
            }

            return img;
        }

        private static int bluredSize
        {
            get
            {
                return Psd2UguiSettingsManager.Settings.blurSize;
            }
        }

        /// <summary>
        /// 判断两张纹理是否相同
        /// </summary>
        /// <param name="tex1"></param>
        /// <param name="tex2"></param>
        /// <returns></returns>
        public static bool CompareTexture(Texture2D tex1, Texture2D tex2)
        {
            int v = 0;
            Color[] color1 = tex1.GetPixels();
            Color[] color2 = tex2.GetPixels();
            for (int i = 0; i < color1.Length; i++)
            {
                if (color1[i] != color2[i])
                {
                    v++;
                }
                if (v >= threshold)
                {
                    return false;
                }
            }
            return true;
        }

        private static float[,] DCTMatrix
        {
            get
            {
                if (mDctMatrix == null || bluredSize != mDctMatrix.GetLength(0))
                {
                    mDctMatrix = CreateDCTMatrix(bluredSize);
                }
                return mDctMatrix;
            }
        }
        private static float[,] TransDCTMatrix
        {
            get
            {
                if (mTransDctMatrix == null || bluredSize != mTransDctMatrix.GetLength(0))
                {
                    mTransDctMatrix = Transpose(DCTMatrix);
                }
                return mTransDctMatrix;
            }
        }

        /// <summary>
        /// 返回模糊后的texture
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        static public Texture2D BluredTex(Texture2D source)
        {
            return DuplicateTexture(source, bluredSize, bluredSize);
        }

        /// <summary>
        /// 改变texture的大小，同时强制可读写
        /// </summary>
        /// <param name="source"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        static public Texture2D DuplicateTexture(Texture2D source, int width, int height)
        {
            RenderTexture renderTex = RenderTexture.GetTemporary(
                    width,
                    height,
                    0,
                    RenderTextureFormat.Default,
                    RenderTextureReadWrite.Linear);

            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            Texture2D readableText = new Texture2D(width, height);
            readableText.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readableText.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            return readableText;
        }

        public static string GetPHash(Texture2D tex)
        {
            if (Mathf.Max(tex.height, tex.width) < bluredSize)
            {
                return null;
            }
            tex = BluredTex(tex);
            var A = CreateDCTMatrix(tex.width);
            var Aa = Transpose(A);
            tex = Tex2Gray(tex);
            var dct = Multiply(Multiply(A, Image2F(tex)), Aa);
            var hash = GetHash(dct, AverageDCT(dct));
            return hash;
        }

        public static bool CompareTex(string hash1, string hash2)
        {
            return ComputeDistance(hash1, hash2) >= threshold;
        }

        //转灰度
        static Texture2D Tex2Gray(Texture2D tex)
        {
            Color color;
            for (int i = 0; i < tex.height; i++)
            {
                for (int j = 0; j < tex.width; j++)
                {
                    color = tex.GetPixel(j, i);
                    float gray = (color.r * 30 + color.b * 59 + color.b * 11) / 100;
                    tex.SetPixel(j, i, new Color(gray, gray, gray));
                }
            }
            return tex;
        }

        //图片转矩阵
        static float[,] Image2F(Texture2D tex)
        {
            int size = tex.width;
            float[,] f = new float[size, size];
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    f[i, j] = tex.GetPixel(i, j).r;
                }
            }
            return f;
        }
        //计算DCT矩阵
        static float[,] CreateDCTMatrix(int size)
        {
            //Debug.Log((float)Mathf.Cos(Mathf.PI));
            float[,] ret = new float[size, size];
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float angle = ((y + 0.5f) * Mathf.PI * x / size);
                    ret[x, y] = Cfunc(x, size) * (float)Mathf.Cos(angle);
                }
            }
            return ret;
        }

        static float Cfunc(int n, int size)
        {
            if (n == 0)
            {
                return Mathf.Sqrt(1f / size);
            }
            else
            {
                return Mathf.Sqrt(2f / size);
            }

        }

        //矩阵转置
        static float[,] Transpose(float[,] C)
        {
            int size = C.GetLength(0);
            float[,] ret = new float[size, size];
            for (var x = 0; x < size; x++)
            {
                for (var y = 0; y < size; y++)
                {
                    ret[y, x] = C[x, y];
                }
            }
            return ret;
        }

        //矩阵相乘
        static float[,] Multiply(float[,] C1, float[,] C2)
        {
            int size = C1.GetLength(0);
            float[,] ret = new float[size, size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    float sum1 = 0;
                    for (int k = 0; k < size; k++)
                    {
                        sum1 += C1[x, k] * C2[k, y];
                    }
                    ret[x, y] = sum1;
                }
            }
            return ret;
        }

        //DCT均值
        static float AverageDCT(float[,] dct)
        {
            int size = dct.GetLength(0);
            float aver = 0;
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    aver += dct[i, j];
                }
            }
            return aver / (size * size);
        }

        //获取当前图片pHash值
        static string GetHash(float[,] dct, float aver)
        {
            string hash = string.Empty;
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    hash += (dct[i, j] >= aver ? "1" : "0");
                }
            }
            return hash;
        }

        //计算两图片哈希值的汉明距离
        static float ComputeDistance(string hash1, string hash2)
        {
            float dis = 0;
            for (int i = 0; i < hash1.Length; i++)
            {
                if (hash1[i] == hash2[i])
                {
                    dis++;
                }
            }
            return dis / hash1.Length;
        }
    }
}
