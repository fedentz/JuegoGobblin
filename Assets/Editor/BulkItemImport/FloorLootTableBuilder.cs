using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Project.Interaction;

namespace Project.EditorTools
{
    // Toma Assets/Editor/BulkItemImport/items_by_floor.json (armado a partir del CSV de ítems
    // ya clasificado por piso) y crea/actualiza una LootTable por piso, reusando los ItemData.asset
    // que ya generó BulkItemImporter (no crea ítems nuevos, solo arma las tablas).
    public static class FloorLootTableBuilder
    {
        private const string DataJsonPath = "Assets/Editor/BulkItemImport/items_by_floor.json";
        private const string ItemsRootFolder = "Assets/LootTables/Items";
        private const string OutputFolder = "Assets/LootTables";

        [Serializable]
        private class FloorItemEntry
        {
            public string category;
            public string filenameStem;
            public float dropWeight;
        }

        [Serializable]
        private class FloorGroup
        {
            public string floor;
            public List<FloorItemEntry> items;
        }

        [Serializable]
        private class FloorData
        {
            public List<FloorGroup> floors;
        }

        private static readonly Dictionary<string, string> FloorToAssetName = new Dictionary<string, string>
        {
            { "1er Piso", "LootTable_Piso1" },
            { "2do Piso", "LootTable_Piso2" },
            { "3er Piso", "LootTable_Piso3" },
        };

        [MenuItem("Tools/Loot/Build Floor Loot Tables")]
        public static void Run()
        {
            AssetDatabase.Refresh();

            if (!File.Exists(DataJsonPath))
            {
                Debug.LogError($"[FloorLootTableBuilder] No encontré {DataJsonPath}");
                return;
            }

            string rawJson = File.ReadAllText(DataJsonPath);
            FloorData data = JsonUtility.FromJson<FloorData>(rawJson);
            if (data == null || data.floors == null || data.floors.Count == 0)
            {
                Debug.LogError("[FloorLootTableBuilder] El JSON está vacío o no se pudo parsear.");
                return;
            }

            foreach (FloorGroup group in data.floors)
            {
                if (!FloorToAssetName.TryGetValue(group.floor, out string assetName))
                {
                    Debug.LogWarning($"[FloorLootTableBuilder] Piso desconocido '{group.floor}', se salteó.");
                    continue;
                }

                var resolved = new List<(ItemData item, float dropWeight)>();
                foreach (FloorItemEntry entry in group.items)
                {
                    ItemData item = FindItemData(entry.category, entry.filenameStem);
                    if (item == null)
                    {
                        Debug.LogWarning($"[FloorLootTableBuilder] No encontré el ItemData de '{entry.category}/{entry.filenameStem}' ({group.floor}).");
                        continue;
                    }
                    resolved.Add((item, entry.dropWeight));
                }

                string path = $"{OutputFolder}/{assetName}.asset";
                LootTable table = BuildLootTable(path, resolved);
                Debug.Log($"[FloorLootTableBuilder] {group.floor}: {resolved.Count}/{group.items.Count} ítems -> {path}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FloorLootTableBuilder] Listo.");
        }

        private static ItemData FindItemData(string category, string filenameStem)
        {
            string folder = $"{ItemsRootFolder}/{category}";
            if (!AssetDatabase.IsValidFolder(folder)) return null;

            string[] guids = AssetDatabase.FindAssets("t:ItemData", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith($"_{filenameStem}.asset", StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<ItemData>(path);
                }
            }
            return null;
        }

        private static LootTable BuildLootTable(string path, List<(ItemData item, float dropWeight)> items)
        {
            LootTable table = AssetDatabase.LoadAssetAtPath<LootTable>(path);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<LootTable>();
                AssetDatabase.CreateAsset(table, path);
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
    }
}
