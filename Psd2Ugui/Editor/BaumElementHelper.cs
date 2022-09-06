using UnityEngine;
using System.Collections.Generic;
using System;

namespace Baum2.Editor
{
    public static class BaumElementHelper
    {

        // 第一个Type为目标类型，第二个Type为源类型
        private static Dictionary<Type, Type> ComponentMap = new Dictionary<Type, Type>();
        public static Type GetComponentTypeByElement<T>() where T : Component
        {
            var type = typeof(T);
            if (ComponentMap.ContainsKey(type))
                return ComponentMap[type];

            return type;
        }

        // 第一个Type为目标类型，第二个Type为源类型
        public static void AddComponentTypeByElement<T, M>() where T : Component where M : Component
        {
            var typeT = typeof(T);
            var typeM = typeof(M);
            if (ComponentMap.ContainsKey(typeT))
                ComponentMap[typeT] = typeM;
            else ComponentMap.Add(typeT, typeM);
        }

        public static T AddComponentByElement<T>(this GameObject go) where T : Component
        {
            var typeComponent = GetComponentTypeByElement<T>();

            return (T)go.AddComponent(typeComponent);
        }

        public static T GetComponentByElement<T>(this GameObject go) where T : Component
        {
            var typeComponent = GetComponentTypeByElement<T>();

            return (T)go.GetComponent(typeComponent);
        }


        public static T GetComponentByElement<T>(this Component go) where T : Component
        {
            var typeComponent = GetComponentTypeByElement<T>();

            return (T)go.GetComponent(typeComponent);
        }


        public class Image
        {

            public static bool IsBigTexture(Texture texture)
            {
                return texture.width * texture.height >= Psd2UguiSettingsManager.Settings.biggestSprite.x * Psd2UguiSettingsManager.Settings.biggestSprite.y;
            }

        }

    }
}