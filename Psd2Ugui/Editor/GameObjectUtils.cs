using UnityEngine;

namespace Baum2.Editor
{
    static class GameObjectUtils
    {

        /// <summary>
        /// 递归搜索第一个匹配
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="go"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        static public T FindInChild<T>(this GameObject go, string name = "") where T : Component
        {
            if (go == null) return null;
            T comp = null;
            if (!string.IsNullOrEmpty(name) && !go.name.Contains(name))
            {
                comp = null;
            }
            else
                comp = go.GetComponent<T>();
            if (comp == null)
            {
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    comp = FindInChild<T>(go.transform.GetChild(i).gameObject, name);
                    if (comp)
                        return comp;
                }
            }

            return comp;
        }

        /// <summary>
        /// 递归搜索第一个匹配
        /// </summary>
        /// <param name="go"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        static public GameObject FindInChild(this GameObject go, string name = "")
        {
            var tf = FindInChild<Transform>(go, name);
            return tf ? tf.gameObject : null;
        }

    }
}