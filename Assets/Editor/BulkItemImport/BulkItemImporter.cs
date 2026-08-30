using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using Project.Interaction;

namespace Project.EditorTools
{
    // Toma Assets/Editor/BulkItemImport/item_icons_data.json (generado a partir de las 156
    // imágenes de Assets/Art/ItemIcons/) y crea: los Sprites, las entradas localizadas en la
    // tabla UI_HUD, los ItemData.asset, y una LootTable con todos ellos.
    public static class BulkItemImporter
    {
        private const string DataJsonPath = "Assets/Editor/BulkItemImport/item_icons_data.json";
        private const string IconsFolder = "Assets/Art/ItemIcons";
        private const string ItemsRootFolder = "Assets/LootTables/Items";
        private const string LootTableOutputPath = "Assets/LootTables/LootTable_RavenIcons.asset";
        private const string StringTableCollectionName = "UI_HUD";

        [Serializable]
        private class IconEntry
        {
            public string filename;
            public string sourceSubfolder;
            public string category;
            public string name_es;
            public string name_en;
            public int value;
            public float weight;
            public float dropWeight;
        }

        [Serializable]
        private class IconEntryList
        {
            public List<IconEntry> items;
        }

        [MenuItem("Tools/Loot/Bulk Import Item Icons")]
        public static void Run()
        {
            AssetDatabase.Refresh();

            if (!File.Exists(DataJsonPath))
            {
                Debug.LogError($"[BulkItemImporter] No encontré {DataJsonPath}");
                return;
            }

            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollections()
                .FirstOrDefault(c => c.TableCollectionName == StringTableCollectionName);
            if (collection == null)
            {
                Debug.LogError($"[BulkItemImporter] No encontré la String Table Collection '{StringTableCollectionName}'.");
                return;
            }

            string rawJson = File.ReadAllText(DataJsonPath);
            IconEntryList list = JsonUtility.FromJson<IconEntryList>("{\"items\":" + rawJson + "}");
            if (list == null || list.items == null || list.items.Count == 0)
            {
                Debug.LogError("[BulkItemImporter] El JSON está vacío o no se pudo parsear.");
                return;
            }

            var createdItems = new List<(ItemData item, float dropWeight)>();
            int total = list.items.Count;

            try
            {
                for (int i = 0; i < total; i++)
                {
                    IconEntry entry = list.items[i];
                    EditorUtility.DisplayProgressBar("Bulk Import Item Icons", $"{entry.name_es} ({i + 1}/{total})", (float)i / total);

                    Sprite sprite = LoadAsSprite(entry.filename);
                    if (sprite == null)
                    {
                        Debug.LogWarning($"[BulkItemImporter] No encontré el sprite para '{entry.filename}', se salteó.");
                        continue;
                    }

                    long keyId = EnsureLocalizedEntry(collection, entry);

                    ItemData item = ScriptableObject.CreateInstance<ItemData>();
                    item.icon = sprite;
                    item.weight = entry.weight;
                    item.value = entry.value;
                    item.itemName = BuildLocalizedString(collection, keyId);

                    string categoryFolder = EnsureCategoryFolder(entry.category);
                    string assetName = SanitizeFileName($"{entry.name_es}_{Path.GetFileNameWithoutExtension(entry.filename)}");
                    string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{categoryFolder}/{assetName}.asset");

                    AssetDatabase.CreateAsset(item, assetPath);
                    createdItems.Add((item, entry.dropWeight));
                }

                LootTable table = BuildLootTable(createdItems);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[BulkItemImporter] Listo: {createdItems.Count}/{total} ítems creados. Loot table en {LootTableOutputPath} (asignala al Cofre a mano).");
                Selection.activeObject = table;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static Sprite LoadAsSprite(string filename)
        {
            string path = $"{IconsFolder}/{filename}";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Crea (si no existe) una entrada en la tabla compartida y su texto en cada locale (es/en).
        private static long EnsureLocalizedEntry(StringTableCollection collection, IconEntry entry)
        {
            string key = SanitizeFileName($"Item_{entry.name_es}_{Path.GetFileNameWithoutExtension(entry.filename)}");

            SharedTableData.SharedTableEntry sharedEntry = collection.SharedData.GetEntry(key)
                ?? collection.SharedData.AddKey(key);
            long keyId = sharedEntry.Id;

            foreach (StringTable table in collection.StringTables)
            {
                bool isSpanish = table.LocaleIdentifier.Code.StartsWith("es", StringComparison.OrdinalIgnoreCase);
                string text = isSpanish ? entry.name_es : entry.name_en;
                table.AddEntry(keyId, text);
                EditorUtility.SetDirty(table);
            }

            EditorUtility.SetDirty(collection.SharedData);
            return keyId;
        }

        private static LocalizedString BuildLocalizedString(StringTableCollection collection, long keyId)
        {
            var localized = new LocalizedString
            {
                TableReference = collection.SharedData.TableCollectionNameGuid,
                TableEntryReference = keyId
            };
            return localized;
        }

        private static string EnsureCategoryFolder(string category)
        {
            string folderName = string.IsNullOrEmpty(category) ? "Otro" : category;
            string path = $"{ItemsRootFolder}/{folderName}";

            if (!AssetDatabase.IsValidFolder(ItemsRootFolder))
            {
                AssetDatabase.CreateFolder("Assets/LootTables", "Items");
            }
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(ItemsRootFolder, folderName);
            }

            return path;
        }

        private static LootTable BuildLootTable(List<(ItemData item, float dropWeight)> items)
        {
            LootTable table = AssetDatabase.LoadAssetAtPath<LootTable>(LootTableOutputPath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<LootTable>();
                AssetDatabase.CreateAsset(table, LootTableOutputPath);
            }

            var so = new SerializedObject(table);
            SerializedProperty entriesProp = so.FindProperty("entries");
            entriesProp.ClearArray();

            for (int i = 0; i < items.Count; i++)
            {
                entriesProp.InsertArrayElementAtIndex(i);
                SerializedProperty elem = entriesProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("item").objectReferenceValue = items[i].item;
                elem.FindPropertyRelative("dropWeight").floatValue = items[i].dropWeight;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(table);
            return table;
        }

        private static string SanitizeFileName(string raw)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                raw = raw.Replace(c, '_');
            }
            return raw.Replace(" ", "");
        }
    }
}
