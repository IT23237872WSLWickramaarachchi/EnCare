using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace EnCare.Editor
{
    public static class FixTMPFontAsset
    {
        [MenuItem("Tools/EnCare/Fix TMP Font Serialization Warnings")]
        [InitializeOnLoadMethod]
        public static void FixFontAssetSerialization()
        {
            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (fontAsset != null)
                {
                    EditorUtility.SetDirty(fontAsset);
                    count++;
                }
            }

            if (count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[EnCare] Successfully marked dirty and saved {count} TMP Font Asset(s) (including LiberationSans SDF - Fallback) to persist serialized UnitsPerEM values.");
            }

            // Ensure Basket.prefab is up-to-date with XRGrabInteractable & Rigidbody
            CleanupSceneBuilder.CreateBasketPrefab();
        }
    }
}
