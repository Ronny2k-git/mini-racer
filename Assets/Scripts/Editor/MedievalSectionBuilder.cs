using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds "Endless Section Medieval PF" as a variant of the base section, using the KayKit Medieval Hexagon pack.
// Re-run from Tools > Mini Racer > Build Medieval Section (it overwrites the prefab).
public static class MedievalSectionBuilder
{
    const string PackPath = "Assets/Models/KayKit_Medieval_Hexagon_Pack_1.0_FREE/Assets/fbx(unity)";
    const string TexturePath = PackPath + "/tiles/base/hexagons_medieval.png";
    const string BasePrefabPath = "Assets/Prefab/Endless Sections/Endless Section Base PF.prefab";
    const string OutputPrefabPath = "Assets/Prefab/Endless Sections/Endless Section Medieval PF.prefab";
    const string TemplateMaterialPath = "Assets/Materials/Ground Halloween.mat";
    const string GroundMaterialPath = "Assets/Materials/Ground Medieval.mat";
    const string ScenePath = "Assets/Scenes/Stage.unity";

    // Section length is fixed by EndlessLevelHandler (26), so only the width grows
    const float HalfLength = 13f;
    const float GroundWidth = 64f;
    const float HalfWidth = GroundWidth / 2f;

    // World width a building's hex footprint should have
    const float HexWorldSize = 3.2f;

    // Z position of the river crossing the road
    const float RiverZ = 4f;

    struct Footprint { public float x, z, r; }

    static Dictionary<string, GameObject> models;
    static List<Footprint> footprints;
    static System.Random rng;
    static Transform root;
    static float unit;
    static float roadCenterX;
    static float roadHalfWidth;
    static float roadTop;
    static float riverHalfWidth;
    static float waterTop;

    [MenuItem("Tools/Mini Racer/Build Medieval Section")]
    public static void Build()
    {
        rng = new System.Random(2026);
        footprints = new List<Footprint>();
        LoadModels();

        Scene previewScene = EditorSceneManager.NewPreviewScene();
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
        GameObject section = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, previewScene);
        section.name = Path.GetFileNameWithoutExtension(OutputPrefabPath);
        root = section.transform;

        MeasureUnit();
        SetupGround();
        MeasureRoad();

        Transform props = Group("Medieval Props", root);
        BuildRiver(Group("River", props));
        BuildRoadDecor(Group("Road", props));
        BuildVillage(Group("Village", props));
        BuildMilitary(Group("Military", props));
        BuildMountains(Group("Mountains", props));
        BuildTrees(Group("Trees", props));
        BuildRocks(Group("Rocks", props));

        PrefabUtility.SaveAsPrefabAsset(section, OutputPrefabPath);
        EditorSceneManager.ClosePreviewScene(previewScene);

        AddToLevelHandler();
        AssetDatabase.SaveAssets();
        Debug.Log($"[MedievalSectionBuilder] Saved {OutputPrefabPath} (unit {unit:F3}, road half width {roadHalfWidth:F2}, river half width {riverHalfWidth:F2})");
    }

    // ---------- Setup ----------

    static void LoadModels()
    {
        models = new Dictionary<string, GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { PackPath }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            models[Path.GetFileNameWithoutExtension(path)] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
    }

    static void MeasureUnit()
    {
        GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(models["hex_grass"], root);
        Bounds b = WorldBounds(probe);
        unit = HexWorldSize / Mathf.Min(b.size.x, b.size.z);
        Object.DestroyImmediate(probe);
    }

    static void SetupGround()
    {
        Transform ground = root.Find("Ground");
        Vector3 scale = ground.localScale;
        ground.localScale = new Vector3(GroundWidth, scale.y, scale.z);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>(TemplateMaterialPath));
            AssetDatabase.CreateAsset(material, GroundMaterialPath);
        }
        material.SetColor("_BaseColor", SampleGrassColor());
        EditorUtility.SetDirty(material);
        ground.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    // Average colour of the top faces of hex_grass, so the ground matches the tiles and hills
    static Color SampleGrassColor()
    {
        Color fallback = new Color(0.43f, 0.69f, 0.33f);
        try
        {
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(TexturePath));
            Mesh mesh = models["hex_grass"].GetComponentInChildren<MeshFilter>().sharedMesh;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            Color sum = Color.black;
            int count = 0;
            for (int i = 0; i < normals.Length; i++)
            {
                if (normals[i].y < 0.95f) continue;
                sum += texture.GetPixelBilinear(uvs[i].x, uvs[i].y);
                count++;
            }
            return count > 0 ? sum / count : fallback;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MedievalSectionBuilder] Using fallback grass colour: {e.Message}");
            return fallback;
        }
    }

    static void MeasureRoad()
    {
        Renderer[] renderers = root.Find("Road").GetComponentsInChildren<Renderer>();
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        roadCenterX = b.center.x - root.position.x;
        roadHalfWidth = b.extents.x;
        roadTop = b.max.y - root.position.y;
    }

    // ---------- Areas ----------

    static void BuildRiver(Transform parent)
    {
        // Water sits just under the road surface so the road reads as a bridge over it
        waterTop = Mathf.Clamp(roadTop - 0.004f, 0.006f, 0.02f);
        float tileScale = unit * 0.85f;

        GameObject probe = Spawn("hex_water", parent, 0, 0, 0, tileScale);
        Bounds b = WorldBounds(probe);
        Object.DestroyImmediate(probe);

        bool cornersAlongX = b.size.x > b.size.z;
        float stepX = cornersAlongX ? b.size.x * 0.75f : b.size.x;
        float zigzag = cornersAlongX ? b.size.z * 0.5f : 0f;
        riverHalfWidth = (b.size.z + zigzag) * 0.5f;

        int index = 0;
        for (float x = -HalfWidth; x <= HalfWidth; x += stepX, index++)
        {
            float z = RiverZ + (index % 2 == 0 ? -zigzag : zigzag) * 0.5f;
            GameObject tile = Spawn("hex_water", parent, x, z, 0, tileScale);
            SnapTop(tile, waterTop);
        }

        // Plants and lilies along the banks and on the water
        string[] bankPlants = { "waterplant_A", "waterplant_B", "waterplant_C" };
        string[] lilies = { "waterlily_A", "waterlily_B" };
        for (int i = 0; i < 18; i++)
        {
            float x = RandomSideX(roadHalfWidth + 1.2f, HalfWidth - 2f);
            float side = i % 2 == 0 ? 1 : -1;
            float z = RiverZ + side * (riverHalfWidth - Range(0.1f, 0.5f));
            Spawn(Pick(bankPlants), parent, x, z, Range(0, 360), unit * Range(0.8f, 1.1f)).transform.position += Vector3.up * waterTop;
        }
        for (int i = 0; i < 10; i++)
        {
            float x = RandomSideX(roadHalfWidth + 1.5f, HalfWidth - 2f);
            float z = RiverZ + Range(-riverHalfWidth * 0.5f, riverHalfWidth * 0.5f);
            Spawn(Pick(lilies), parent, x, z, Range(0, 360), unit * Range(0.8f, 1.1f)).transform.position += Vector3.up * waterTop;
        }
    }

    static void BuildRoadDecor(Transform parent)
    {
        // Stone railings where the road crosses the river
        GameObject probe = Spawn("fence_stone_straight", parent, 0, 0, 0, unit * 0.7f);
        Bounds b = WorldBounds(probe);
        Object.DestroyImmediate(probe);
        float rotation = b.size.x > b.size.z ? 90 : 0;
        float length = Mathf.Max(b.size.x, b.size.z) * 0.98f;

        float spanStart = RiverZ - riverHalfWidth - 0.6f;
        float spanEnd = RiverZ + riverHalfWidth + 0.6f;
        foreach (float side in new[] { -1f, 1f })
        {
            float x = roadCenterX + side * (roadHalfWidth + 0.15f);
            for (float z = spanStart + length / 2; z < spanEnd + length / 2; z += length)
                Spawn("fence_stone_straight", parent, x, z, rotation, unit * 0.7f);
        }

        // Yellow banners along the road, alternating sides
        int flag = 0;
        for (float z = -11f; z <= 11f; z += 5.5f, flag++)
        {
            if (Mathf.Abs(z - RiverZ) < riverHalfWidth + 1f) continue;
            float side = flag % 2 == 0 ? 1 : -1;
            Spawn("flag_yellow", parent, roadCenterX + side * (roadHalfWidth + 0.7f), z, side > 0 ? -90 : 90, unit * 0.75f);
        }

        // Wooden fence along the farm side of the road
        GameObject fenceProbe = Spawn("fence_wood_straight", parent, 0, 0, 0, unit * 0.6f);
        Bounds fb = WorldBounds(fenceProbe);
        Object.DestroyImmediate(fenceProbe);
        float fenceRotation = fb.size.x > fb.size.z ? 90 : 0;
        float fenceLength = Mathf.Max(fb.size.x, fb.size.z) * 0.98f;
        for (float z = RiverZ + riverHalfWidth + 1.2f + fenceLength / 2; z < HalfLength - fenceLength / 2; z += fenceLength)
            Spawn("fence_wood_straight", parent, roadCenterX - (roadHalfWidth + 1.4f), z, fenceRotation, unit * 0.6f);
    }

    static void BuildVillage(Transform parent)
    {
        // Right side of the road: a small yellow village, facing the road
        float near = roadCenterX + roadHalfWidth + 2.6f;
        PlaceBuilding("building_home_A_yellow", parent, near, -10.5f, -90);
        PlaceBuilding("building_tavern_yellow", parent, near + 0.3f, -5.5f, -90);
        PlaceBuilding("building_home_B_yellow", parent, near, -0.8f, -90);
        PlaceBuilding("building_well_yellow", parent, near + 3.6f, -8f, 0, 0.7f);
        PlaceBuilding("building_market_yellow", parent, near + 4.2f, -11.2f, -90);
        PlaceBuilding("building_blacksmith_yellow", parent, near + 4.4f, -3.2f, -90);
        PlaceBuilding("building_church_yellow", parent, near + 8.6f, -7.5f, -90, 1.1f);
        PlaceBuilding("building_windmill_yellow", parent, near + 9f, -1.4f, -120, 1.1f);

        // Across the river
        PlaceBuilding("building_watermill_yellow", parent, near + 1.6f, RiverZ + riverHalfWidth + 2f, 180);
        PlaceBuilding("building_home_A_yellow", parent, near + 0.4f, 11f, -90);
        PlaceBuilding("building_home_B_yellow", parent, near + 5f, 10.2f, -60);

        // Village props
        Transform props = Group("Props", parent);
        string[] clutter = { "barrel", "crate_A_small", "crate_B_small", "sack", "bucket_water", "crate_A_big", "wheelbarrow", "pallet" };
        Scatter(clutter, 14, props, near - 1.2f, near + 6f, -12f, RiverZ - riverHalfWidth - 0.5f, 0.55f, 0.7f);
        Scatter(new[] { "resource_lumber", "resource_stone", "crate_long_A", "barrel" }, 5, props, near - 1f, near + 6f, RiverZ + riverHalfWidth + 0.5f, 12.4f, 0.55f, 0.7f);
    }

    static void BuildMilitary(Transform parent)
    {
        // Left side of the road: guard tower, barracks, archery range and the castle in the distance
        float near = roadCenterX - roadHalfWidth - 2.6f;
        PlaceBuilding("building_tower_A_yellow", parent, near, -9.5f, 90);
        PlaceBuilding("building_barracks_yellow", parent, near - 4.4f, -10.5f, 90);
        PlaceBuilding("building_archeryrange_yellow", parent, near - 4f, -4.6f, 90);
        PlaceBuilding("building_lumbermill_yellow", parent, near - 1.2f, 9.6f, 90);
        PlaceBuilding("building_castle_yellow", parent, near - 13f, -6f, 90, 1.5f);
        PlaceBuilding("building_tower_B_yellow", parent, near - 9f, -11f, 90);
        PlaceBuilding("building_tower_catapult_yellow", parent, near - 9.6f, -1.2f, 90);

        Transform props = Group("Props", parent);
        Spawn("target", props, near - 1.2f, -3.2f, 90, unit * 0.6f);
        Spawn("target", props, near - 1.2f, -5.8f, 90, unit * 0.6f);
        Spawn("weaponrack", props, near + 0.4f, -6.8f, 90, unit * 0.6f);
        Spawn("bucket_arrows", props, near - 0.2f, -2f, 0, unit * 0.6f);
        Spawn("tent", props, near - 6.8f, -7.6f, 60, unit * 0.65f);
        Spawn("flag_yellow", props, near - 2f, -12.2f, 90, unit * 0.75f);
        Scatter(new[] { "resource_lumber", "crate_long_B", "crate_long_C", "pallet" }, 5, props, near - 4f, near, RiverZ + riverHalfWidth + 0.8f, 12.4f, 0.55f, 0.7f);
    }

    static void BuildMountains(Transform parent)
    {
        string[] mountains = { "mountain_A_grass_trees", "mountain_B_grass_trees", "mountain_C_grass_trees", "mountain_A_grass", "mountain_B_grass", "mountain_C" };
        string[] hills = { "hills_A_trees", "hills_B_trees", "hills_C_trees", "hill_single_A", "hill_single_B", "hill_single_C" };

        // Mountain ridge on both outer edges, hills just in front of them
        foreach (float side in new[] { -1f, 1f })
        {
            for (float z = -10.5f; z <= 10.5f; z += 5.2f)
                TryPlace(Pick(mountains), parent, side * Range(26f, 29f), z + Range(-0.8f, 0.8f), Range(0, 360), unit * Range(2f, 2.6f), 1.5f, 0.7f);
            for (int i = 0; i < 5; i++)
                TryPlace(Pick(hills), parent, side * Range(18f, 23f), Range(-11f, 11f), Range(0, 360), unit * Range(1.2f, 1.6f), 3f, 0.8f);
        }
    }

    static void BuildTrees(Transform parent)
    {
        string[] forest = { "trees_A_large", "trees_B_large", "trees_A_medium", "trees_B_medium", "tree_single_A", "tree_single_B" };
        string[] singles = { "tree_single_A", "tree_single_B", "trees_A_small", "trees_B_small", "tree_single_A_cut", "tree_single_B_cut" };

        // Forests behind the lumbermill and past the village, then scattered trees everywhere else
        Scatter(forest, 16, parent, -18f, -6f, RiverZ + riverHalfWidth + 0.5f, 12.6f, 0.9f, 1.3f);
        Scatter(forest, 12, parent, 12f, 20f, RiverZ + riverHalfWidth + 0.5f, 12.6f, 0.9f, 1.3f);
        Scatter(forest, 8, parent, 14f, 20f, -12.6f, RiverZ - riverHalfWidth - 0.5f, 0.9f, 1.3f);
        Scatter(singles, 14, parent, -HalfWidth + 6f, HalfWidth - 6f, -12.6f, 12.6f, 0.8f, 1.1f);
        Scatter(new[] { "trees_A_cut", "trees_B_cut" }, 3, parent, -9f, -4f, RiverZ + riverHalfWidth + 0.5f, 12.4f, 0.8f, 1f);
    }

    static void BuildRocks(Transform parent)
    {
        string[] rocks = { "rock_single_A", "rock_single_B", "rock_single_C", "rock_single_D", "rock_single_E" };

        // Rocks lining the river banks plus a few scattered around
        for (int i = 0; i < 12; i++)
        {
            float side = i % 2 == 0 ? 1 : -1;
            TryPlace(Pick(rocks), parent, RandomSideX(roadHalfWidth + 1.5f, HalfWidth - 4f), RiverZ + side * (riverHalfWidth + Range(0.2f, 0.7f)), Range(0, 360), unit * Range(0.6f, 1f), 0f, 0.5f, ignoreRiver: true);
        }
        Scatter(rocks, 12, parent, -HalfWidth + 4f, HalfWidth - 4f, -12.6f, 12.6f, 0.6f, 1f);
    }

    // ---------- Placement helpers ----------

    static Transform Group(string name, Transform parent)
    {
        Transform group = new GameObject(name).transform;
        group.SetParent(parent, false);
        return group;
    }

    static GameObject Spawn(string model, Transform parent, float x, float z, float rotationY, float scale)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(models[model], parent);
        instance.transform.localPosition = new Vector3(x, 0, z);
        instance.transform.localRotation = Quaternion.Euler(0, rotationY, 0);
        instance.transform.localScale = Vector3.one * scale;
        SnapBottom(instance, 0);
        return instance;
    }

    static void PlaceBuilding(string model, Transform parent, float x, float z, float rotationY, float scale = 1f)
    {
        if (!TryPlace(model, parent, x, z, rotationY, unit * scale, 4f, 0.85f))
            Debug.LogWarning($"[MedievalSectionBuilder] No room for {model} near ({x}, {z})");
    }

    // Places a model at (x, z), or the nearest free spot within searchRadius
    static bool TryPlace(string model, Transform parent, float x, float z, float rotationY, float scale, float searchRadius, float overlap, bool ignoreRiver = false)
    {
        GameObject instance = Spawn(model, parent, x, z, rotationY, scale);
        Bounds b = WorldBounds(instance);
        float r = Mathf.Max(b.extents.x, b.extents.z) * overlap;

        for (float radius = 0; radius <= searchRadius; radius += 0.5f)
        {
            int steps = radius == 0 ? 1 : Mathf.CeilToInt(radius * 6);
            for (int s = 0; s < steps; s++)
            {
                float angle = s * Mathf.PI * 2 / steps;
                float px = x + Mathf.Cos(angle) * radius;
                float pz = z + Mathf.Sin(angle) * radius;
                if (!Fits(px, pz, r, ignoreRiver)) continue;
                instance.transform.localPosition = new Vector3(px, instance.transform.localPosition.y, pz);
                footprints.Add(new Footprint { x = px, z = pz, r = r });
                return true;
            }
        }
        Object.DestroyImmediate(instance);
        return false;
    }

    static void Scatter(string[] modelNames, int count, Transform parent, float xMin, float xMax, float zMin, float zMax, float scaleMin, float scaleMax)
    {
        int placed = 0;
        for (int attempt = 0; attempt < count * 12 && placed < count; attempt++)
        {
            if (TryPlace(Pick(modelNames), parent, Range(xMin, xMax), Range(zMin, zMax), Range(0, 360), unit * Range(scaleMin, scaleMax), 0f, 0.8f))
                placed++;
        }
    }

    // Keeps things off the road, out of the river, inside the section and apart from each other
    static bool Fits(float x, float z, float r, bool ignoreRiver)
    {
        if (Mathf.Abs(z) + r > HalfLength - 0.2f) return false;
        if (Mathf.Abs(x) + r > HalfWidth) return false;
        if (Mathf.Abs(x - roadCenterX) - r < roadHalfWidth + 0.6f) return false;
        if (!ignoreRiver && Mathf.Abs(z - RiverZ) - r < riverHalfWidth + 0.2f) return false;
        foreach (Footprint f in footprints)
        {
            float dx = f.x - x, dz = f.z - z;
            if (dx * dx + dz * dz < (f.r + r) * (f.r + r)) return false;
        }
        return true;
    }

    static void SnapBottom(GameObject instance, float localY)
    {
        Bounds b = WorldBounds(instance);
        instance.transform.position += Vector3.up * (root.position.y + localY - b.min.y);
    }

    static void SnapTop(GameObject instance, float localY)
    {
        Bounds b = WorldBounds(instance);
        instance.transform.position += Vector3.up * (root.position.y + localY - b.max.y);
    }

    static Bounds WorldBounds(GameObject instance)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    static float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

    static string Pick(string[] options) => options[rng.Next(options.Length)];

    static float RandomSideX(float min, float max) => roadCenterX + (rng.Next(2) == 0 ? -1 : 1) * Range(min, max);

    // ---------- Scene ----------

    // Adds the new section to the EndlessLevelHandler in Stage so it shows up while driving
    static void AddToLevelHandler()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefabPath);
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = false;
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, Application.isBatchMode ? OpenSceneMode.Single : OpenSceneMode.Additive);
            openedHere = true;
        }

        EndlessLevelHandler handler = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EndlessLevelHandler>(true)).FirstOrDefault();
        if (handler == null) return;

        SerializedObject serialized = new SerializedObject(handler);
        SerializedProperty list = serialized.FindProperty("sectionsPreFabs");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == prefab) return;

        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = prefab;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);

        if (openedHere)
        {
            EditorSceneManager.SaveScene(scene);
            if (!Application.isBatchMode) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
