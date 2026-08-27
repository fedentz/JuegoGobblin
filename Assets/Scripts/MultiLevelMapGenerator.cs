using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Genera un dungeon 3D de M\u00daLTIPLES PISOS a partir de una LISTA de im\u00e1genes (una por piso),
/// donde cada bloque de color representa un prefab distinto (pared, escalera, piso, puerta, columna,
/// esquina interna).
///
/// IMPORTANTE - Configuraci\u00f3n de cada textura en el Import Settings:
///  - Read/Write Enabled: ON
///  - Non-Power of 2: None
///  - Compression: None
///  - Filter Mode: Point (No Filter)
///
/// v3: agrega dos cambios grandes sobre la v2:
///  1) M\u00daLTIPLES PISOS: en vez de una sola Map Image, ahora se asigna una LISTA de im\u00e1genes
///     (Floor Images). Cada imagen es un piso completo, y se apilan autom\u00e1ticamente (por defecto
///     hacia ABAJO, como un dungeon que desciende) separadas por Floor Height.
///  2) ESCALERAS COMO BLOQUE: el prefab de Stairs ahora es una sola pieza que ocupa varias celdas
///     (t\u00edpicamente 4x3). El script detecta el GRUPO CONTIGUO de celdas de Stairs en la imagen,
///     calcula el centro de su bounding box, y coloca UNA sola instancia ah\u00ed, con la rotaci\u00f3n
///     POR DEFECTO del prefab (sin rotar por c\u00f3digo) \u2014 si necesit\u00e1s otra orientaci\u00f3n, gir\u00e1 el
///     bloque dibujado en la imagen de ese piso.
/// </summary>
public class MultiLevelMapGenerator : MonoBehaviour
{
    private enum TileType { None, Wall, Stairs, Floor, Door, Column, InnerCorner }

    [Header("Imagen fuente (un piso por imagen)")]
    [Tooltip("Una imagen por piso, en orden. La primera (\u00edndice 0) es el piso de arriba; las siguientes se apilan seg\u00fan 'Stack Floors Downward'.")]
    public List<Texture2D> floorImages = new List<Texture2D>();

    [Tooltip("Tama\u00f1o en p\u00edxeles de UN bloque/tile en la imagen. Se asume igual para todos los pisos.")]
    public float pixelsPerTile = 90f;

    // Campo de trabajo interno: apunta a la imagen del piso que se est\u00e1 procesando en cada momento.
    // Se usa internamente para no tener que pasar la textura como par\u00e1metro por todos lados.
    private Texture2D mapImage;

    [Header("Prefabs")]
    public GameObject wallPrefab;

    [Tooltip("Prefab de escalera. Ahora es UNA SOLA pieza que ocupa varias celdas (t\u00edpicamente 4x3). Se coloca UNA vez por cada grupo contiguo de celdas de Stairs en la imagen, con su rotaci\u00f3n POR DEFECTO (sin rotar por c\u00f3digo).")]
    public GameObject stairsPrefab;
    public GameObject floorPrefab;

    [Tooltip("Prefab de puerta para TRAMOS HORIZONTALES (cuando la pared corre izquierda-derecha). Ya tiene que venir con la rotaci\u00f3n correcta como asset; el script NO le aplica ninguna rotaci\u00f3n extra por c\u00f3digo.")]
    public GameObject doorPrefabHorizontal;

    [Tooltip("Prefab de puerta para TRAMOS VERTICALES (cuando la pared corre arriba-abajo). Ya tiene que venir con la rotaci\u00f3n correcta como asset; el script NO le aplica ninguna rotaci\u00f3n extra por c\u00f3digo.")]
    public GameObject doorPrefabVertical;

    [Header("Prefabs adicionales")]
    [Tooltip("Se genera uno por cada celda de Piso, a la altura indicada m\u00e1s abajo.")]
    public GameObject ceilingPrefab;

    [Tooltip("Se genera en cada esquina donde dos tramos de pared se cruzan en \u00e1ngulo recto, y en el v\u00e9rtice de cada esquina interna.")]
    public GameObject columnPrefab;

    [Header("Colores (coinciden con tu leyenda)")]
    public Color32 wallColor = new Color32(0x49, 0x5b, 0x24, 0xFF);
    public Color32 stairsColor = new Color32(0x24, 0x89, 0xc7, 0xFF);
    public Color32 floorColor = new Color32(0x8e, 0x21, 0x21, 0xFF);
    public Color32 doorColor = new Color32(0xf1, 0xaf, 0x15, 0xFF);

    [Tooltip("Color que marca en la imagen d\u00f3nde va una COLUMNA. Se coloca ah\u00ed y se empuja contra la(s) pared(es) que la tocan.")]
    public Color32 columnColor = new Color32(0xc1, 0x12, 0xdc, 0xFF); // #c112dc

    [Tooltip("Color que marca una ESQUINA INTERNA (c\u00f3ncava, como el interior de una 'U'): una sola celda que necesita DOS piezas de pared (horizontal + vertical) formando el \u00e1ngulo, en vez de una sola.")]
    public Color32 innerCornerColor = new Color32(0x12, 0xdc, 0xc1, 0xFF); // #12dcc1

    [Range(1, 100)]
    public float colorMatchThreshold = 30f;

    private enum TileSizeReference { AutoMinimo, Wall, Stairs, Floor, Door, Manual }

    [Header("Espaciado en el mundo")]
    [Tooltip("Qu\u00e9 prefab define el tama\u00f1o real de UNA celda de la grilla. 'AutoMinimo' usa el m\u00e1s cuadrado de los prefabs de 1 celda (recomendado). OJO: como Stairs ahora es un bloque de varias celdas, NUNCA lo uses como referencia (ni manualmente ni deber\u00eda salir elegido en AutoMinimo).")]
    [SerializeField] private TileSizeReference tileSizeReference = TileSizeReference.AutoMinimo;
    public Vector2 manualWorldTileSize = new Vector2(10f, 10f);

    [Header("Rotaci\u00f3n de pared")]
    [Tooltip("Si tu prefab de pared, en rotaci\u00f3n 0, corre a lo largo del eje X (horizontal), dejalo en 0. Si corre a lo largo del eje Z (vertical), poné 90 ac\u00e1 para invertir la l\u00f3gica.")]
    public float wallBaseRotationY = 0f;

    [Header("Ajuste de pegado pared-piso")]
    [Tooltip("Si est\u00e1 activo, en vez de centrar la pared en el medio de su celda de grilla (que puede ser mucho m\u00e1s grande que la pared misma), la empuja hacia el borde que toca al piso vecino, dejando solo el grosor propio de la pared como separaci\u00f3n real. As\u00ed la pared queda 'pegada' al piso en vez de flotando en el centro de una celda grande.")]
    public bool snapWallsToFloorEdge = true;

    [Tooltip("Aplicar el mismo comportamiento de pegado a las puertas.")]
    public bool snapDoorsToFloorEdge = true;

    [Header("Techo")]
    public bool generateCeiling = true;

    [Tooltip("Si est\u00e1 activo, la altura del techo se calcula autom\u00e1ticamente usando la altura (eje Y) del Wall Prefab. Si tu pared no define bien esa altura, desactivalo y us\u00e1 el valor manual.")]
    public bool autoDetectCeilingHeight = true;

    [Tooltip("Altura manual (unidades del mundo) desde el piso hasta el techo. Solo se usa si Auto Detect Ceiling Height est\u00e1 desactivado.")]
    public float manualCeilingHeight = 3f;

    private enum FloorHeightSource { Stairs, Wall, Manual }

    [Header("M\u00faltiples pisos")]
    [Tooltip("De d\u00f3nde sale la distancia vertical entre un piso y el siguiente. 'Stairs' (recomendado): usa la altura real del bloque de escalera, para que el final de la escalera coincida exactamente con el tile de piso de abajo. 'Wall': usa la altura de la pared (puede no coincidir con la ca\u00edda real de la escalera). 'Manual': vos pon\u00e9s el n\u00famero.")]
    [SerializeField] private FloorHeightSource floorHeightSource = FloorHeightSource.Stairs;

    [Tooltip("Distancia vertical manual entre un piso y el siguiente. Solo se usa si Floor Height Source est\u00e1 en Manual.")]
    public float manualFloorHeight = 4f;

    [Tooltip("Si est\u00e1 activo (recomendado para un dungeon que desciende), cada imagen siguiente en la lista 'Floor Images' se genera UN NIVEL M\u00c1S ABAJO que la anterior. Si lo desactiv\u00e1s, se apilan hacia ARRIBA.")]
    public bool stackFloorsDownward = true;

    [Header("Columnas (marcadas con color en la imagen)")]
    [Tooltip("Si est\u00e1 activo, la columna se empuja hacia la(s) pared(es) vecina(s) para quedar pegada, en vez de flotar centrada en el medio de su celda.")]
    public bool snapColumnsToWalls = true;

    [Header("Puerta compuesta (m\u00faltiples GameObjects)")]
    [Tooltip("Si est\u00e1 activo, adem\u00e1s de elegir el prefab correcto (Horizontal/Vertical), busca DENTRO de \u00e9l un hijo que represente la HOJA de la puerta y lo rota para que mire al cuarto. DESACTIVADO por defecto: si tus dos prefabs (H y V) ya vienen con la hoja bien orientada de f\u00e1brica, esta rotaci\u00f3n extra sobra y puede romper la alineaci\u00f3n visual.")]
    public bool rotateDoorLeafToFaceRoom = false;

    [Tooltip("Substring (sin distinguir may\u00fasculas) para encontrar, DENTRO del prefab de la puerta, el GameObject que representa la HOJA de la puerta. Solo se usa si 'Rotate Door Leaf To Face Room' est\u00e1 activo.")]
    public string doorLeafNameContains = "Door";

    [Tooltip("Activ\u00e1 esto si, con 'Rotate Door Leaf To Face Room' activo, la hoja de la puerta queda mirando para el lado contrario del que deber\u00eda.")]
    public bool invertDoorFacing = false;

    [Header("Escaleras (bloque de varias celdas)")]
    [Tooltip("Ancho x alto esperado del bloque de Stairs, en celdas. Solo informativo: si el grupo detectado no coincide, se avisa por consola pero se coloca igual, centrado en su \u00e1rea real.")]
    public Vector2Int expectedStairsBlockSize = new Vector2Int(4, 3);

    [Tooltip("Si est\u00e1 activo, IGNORA el c\u00e1lculo autom\u00e1tico basado en el techo y usa 'Fixed Stairs Y' directamente para todas las escaleras. El valor es relativo a la base de CADA piso (Floor_N): con el generador en (0,0,0), en el piso 0 la escalera queda exactamente en ese Y; en los dem\u00e1s pisos se desplaza junto con la altura de cada piso.")]
    public bool useFixedStairsY = true;

    [Tooltip("Coordenada Y fija para la escalera (relativa a la base de cada piso). Solo se usa si 'Use Fixed Stairs Y' est\u00e1 activo.")]
    public float fixedStairsY = -9.530601f;

    [System.Serializable]
    public struct WeightedDecor
    {
        public GameObject prefab;
        [Tooltip("Peso relativo (no tiene que sumar 100, se normaliza solo). Un prefab con peso 2 sale el doble de seguido que uno con peso 1.")]
        public float weight;
    }

    [Header("Decoraci\u00f3n: Luces en pared (bot\u00f3n aparte)")]
    [Tooltip("Prefab de luz que se monta en la cara interior de una pared elegible.")]
    public GameObject wallLightPrefab;

    [Range(0f, 1f)]
    [Tooltip("Probabilidad (0 a 1) de que UNA pared elegible reciba una luz.")]
    public float wallLightChance = 0.12f;

    [Tooltip("Separaci\u00f3n entre la cara de la pared y el objeto montado (luz), para que no quede clipeando dentro de la geometr\u00eda.")]
    public float wallDecorForwardOffset = 0.15f;

    [Range(0f, 1f)]
    [Tooltip("A qu\u00e9 porcentaje de la altura de la pared (0 = piso, 1 = techo) se monta la luz. Ej: 0.6 = 60% de la altura de la pared.")]
    public float wallDecorHeightPercent = 0.6f;

    [Header("Decoraci\u00f3n: objetos sueltos en bordes de sala (bot\u00f3n aparte)")]
    [Tooltip("Lista de prefabs candidatos para decorar el PISO en celdas que tocan una pared (bordes y esquinas de sala), cada uno con su peso relativo.")]
    public List<WeightedDecor> floorDecorOptions = new List<WeightedDecor>();

    [Range(0f, 1f)]
    [Tooltip("Probabilidad (0 a 1) de que UNA celda de piso elegible (que toca pared) reciba ALGUNA decoraci\u00f3n. Si no sale, esa celda queda vac\u00eda. Si sale, se elige un prefab de 'Floor Decor Options' seg\u00fan su peso.")]
    public float floorDecorChance = 0.18f;

    [Tooltip("Si est\u00e1 activo, cada decoraci\u00f3n de piso recibe una rotaci\u00f3n Y aleatoria (0-360) para que no se vean todas iguales.")]
    public bool randomizeFloorDecorRotation = true;

    [Range(0f, 1f)]
    [Tooltip("Desplazamiento aleatorio dentro de la celda, como PORCENTAJE de la mitad del tama\u00f1o de celda. 0 = siempre en el centro exacto. 1 = puede llegar hasta el borde de la celda. Recomendado alto (0.7-0.9) para que no se vean todas 'en el medio del piso'.")]
    public float floorDecorJitterPercent = 0.8f;

    [Header("Aleatoriedad de la decoraci\u00f3n")]
    [Tooltip("Si est\u00e1 activo, usa 'Random Seed' para que la decoraci\u00f3n generada sea siempre la MISMA cada vez que apretes el bot\u00f3n (\u00fatil para iterar sin que cambie todo de nuevo). Si lo desactiv\u00e1s, cada corrida es distinta.")]
    public bool useFixedRandomSeed = true;
    public int randomSeed = 12345;

    [Header("Organizaci\u00f3n")]
    public bool parentToThis = true;
    public bool clearBeforeGenerating = true;

    // Apunta al Transform del piso (Floor_N) que se est\u00e1 generando en cada momento.
    // Todas las posiciones y el parenteo de los objetos generados usan ESTO, no 'transform' directo,
    // para que cada piso quede apilado en su propia altura.
    private Transform activeParent;

    private struct PrefabPlacementInfo
    {
        public Vector2 size;
        public Vector2 centerOffsetXZ; // offset entre el pivot del prefab y el centro real de su bounding box (en XZ, rotaci\u00f3n 0)
        public float height;           // tama\u00f1o del bounding box en Y
        public float centerOffsetY;    // offset entre el pivot y el centro real de su bounding box en Y
    }

#if UNITY_EDITOR
    [ContextMenu("Generate Map From Image")]
    public void GenerateMapFromImage()
    {
        if (floorImages == null || floorImages.Count == 0)
        {
            Debug.LogError("[MapFromImageGenerator] Asign\u00e1 al menos una imagen en 'Floor Images' antes de generar.");
            return;
        }

        if (wallPrefab == null || stairsPrefab == null || floorPrefab == null || doorPrefabHorizontal == null || doorPrefabVertical == null)
        {
            Debug.LogError("[MapFromImageGenerator] Falta asignar alg\u00fan prefab obligatorio (Wall, Stairs, Floor, Door Horizontal o Door Vertical).");
            return;
        }

        if (clearBeforeGenerating)
        {
            ClearGeneratedMap();
        }

        // Medimos los prefabs UNA sola vez: son los mismos para todos los pisos.
        var wallInfo = GetPrefabInfo(wallPrefab);
        var stairsInfo = GetPrefabInfo(stairsPrefab);
        var floorInfo = GetPrefabInfo(floorPrefab);
        var doorInfoH = GetPrefabInfo(doorPrefabHorizontal);
        var doorInfoV = GetPrefabInfo(doorPrefabVertical);
        var columnInfo = columnPrefab != null ? GetPrefabInfo(columnPrefab) : default;
        var ceilingInfo = (generateCeiling && ceilingPrefab != null) ? GetPrefabInfo(ceilingPrefab) : default;

        Vector2 worldTileSize = ResolveWorldTileSize(wallInfo, stairsInfo, floorInfo, doorInfoH);
        if (worldTileSize.x <= 0f || worldTileSize.y <= 0f)
        {
            Debug.LogError("[MapFromImageGenerator] No se pudo detectar un tama\u00f1o de celda v\u00e1lido.");
            return;
        }

        float floorHeight;
        switch (floorHeightSource)
        {
            case FloorHeightSource.Stairs: floorHeight = stairsInfo.height; break;
            case FloorHeightSource.Wall: floorHeight = wallInfo.height; break;
            default: floorHeight = manualFloorHeight; break;
        }

        if (floorHeight <= 0f)
        {
            Debug.LogWarning($"[MapFromImageGenerator] La altura de piso detectada v\u00eda '{floorHeightSource}' fue 0. Se us\u00f3 Manual Floor Height ({manualFloorHeight}) como respaldo.");
            floorHeight = manualFloorHeight;
        }

        Debug.Log($"[MapFromImageGenerator] Tama\u00f1os detectados -> Wall: {wallInfo.size}, Stairs: {stairsInfo.size} (bloque, ignorado para worldTileSize; altura Y: {stairsInfo.height}), Floor: {floorInfo.size}, DoorH: {doorInfoH.size}, DoorV: {doorInfoV.size}. worldTileSize usado: {worldTileSize}. Floor height ({floorHeightSource}): {floorHeight}. Pisos a generar: {floorImages.Count}.");

        int totalPlaced = 0;

        for (int floorIndex = 0; floorIndex < floorImages.Count; floorIndex++)
        {
            Texture2D floorImage = floorImages[floorIndex];
            if (floorImage == null)
            {
                Debug.LogWarning($"[MapFromImageGenerator] Piso {floorIndex}: la imagen est\u00e1 vac\u00eda (null). Se salte\u00f3.");
                continue;
            }
            if (!floorImage.isReadable)
            {
                Debug.LogError($"[MapFromImageGenerator] Piso {floorIndex} ('{floorImage.name}'): la textura no tiene 'Read/Write Enabled' activado. Se salte\u00f3.");
                continue;
            }

            mapImage = floorImage;

            int columns = Mathf.RoundToInt(mapImage.width / pixelsPerTile);
            int rows = Mathf.RoundToInt(mapImage.height / pixelsPerTile);

            if (columns <= 0 || rows <= 0)
            {
                Debug.LogError($"[MapFromImageGenerator] Piso {floorIndex} ('{floorImage.name}'): el c\u00e1lculo de columnas/filas dio 0. Revis\u00e1 Pixels Per Tile. Se salte\u00f3.");
                continue;
            }

            // Creamos (o reusamos) el contenedor de este piso, a su altura correspondiente.
            string floorName = $"Floor_{floorIndex}";
            Transform floorParent = transform.Find(floorName);
            if (floorParent == null)
            {
                GameObject floorGO = new GameObject(floorName);
                floorGO.transform.SetParent(transform, false);
                Undo.RegisterCreatedObjectUndo(floorGO, "Generate Map From Image");
                floorParent = floorGO.transform;
            }

            float floorY = stackFloorsDownward ? -(floorIndex * floorHeight) : (floorIndex * floorHeight);
            floorParent.localPosition = new Vector3(0f, floorY, 0f);
            activeParent = floorParent;

            int placedThisFloor = GenerateFloor(columns, rows, worldTileSize, wallInfo, stairsInfo, floorInfo, doorInfoH, doorInfoV, columnInfo, ceilingInfo);
            totalPlaced += placedThisFloor;

            Debug.Log($"[MapFromImageGenerator] Piso {floorIndex} ('{floorImage.name}'): grilla {columns}x{rows}, {placedThisFloor} objetos colocados, a Y={floorY}.");
        }

        Debug.Log($"[MapFromImageGenerator] Listo. Total de objetos colocados en {floorImages.Count} piso(s): {totalPlaced}.");
    }

    /// <summary>
    /// Genera UN piso completo (usa el campo 'mapImage' y 'activeParent' ya seteados por el llamador).
    /// Devuelve la cantidad de objetos colocados.
    /// </summary>
    private int GenerateFloor(int columns, int rows, Vector2 worldTileSize, PrefabPlacementInfo wallInfo, PrefabPlacementInfo stairsInfo, PrefabPlacementInfo floorInfo, PrefabPlacementInfo doorInfoH, PrefabPlacementInfo doorInfoV, PrefabPlacementInfo columnInfo, PrefabPlacementInfo ceilingInfo)
    {
        // Paso 1: muestrear toda la grilla UNA vez y guardar el tipo de cada celda.
        TileType[,] grid = new TileType[columns, rows];
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                grid[col, row] = SampleTileType(col, row, columns, rows);
            }
        }

        // Paso 1.5a: agrupar las celdas de Puerta contiguas (una sola orientaci\u00f3n por grupo).
        var doorGroups = ComputeDoorGroups(grid, columns, rows);

        // Altura de techo de ESTE piso, calculada ac\u00e1 arriba porque tambi\u00e9n la necesitamos
        // para alinear el punto m\u00e1s alto de la escalera con el techo (no solo para el Ceiling Prefab).
        float ceilingHeight = autoDetectCeilingHeight ? wallInfo.height : manualCeilingHeight;
        if (ceilingHeight <= 0f)
        {
            Debug.LogWarning("[MultiLevelMapGenerator] La altura de techo detectada fue 0. Se us\u00f3 Manual Ceiling Height como respaldo.");
            ceilingHeight = manualCeilingHeight;
        }

        // Paso 1.5b: agrupar las celdas de Stairs contiguas (un solo prefab por grupo, sin rotar).
        var stairsGroups = FindConnectedGroups(grid, TileType.Stairs, columns, rows);
        var stairsCellSet = new HashSet<Vector2Int>();

        int placedCount = 0;

        foreach (var group in stairsGroups)
        {
            int minCol = group.Min(c => c.x);
            int maxCol = group.Max(c => c.x);
            int minRow = group.Min(c => c.y);
            int maxRow = group.Max(c => c.y);
            int width = maxCol - minCol + 1;
            int height = maxRow - minRow + 1;

            if (!((width == expectedStairsBlockSize.x && height == expectedStairsBlockSize.y) ||
                  (width == expectedStairsBlockSize.y && height == expectedStairsBlockSize.x)))
            {
                Debug.LogWarning($"[MapFromImageGenerator] Un grupo de Stairs mide {width}x{height} celdas (se esperaba {expectedStairsBlockSize.x}x{expectedStairsBlockSize.y} o al rev\u00e9s). Se coloca igual, centrado en su \u00e1rea real.");
            }

            Vector3 minCellCenter = GetCellCenterWorld(minCol, minRow, rows, worldTileSize);
            Vector3 maxCellCenter = GetCellCenterWorld(maxCol, maxRow, rows, worldTileSize);
            Vector3 groupCenterWorld = (minCellCenter + maxCellCenter) * 0.5f;

            // Rotaci\u00f3n FIJA: se usa la que ya trae el prefab, sin aplicar nada extra por c\u00f3digo.
            Vector3 offsetXZ = stairsPrefab.transform.rotation * new Vector3(stairsInfo.centerOffsetXZ.x, 0f, stairsInfo.centerOffsetXZ.y);
            Vector3 finalPos = groupCenterWorld - offsetXZ;

            // "Pegado" al piso, igual que las paredes: en vez de dejar la escalera centrada en TODO
            // el bloque que dibujaste (que puede ser m\u00e1s grande que el prefab real), la empujamos
            // hacia el borde por donde entra el piso, dejando solo el tama\u00f1o real del prefab como
            // margen. Primero detectamos de qu\u00e9 lado del bloque hay Piso (mayor\u00eda de vecinos en el borde).
            Vector2Int stairsEntryDir = DetectGroupFloorDirection(grid, group, minCol, maxCol, minRow, maxRow, columns, rows);

            if (stairsEntryDir != Vector2Int.zero)
            {
                bool isRowAxis = stairsEntryDir.y != 0;

                // Celda de referencia sobre el borde del grupo m\u00e1s cercana a la direcci\u00f3n de entrada,
                // para calcular la direcci\u00f3n mundial exacta (con el mismo criterio que ya usamos en paredes).
                int refCol = isRowAxis ? (minCol + maxCol) / 2 : (stairsEntryDir.x < 0 ? minCol : maxCol);
                int refRow = isRowAxis ? (stairsEntryDir.y < 0 ? minRow : maxRow) : (minRow + maxRow) / 2;
                Vector3 refCellCenter = GetCellCenterWorld(refCol, refRow, rows, worldTileSize);
                Vector3 neighborCellCenter = GetCellCenterWorld(refCol + stairsEntryDir.x, refRow + stairsEntryDir.y, rows, worldTileSize);
                Vector3 directionToFloor = (neighborCellCenter - refCellCenter).normalized;

                float drawnSizeAlongAxis = isRowAxis ? (height * worldTileSize.y) : (width * worldTileSize.x);
                float prefabSizeAlongAxis = isRowAxis ? stairsInfo.size.y : stairsInfo.size.x;
                float pushDistance = Mathf.Max(0f, (drawnSizeAlongAxis - prefabSizeAlongAxis) * 0.5f);

                finalPos += directionToFloor * pushDistance;
            }

            if (useFixedStairsY)
            {
                // Y fijo: se ignora el techo por completo. groupCenterWorld.y ya es la base de ESTE
                // piso (0 para el piso 0 si el generador est\u00e1 en (0,0,0)), as\u00ed que sumarle
                // fixedStairsY da exactamente ese n\u00famero en el piso 0, y se desplaza igual en los dem\u00e1s.
                finalPos.y = groupCenterWorld.y + fixedStairsY;
            }
            else
            {
                // Ajuste vertical autom\u00e1tico: el punto M\u00c1S ALTO de la escalera (no su pivot, no su
                // centro) tiene que coincidir con el punto M\u00c1S ALTO real del techo de ESTE piso.
                // 'ceilingHeight' es la altura a la que se ubica el CENTRO del Ceiling Prefab (ver
                // Paso 4 m\u00e1s abajo), no su punto m\u00e1s alto \u2014 el techo tiene su propio grosor, as\u00ed
                // que hay que sumarle la mitad de SU propia altura para llegar a su superficie superior real.
                float ceilingOwnHalfHeight = (generateCeiling && ceilingPrefab != null) ? ceilingInfo.height * 0.5f : 0f;
                float topOffsetFromPivot = stairsInfo.centerOffsetY + stairsInfo.height * 0.5f;
                float desiredTopY = groupCenterWorld.y + ceilingHeight + ceilingOwnHalfHeight;
                finalPos.y = desiredTopY - topOffsetFromPivot;
            }

            GameObject stairsInstance = (GameObject)PrefabUtility.InstantiatePrefab(stairsPrefab);
            stairsInstance.transform.position = finalPos;
            stairsInstance.transform.rotation = stairsPrefab.transform.rotation;
            stairsInstance.name = $"{stairsPrefab.name}_{minCol}_{minRow}";

            if (parentToThis)
            {
                stairsInstance.transform.SetParent(activeParent, true);
            }

            Undo.RegisterCreatedObjectUndo(stairsInstance, "Generate Map From Image");
            placedCount++;

            foreach (var cell in group)
            {
                stairsCellSet.Add(cell);
            }
        }

        // Paso 3: instanciar el resto de la grilla (todo salvo Stairs, ya colocadas arriba como bloque).
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                TileType type = grid[col, row];
                if (type == TileType.None) continue;
                if (type == TileType.Stairs) continue; // ya se coloc\u00f3 como grupo m\u00e1s arriba
                if (type == TileType.Column && columnPrefab == null)
                {
                    Debug.LogWarning($"[MapFromImageGenerator] Hay una celda rosa (Column) en ({col},{row}) pero no asignaste Column Prefab. Se salte\u00f3.");
                    continue;
                }

                // Esquina interna (c\u00f3ncava): en vez de UNA pieza de pared, van DOS: una horizontal
                // (pegada al piso de arriba o abajo) y una vertical (pegada al piso de izq o der).
                if (type == TileType.InnerCorner)
                {
                    bool IsFloorLocal(int c, int r) => c >= 0 && c < columns && r >= 0 && r < rows && grid[c, r] == TileType.Floor;

                    Vector2Int hFloorDir = Vector2Int.zero;
                    if (IsFloorLocal(col, row - 1)) hFloorDir = new Vector2Int(0, -1);
                    else if (IsFloorLocal(col, row + 1)) hFloorDir = new Vector2Int(0, 1);

                    Vector2Int vFloorDir = Vector2Int.zero;
                    if (IsFloorLocal(col - 1, row)) vFloorDir = new Vector2Int(-1, 0);
                    else if (IsFloorLocal(col + 1, row)) vFloorDir = new Vector2Int(1, 0);

                    if (hFloorDir != Vector2Int.zero)
                    {
                        PlaceWallSegment(wallPrefab, wallInfo, col, row, rows, worldTileSize, wallBaseRotationY + 0f, hFloorDir, snapWallsToFloorEdge, "_H", ref placedCount);
                    }
                    if (vFloorDir != Vector2Int.zero)
                    {
                        PlaceWallSegment(wallPrefab, wallInfo, col, row, rows, worldTileSize, wallBaseRotationY + 90f, vFloorDir, snapWallsToFloorEdge, "_V", ref placedCount);
                    }

                    if (hFloorDir == Vector2Int.zero && vFloorDir == Vector2Int.zero)
                    {
                        Debug.LogWarning($"[MapFromImageGenerator] La esquina interna en ({col},{row}) no encontr\u00f3 Piso ni horizontal ni vertical. Revis\u00e1 esa zona de la imagen.");
                    }

                    if (columnPrefab != null)
                    {
                        Vector3 innerCornerCellCenter = GetCellCenterWorld(col, row, rows, worldTileSize);
                        Vector3 push = Vector3.zero;

                        if (hFloorDir != Vector2Int.zero)
                        {
                            Vector3 hNeighborCenter = GetCellCenterWorld(col + hFloorDir.x, row + hFloorDir.y, rows, worldTileSize);
                            Vector3 hDirWorld = (hNeighborCenter - innerCornerCellCenter).normalized;
                            push += hDirWorld * (worldTileSize.y * 0.5f);
                        }

                        if (vFloorDir != Vector2Int.zero)
                        {
                            Vector3 vNeighborCenter = GetCellCenterWorld(col + vFloorDir.x, row + vFloorDir.y, rows, worldTileSize);
                            Vector3 vDirWorld = (vNeighborCenter - innerCornerCellCenter).normalized;
                            push += vDirWorld * (worldTileSize.x * 0.5f);
                        }

                        Vector3 columnOffset = new Vector3(columnInfo.centerOffsetXZ.x, 0f, columnInfo.centerOffsetXZ.y);
                        Vector3 columnPosition = innerCornerCellCenter + push - columnOffset;

                        GameObject columnInstance = (GameObject)PrefabUtility.InstantiatePrefab(columnPrefab);
                        columnInstance.transform.position = columnPosition;
                        columnInstance.transform.rotation = columnPrefab.transform.rotation;
                        columnInstance.name = $"{columnPrefab.name}_{col}_{row}_InnerCorner";

                        if (parentToThis)
                        {
                            columnInstance.transform.SetParent(activeParent, true);
                        }

                        Undo.RegisterCreatedObjectUndo(columnInstance, "Generate Map From Image");
                        placedCount++;
                    }

                    continue;
                }

                // Para Wall necesitamos saber si el tramo es horizontal o vertical (para rotarse).
                // Para Door, la orientaci\u00f3n y la direcci\u00f3n hacia el piso ya vienen calculadas por GRUPO.
                float segmentRotation = 0f;
                Vector2Int floorNeighborOffset = Vector2Int.zero;
                if (type == TileType.Wall)
                {
                    var segment = DetectSegmentRotation(grid, col, row, columns, rows);
                    segmentRotation = segment.rotation;
                    floorNeighborOffset = segment.floorNeighborOffset;
                }
                else if (type == TileType.Door && doorGroups.TryGetValue(new Vector2Int(col, row), out var doorGroup))
                {
                    segmentRotation = doorGroup.rotation;
                    floorNeighborOffset = doorGroup.floorDirection;
                }

                GameObject prefab;
                PrefabPlacementInfo info;
                bool useAutoRotation;

                switch (type)
                {
                    case TileType.Wall:
                        prefab = wallPrefab; info = wallInfo; useAutoRotation = true; break;
                    case TileType.Door:
                        if (segmentRotation == 0f) { prefab = doorPrefabHorizontal; info = doorInfoH; }
                        else { prefab = doorPrefabVertical; info = doorInfoV; }
                        useAutoRotation = false;
                        break;
                    case TileType.Column:
                        prefab = columnPrefab; info = columnInfo; useAutoRotation = false; break;
                    default:
                        prefab = floorPrefab; info = floorInfo; useAutoRotation = false; break;
                }

                float rotationY = prefab.transform.eulerAngles.y;
                if (useAutoRotation)
                {
                    rotationY = wallBaseRotationY + segmentRotation;
                }

                Quaternion rotation = Quaternion.Euler(0f, rotationY, 0f);
                Vector3 cellCenterWorld = GetCellCenterWorld(col, row, rows, worldTileSize);

                Vector3 rotatedOffset = rotation * new Vector3(info.centerOffsetXZ.x, 0f, info.centerOffsetXZ.y);
                Vector3 finalPosition = cellCenterWorld - rotatedOffset;

                Vector3 directionToFloor = Vector3.zero;
                if (floorNeighborOffset != Vector2Int.zero)
                {
                    Vector3 neighborCellCenter = GetCellCenterWorld(col + floorNeighborOffset.x, row + floorNeighborOffset.y, rows, worldTileSize);
                    directionToFloor = (neighborCellCenter - cellCenterWorld).normalized;
                }

                bool shouldSnap = (type == TileType.Wall && snapWallsToFloorEdge) ||
                                   (type == TileType.Door && snapDoorsToFloorEdge);

                if (shouldSnap && floorNeighborOffset != Vector2Int.zero)
                {
                    float thickness = Mathf.Min(info.size.x, info.size.y);
                    float cellDepthInPushDirection = (floorNeighborOffset.y != 0) ? worldTileSize.y : worldTileSize.x;
                    float pushDistance = Mathf.Max(0f, (cellDepthInPushDirection - thickness) * 0.5f);
                    finalPosition += directionToFloor * pushDistance;
                }

                if (type == TileType.Column && snapColumnsToWalls)
                {
                    finalPosition += ComputeWallNeighborPush(grid, col, row, columns, rows, worldTileSize, cellCenterWorld);
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.position = finalPosition;
                instance.transform.rotation = rotation;
                instance.name = $"{prefab.name}_{col}_{row}";

                if (parentToThis)
                {
                    instance.transform.SetParent(activeParent, true);
                }

                Undo.RegisterCreatedObjectUndo(instance, "Generate Map From Image");
                placedCount++;

                if (type == TileType.Door && rotateDoorLeafToFaceRoom && directionToFloor != Vector3.zero)
                {
                    Transform doorLeaf = FindChildContaining(instance.transform, doorLeafNameContains);
                    if (doorLeaf != null)
                    {
                        Vector3 faceDir = invertDoorFacing ? -directionToFloor : directionToFloor;
                        doorLeaf.rotation = Quaternion.LookRotation(faceDir, Vector3.up);
                    }
                    else
                    {
                        Debug.LogWarning($"[MapFromImageGenerator] No encontr\u00e9 ning\u00fan hijo que contenga '{doorLeafNameContains}' dentro de {instance.name} para orientar la hoja de la puerta.");
                    }
                }
            }
        }

        // Paso 4: techo de este piso. (ceilingHeight ya se calcul\u00f3 arriba, antes de las escaleras)
        if (generateCeiling && ceilingPrefab != null)
        {
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    if (grid[col, row] != TileType.Floor) continue;

                    Vector3 floorCellCenter = GetCellCenterWorld(col, row, rows, worldTileSize);
                    Vector3 ceilingOffset = new Vector3(ceilingInfo.centerOffsetXZ.x, ceilingInfo.centerOffsetY, ceilingInfo.centerOffsetXZ.y);
                    Vector3 ceilingTargetCenter = floorCellCenter + new Vector3(0f, ceilingHeight, 0f);
                    Vector3 ceilingPosition = ceilingTargetCenter - ceilingOffset;

                    GameObject ceilingInstance = (GameObject)PrefabUtility.InstantiatePrefab(ceilingPrefab);
                    ceilingInstance.transform.position = ceilingPosition;
                    ceilingInstance.transform.rotation = ceilingPrefab.transform.rotation;
                    ceilingInstance.name = $"{ceilingPrefab.name}_{col}_{row}";

                    if (parentToThis)
                    {
                        ceilingInstance.transform.SetParent(activeParent, true);
                    }

                    Undo.RegisterCreatedObjectUndo(ceilingInstance, "Generate Map From Image");
                    placedCount++;
                }
            }
        }

        return placedCount;
    }

    private Vector2 ResolveWorldTileSize(PrefabPlacementInfo wallInfo, PrefabPlacementInfo stairsInfo, PrefabPlacementInfo floorInfo, PrefabPlacementInfo doorInfoH)
    {
        switch (tileSizeReference)
        {
            case TileSizeReference.Wall: return wallInfo.size;
            case TileSizeReference.Stairs: return stairsInfo.size; // OJO: ya no es 1 celda, evitar usar esta opci\u00f3n
            case TileSizeReference.Floor: return floorInfo.size;
            case TileSizeReference.Door: return doorInfoH.size;
            case TileSizeReference.Manual: return manualWorldTileSize;
            default:
                // AutoMinimo: elige, entre Wall/Floor/DoorH (Stairs queda afuera a prop\u00f3sito, ya que
                // ahora es un bloque de varias celdas), el prefab m\u00e1s "cuadrado" (aspect ratio m\u00e1s cercano a 1).
                Vector2 best = wallInfo.size;
                float bestAspect = float.MaxValue;
                foreach (var candidate in new[] { wallInfo, floorInfo, doorInfoH })
                {
                    if (candidate.size.x <= 0f || candidate.size.y <= 0f) continue;
                    float aspect = Mathf.Max(candidate.size.x, candidate.size.y) / Mathf.Min(candidate.size.x, candidate.size.y);
                    if (aspect < bestAspect)
                    {
                        bestAspect = aspect;
                        best = candidate.size;
                    }
                }
                return best;
        }
    }

    [ContextMenu("Clear Generated Map")]
    public void ClearGeneratedMap()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            Undo.DestroyObjectImmediate(child.gameObject);
        }
    }

    private TileType SampleTileType(int col, int row, int columns, int rows)
    {
        int pixelX = Mathf.Clamp(Mathf.RoundToInt((col + 0.5f) * pixelsPerTile), 0, mapImage.width - 1);
        int pixelYFromBottom = mapImage.height - 1 - Mathf.Clamp(Mathf.RoundToInt((row + 0.5f) * pixelsPerTile), 0, mapImage.height - 1);
        Color32 sample = mapImage.GetPixel(pixelX, pixelYFromBottom);

        (Color32 color, TileType type)[] candidates =
        {
            (wallColor, TileType.Wall),
            (stairsColor, TileType.Stairs),
            (floorColor, TileType.Floor),
            (doorColor, TileType.Door),
            (columnColor, TileType.Column),
            (innerCornerColor, TileType.InnerCorner),
        };

        float bestDistance = float.MaxValue;
        TileType bestType = TileType.None;

        foreach (var (color, type) in candidates)
        {
            float dist = ColorDistance(sample, color);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                bestType = type;
            }
        }

        return bestDistance <= colorMatchThreshold ? bestType : TileType.None;
    }

    private struct DoorGroupInfo
    {
        public float rotation;             // 0 = horizontal, 90 = vertical, decidido para TODO el grupo
        public Vector2Int floorDirection;  // hacia d\u00f3nde est\u00e1 el piso, tambi\u00e9n decidido para TODO el grupo
    }

    /// <summary>
    /// Agrupa las celdas de Puerta CONTIGUAS (conectadas por izq/der/arriba/abajo) en bloques,
    /// y decide UNA sola orientaci\u00f3n para cada grupo entero, en vez de celda por celda.
    /// </summary>
    private Dictionary<Vector2Int, DoorGroupInfo> ComputeDoorGroups(TileType[,] grid, int columns, int rows)
    {
        var result = new Dictionary<Vector2Int, DoorGroupInfo>();
        bool IsFloor(int c, int r) => c >= 0 && c < columns && r >= 0 && r < rows && grid[c, r] == TileType.Floor;

        var groups = FindConnectedGroups(grid, TileType.Door, columns, rows);

        foreach (var groupCells in groups)
        {
            int minCol = groupCells.Min(c => c.x);
            int maxCol = groupCells.Max(c => c.x);
            int minRow = groupCells.Min(c => c.y);
            int maxRow = groupCells.Max(c => c.y);

            int width = maxCol - minCol + 1;
            int height = maxRow - minRow + 1;
            float groupRotation = (width >= height) ? 0f : 90f;

            Vector2Int groupFloorDirection = Vector2Int.zero;
            if (groupRotation == 0f)
            {
                int above = 0, below = 0;
                foreach (var cell in groupCells)
                {
                    if (IsFloor(cell.x, cell.y - 1)) above++;
                    if (IsFloor(cell.x, cell.y + 1)) below++;
                }
                if (above > 0 || below > 0)
                {
                    groupFloorDirection = (above >= below) ? new Vector2Int(0, -1) : new Vector2Int(0, 1);
                }
            }
            else
            {
                int left = 0, right = 0;
                foreach (var cell in groupCells)
                {
                    if (IsFloor(cell.x - 1, cell.y)) left++;
                    if (IsFloor(cell.x + 1, cell.y)) right++;
                }
                if (left > 0 || right > 0)
                {
                    groupFloorDirection = (left >= right) ? new Vector2Int(-1, 0) : new Vector2Int(1, 0);
                }
            }

            var info = new DoorGroupInfo { rotation = groupRotation, floorDirection = groupFloorDirection };
            foreach (var cell in groupCells)
            {
                result[cell] = info;
            }
        }

        return result;
    }

    /// <summary>
    /// Flood-fill gen\u00e9rico: agrupa todas las celdas CONTIGUAS (conectadas por izq/der/arriba/abajo)
    /// que sean del tipo indicado. Se usa tanto para Puertas como para Escaleras.
    /// </summary>
    /// <summary>
    /// Dado un grupo de celdas (ej. el bloque de Stairs) y su bounding box, determina de qu\u00e9 lado
    /// (arriba/abajo/izq/der) hay M\u00c1S vecinos de Piso pegados al borde del grupo. Mismo criterio de
    /// "mayor\u00eda" que ya usa ComputeDoorGroups, pero gen\u00e9rico para cualquier bounding box.
    /// </summary>
    private Vector2Int DetectGroupFloorDirection(TileType[,] grid, List<Vector2Int> groupCells, int minCol, int maxCol, int minRow, int maxRow, int columns, int rows)
    {
        bool IsFloor(int c, int r) => c >= 0 && c < columns && r >= 0 && r < rows && grid[c, r] == TileType.Floor;

        int above = 0, below = 0, left = 0, right = 0;

        foreach (var cell in groupCells)
        {
            if (cell.y == minRow && IsFloor(cell.x, cell.y - 1)) above++;
            if (cell.y == maxRow && IsFloor(cell.x, cell.y + 1)) below++;
            if (cell.x == minCol && IsFloor(cell.x - 1, cell.y)) left++;
            if (cell.x == maxCol && IsFloor(cell.x + 1, cell.y)) right++;
        }

        int best = Mathf.Max(above, below, left, right);
        if (best == 0) return Vector2Int.zero;

        if (best == above) return new Vector2Int(0, -1);
        if (best == below) return new Vector2Int(0, 1);
        if (best == left) return new Vector2Int(-1, 0);
        return new Vector2Int(1, 0);
    }

    private List<List<Vector2Int>> FindConnectedGroups(TileType[,] grid, TileType targetType, int columns, int rows)
    {
        var groups = new List<List<Vector2Int>>();
        var visited = new bool[columns, rows];
        Vector2Int[] fourDirs = { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1) };

        for (int col = 0; col < columns; col++)
        {
            for (int row = 0; row < rows; row++)
            {
                if (grid[col, row] != targetType || visited[col, row]) continue;

                var cells = new List<Vector2Int>();
                var stack = new Stack<Vector2Int>();
                stack.Push(new Vector2Int(col, row));
                visited[col, row] = true;

                while (stack.Count > 0)
                {
                    Vector2Int cell = stack.Pop();
                    cells.Add(cell);

                    foreach (var d in fourDirs)
                    {
                        int nc = cell.x + d.x;
                        int nr = cell.y + d.y;
                        if (nc < 0 || nc >= columns || nr < 0 || nr >= rows) continue;
                        if (visited[nc, nr]) continue;
                        if (grid[nc, nr] != targetType) continue;
                        visited[nc, nr] = true;
                        stack.Push(new Vector2Int(nc, nr));
                    }
                }

                groups.Add(cells);
            }
        }

        return groups;
    }

    /// <summary>
    /// Analiza los vecinos de una celda de pared para determinar:
    /// - rotation: 0 si el tramo es horizontal, 90 si es vertical.
    /// - floorNeighborOffset: la direcci\u00f3n (en coordenadas de grilla) hacia el vecino que es Piso.
    /// </summary>
    private (float rotation, Vector2Int floorNeighborOffset) DetectSegmentRotation(TileType[,] grid, int col, int row, int columns, int rows)
    {
        bool InRange(int c, int r) => c >= 0 && c < columns && r >= 0 && r < rows;
        bool IsBoundary(int c, int r) => InRange(c, r) && (grid[c, r] == TileType.Wall || grid[c, r] == TileType.Door);
        bool IsFloor(int c, int r) => InRange(c, r) && grid[c, r] == TileType.Floor;

        bool leftRight = IsBoundary(col - 1, row) || IsBoundary(col + 1, row);
        bool upDown = IsBoundary(col, row - 1) || IsBoundary(col, row + 1);

        float rotation;
        if (leftRight && !upDown) rotation = 0f;
        else if (upDown && !leftRight) rotation = 90f;
        else rotation = 0f;

        Vector2Int floorNeighborOffset = Vector2Int.zero;

        if (rotation == 0f)
        {
            if (IsFloor(col, row - 1)) floorNeighborOffset = new Vector2Int(0, -1);
            else if (IsFloor(col, row + 1)) floorNeighborOffset = new Vector2Int(0, 1);
        }
        else
        {
            if (IsFloor(col - 1, row)) floorNeighborOffset = new Vector2Int(-1, 0);
            else if (IsFloor(col + 1, row)) floorNeighborOffset = new Vector2Int(1, 0);
        }

        if (floorNeighborOffset == Vector2Int.zero)
        {
            if (IsFloor(col, row - 1)) floorNeighborOffset = new Vector2Int(0, -1);
            else if (IsFloor(col, row + 1)) floorNeighborOffset = new Vector2Int(0, 1);
            else if (IsFloor(col - 1, row)) floorNeighborOffset = new Vector2Int(-1, 0);
            else if (IsFloor(col + 1, row)) floorNeighborOffset = new Vector2Int(1, 0);
        }

        return (rotation, floorNeighborOffset);
    }

    /// <summary>
    /// Calcula el centro en el mundo de una celda de grilla dada, relativo al PISO activo
    /// (activeParent), para que cada piso quede apilado correctamente.
    /// </summary>
    private Vector3 GetCellCenterWorld(int col, int row, int rows, Vector2 worldTileSize)
    {
        float x = (col + 0.5f) * worldTileSize.x;
        float z = (rows - 1 - row + 0.5f) * worldTileSize.y;
        Vector3 basePosition = activeParent != null ? activeParent.position : transform.position;
        return basePosition + new Vector3(x, 0f, z);
    }

    /// <summary>
    /// Instancia UNA pieza de pared recta (Wall Prefab) en la celda dada, con la rotaci\u00f3n y el
    /// empuje-hacia-el-piso indicados.
    /// </summary>
    private void PlaceWallSegment(GameObject segmentPrefab, PrefabPlacementInfo segmentInfo, int col, int row, int rows, Vector2 worldTileSize, float rotationY, Vector2Int floorDir, bool snap, string nameSuffix, ref int placedCount)
    {
        Quaternion rotation = Quaternion.Euler(0f, rotationY, 0f);
        Vector3 cellCenterWorld = GetCellCenterWorld(col, row, rows, worldTileSize);
        Vector3 rotatedOffset = rotation * new Vector3(segmentInfo.centerOffsetXZ.x, 0f, segmentInfo.centerOffsetXZ.y);
        Vector3 finalPosition = cellCenterWorld - rotatedOffset;

        if (snap && floorDir != Vector2Int.zero)
        {
            Vector3 neighborCenter = GetCellCenterWorld(col + floorDir.x, row + floorDir.y, rows, worldTileSize);
            Vector3 directionToFloor = (neighborCenter - cellCenterWorld).normalized;
            float thickness = Mathf.Min(segmentInfo.size.x, segmentInfo.size.y);
            float cellDepthInPushDirection = (floorDir.y != 0) ? worldTileSize.y : worldTileSize.x;
            float pushDistance = Mathf.Max(0f, (cellDepthInPushDirection - thickness) * 0.5f);
            finalPosition += directionToFloor * pushDistance;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(segmentPrefab);
        instance.transform.position = finalPosition;
        instance.transform.rotation = rotation;
        instance.name = $"{segmentPrefab.name}_{col}_{row}{nameSuffix}";

        if (parentToThis)
        {
            instance.transform.SetParent(activeParent, true);
        }

        Undo.RegisterCreatedObjectUndo(instance, "Generate Map From Image");
        placedCount++;
    }

    /// <summary>
    /// Calcula cu\u00e1nto empujar un objeto (columna) desde el centro de su celda hacia CADA vecino
    /// que sea Pared o Puerta (izq/der/arriba/abajo), hasta el borde completo de esa celda.
    /// </summary>
    private Vector3 ComputeWallNeighborPush(TileType[,] grid, int col, int row, int columns, int rows, Vector2 worldTileSize, Vector3 cellCenterWorld)
    {
        Vector3 push = Vector3.zero;
        Vector2Int[] neighborDirs = { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1) };

        foreach (var dir in neighborDirs)
        {
            int nc = col + dir.x;
            int nr = row + dir.y;
            if (nc < 0 || nc >= columns || nr < 0 || nr >= rows) continue;
            TileType neighborType = grid[nc, nr];
            if (neighborType != TileType.Wall && neighborType != TileType.Door) continue;

            Vector3 neighborCenter = GetCellCenterWorld(nc, nr, rows, worldTileSize);
            Vector3 dirWorld = (neighborCenter - cellCenterWorld).normalized;

            bool isRowAxis = dir.y != 0;
            float axisCellSize = isRowAxis ? worldTileSize.y : worldTileSize.x;
            float pushDist = axisCellSize * 0.5f;

            push += dirWorld * pushDist;
        }

        return push;
    }

    /// <summary>
    /// Busca recursivamente, dentro de la jerarqu\u00eda de un objeto ya instanciado, el primer
    /// Transform cuyo nombre contenga el substring indicado (sin distinguir may\u00fasculas).
    /// </summary>
    private Transform FindChildContaining(Transform root, string nameContains)
    {
        if (string.IsNullOrEmpty(nameContains)) return null;

        foreach (Transform child in root)
        {
            if (child.name.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return child;
            }

            Transform found = FindChildContaining(child, nameContains);
            if (found != null) return found;
        }

        return null;
    }

    private float ColorDistance(Color32 a, Color32 b)
    {
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        return Mathf.Sqrt(dr * dr + dg * dg + db * db);
    }

    // ==================== DECORACIONES (bot\u00f3n aparte, no toca la estructura) ====================

    [ContextMenu("Generate Decorations")]
    public void GenerateDecorations()
    {
        if (floorImages == null || floorImages.Count == 0)
        {
            Debug.LogError("[MultiLevelMapGenerator] Asign\u00e1 al menos una imagen en 'Floor Images' antes de generar decoraciones.");
            return;
        }

        if (wallPrefab == null || floorPrefab == null || doorPrefabHorizontal == null || stairsPrefab == null)
        {
            Debug.LogError("[MultiLevelMapGenerator] Faltan prefabs base (Wall/Floor/Door Horizontal/Stairs) para calcular el tama\u00f1o de celda. Son los mismos que us\u00e1s en 'Generate Map From Image'.");
            return;
        }

        bool hasWallDecor = wallLightPrefab != null && wallLightChance > 0f;
        bool hasFloorDecor = floorDecorOptions != null && floorDecorOptions.Count > 0 && floorDecorChance > 0f;

        if (!hasWallDecor && !hasFloorDecor)
        {
            Debug.LogWarning("[MultiLevelMapGenerator] No hay nada configurado para decorar: asign\u00e1 Wall Light Prefab y/o cargá 'Floor Decor Options'.");
            return;
        }

        var wallInfo = GetPrefabInfo(wallPrefab);
        var stairsInfo = GetPrefabInfo(stairsPrefab);
        var floorInfo = GetPrefabInfo(floorPrefab);
        var doorInfoH = GetPrefabInfo(doorPrefabHorizontal);

        Vector2 worldTileSize = ResolveWorldTileSize(wallInfo, stairsInfo, floorInfo, doorInfoH);
        if (worldTileSize.x <= 0f || worldTileSize.y <= 0f)
        {
            Debug.LogError("[MultiLevelMapGenerator] No se pudo detectar un tama\u00f1o de celda v\u00e1lido.");
            return;
        }

        float floorHeight;
        switch (floorHeightSource)
        {
            case FloorHeightSource.Stairs: floorHeight = stairsInfo.height; break;
            case FloorHeightSource.Wall: floorHeight = wallInfo.height; break;
            default: floorHeight = manualFloorHeight; break;
        }
        if (floorHeight <= 0f) floorHeight = manualFloorHeight;

        System.Random rng = useFixedRandomSeed ? new System.Random(randomSeed) : new System.Random();

        int totalPlaced = 0;

        for (int floorIndex = 0; floorIndex < floorImages.Count; floorIndex++)
        {
            Texture2D floorImage = floorImages[floorIndex];
            if (floorImage == null || !floorImage.isReadable)
            {
                Debug.LogWarning($"[MultiLevelMapGenerator] Piso {floorIndex}: imagen inv\u00e1lida o sin Read/Write Enabled. Se salte\u00f3 para decoraci\u00f3n.");
                continue;
            }

            mapImage = floorImage;
            int columns = Mathf.RoundToInt(mapImage.width / pixelsPerTile);
            int rows = Mathf.RoundToInt(mapImage.height / pixelsPerTile);
            if (columns <= 0 || rows <= 0) continue;

            TileType[,] grid = new TileType[columns, rows];
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    grid[col, row] = SampleTileType(col, row, columns, rows);
                }
            }

            string floorName = $"Floor_{floorIndex}";
            Transform floorParent = transform.Find(floorName);
            if (floorParent == null)
            {
                GameObject floorGO = new GameObject(floorName);
                floorGO.transform.SetParent(transform, false);
                Undo.RegisterCreatedObjectUndo(floorGO, "Generate Decorations");
                floorParent = floorGO.transform;
            }

            float floorY = stackFloorsDownward ? -(floorIndex * floorHeight) : (floorIndex * floorHeight);
            floorParent.localPosition = new Vector3(0f, floorY, 0f);

            // Las decoraciones van todas adentro de un hijo "Decor" separado de la estructura,
            // as\u00ed se pueden borrar/regenerar sin tocar paredes/piso/techo ya generados.
            Transform oldDecor = floorParent.Find("Decor");
            if (oldDecor != null)
            {
                Undo.DestroyObjectImmediate(oldDecor.gameObject);
            }
            GameObject decorGO = new GameObject("Decor");
            decorGO.transform.SetParent(floorParent, false);
            Undo.RegisterCreatedObjectUndo(decorGO, "Generate Decorations");
            activeParent = decorGO.transform;

            int placedThisFloor = GenerateFloorDecorations(grid, columns, rows, worldTileSize, wallInfo, floorInfo, rng, hasWallDecor, hasFloorDecor);
            totalPlaced += placedThisFloor;

            Debug.Log($"[MultiLevelMapGenerator] Decoraciones piso {floorIndex} ('{floorImage.name}'): {placedThisFloor} objetos.");
        }

        Debug.Log($"[MultiLevelMapGenerator] Decoraci\u00f3n lista. Total colocado: {totalPlaced}.");
    }

    [ContextMenu("Clear Decorations")]
    public void ClearDecorations()
    {
        int cleared = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform floorChild = transform.GetChild(i);
            Transform decor = floorChild.Find("Decor");
            if (decor != null)
            {
                Undo.DestroyObjectImmediate(decor.gameObject);
                cleared++;
            }
        }
        Debug.Log($"[MultiLevelMapGenerator] Decoraciones borradas en {cleared} piso(s).");
    }

    private int GenerateFloorDecorations(TileType[,] grid, int columns, int rows, Vector2 worldTileSize, PrefabPlacementInfo wallInfo, PrefabPlacementInfo floorInfo, System.Random rng, bool hasWallDecor, bool hasFloorDecor)
    {
        int placedCount = 0;
        bool InRange(int c, int r) => c >= 0 && c < columns && r >= 0 && r < rows;
        bool IsWall(int c, int r) => InRange(c, r) && grid[c, r] == TileType.Wall;
        bool IsFloor(int c, int r) => InRange(c, r) && grid[c, r] == TileType.Floor;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                TileType type = grid[col, row];

                // Luces: solo en paredes de tramo recto (no esquinas, no aisladas),
                // es decir, celdas de Wall que tienen un \u00fanico vecino de Piso claro.
                if (hasWallDecor && type == TileType.Wall)
                {
                    var segment = DetectSegmentRotation(grid, col, row, columns, rows);
                    if (segment.floorNeighborOffset != Vector2Int.zero && rng.NextDouble() < wallLightChance)
                    {
                        PlaceWallMountedDecor(wallLightPrefab, col, row, rows, worldTileSize, segment.floorNeighborOffset, wallInfo.height, ref placedCount);
                    }
                }

                // Decoraci\u00f3n de piso: celdas de Piso que tocan al menos una Pared (bordes/esquinas de sala).
                if (hasFloorDecor && type == TileType.Floor)
                {
                    bool touchesWall = IsWall(col - 1, row) || IsWall(col + 1, row) || IsWall(col, row - 1) || IsWall(col, row + 1);
                    if (!touchesWall) continue;

                    if (rng.NextDouble() >= floorDecorChance) continue;

                    GameObject chosen = PickWeightedRandom(floorDecorOptions, rng);
                    if (chosen == null) continue;

                    Vector3 cellCenter = GetCellCenterWorld(col, row, rows, worldTileSize);
                    float jitterX = ((float)rng.NextDouble() * 2f - 1f) * (worldTileSize.x * 0.5f * floorDecorJitterPercent);
                    float jitterZ = ((float)rng.NextDouble() * 2f - 1f) * (worldTileSize.y * 0.5f * floorDecorJitterPercent);

                    // La SUPERFICIE de caminar del piso no est\u00e1 en cellCenter.y (eso es el PIVOT del
                    // Floor Prefab) \u2014 el piso tiene su propio grosor, as\u00ed que su cara de ARRIBA est\u00e1
                    // m\u00e1s alta que su pivot. Calculamos esa superficie real primero.
                    float floorTopY = cellCenter.y + floorInfo.centerOffsetY + floorInfo.height * 0.5f;

                    // Y reci\u00e9n ah\u00ed corregimos el pivot del objeto elegido para que su BASE (no su
                    // pivot/centro) quede apoyada exactamente sobre esa superficie.
                    var chosenInfo = GetPrefabInfo(chosen);
                    float bottomOffsetFromPivot = chosenInfo.centerOffsetY - chosenInfo.height * 0.5f;
                    float positionY = floorTopY - bottomOffsetFromPivot;

                    Vector3 position = new Vector3(cellCenter.x + jitterX, positionY, cellCenter.z + jitterZ);

                    float rotationY = randomizeFloorDecorRotation ? (float)rng.NextDouble() * 360f : chosen.transform.eulerAngles.y;
                    Quaternion rotation = Quaternion.Euler(0f, rotationY, 0f);

                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(chosen);
                    instance.transform.position = position;
                    instance.transform.rotation = rotation;
                    instance.name = $"{chosen.name}_{col}_{row}";

                    if (parentToThis)
                    {
                        instance.transform.SetParent(activeParent, true);
                    }

                    Undo.RegisterCreatedObjectUndo(instance, "Generate Decorations");
                    placedCount++;
                }
            }
        }

        return placedCount;
    }

    private void PlaceWallMountedDecor(GameObject prefab, int col, int row, int rows, Vector2 worldTileSize, Vector2Int floorDir, float wallHeight, ref int placedCount)
    {
        Vector3 cellCenter = GetCellCenterWorld(col, row, rows, worldTileSize);
        Vector3 neighborCenter = GetCellCenterWorld(col + floorDir.x, row + floorDir.y, rows, worldTileSize);
        Vector3 dirToFloor = (neighborCenter - cellCenter).normalized;

        float edgeDistance = (floorDir.y != 0 ? worldTileSize.y : worldTileSize.x) * 0.5f;
        Vector3 position = cellCenter + dirToFloor * (edgeDistance + wallDecorForwardOffset);

        // Altura: en vez de quedarse a nivel de piso (Y=0), sube al porcentaje configurado
        // de la altura real de la pared (ej. 0.6 = 60% de la pared).
        position.y = cellCenter.y + wallHeight * wallDecorHeightPercent;

        Quaternion rotation = Quaternion.LookRotation(dirToFloor, Vector3.up);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = position;
        instance.transform.rotation = rotation;
        instance.name = $"{prefab.name}_{col}_{row}";

        if (parentToThis)
        {
            instance.transform.SetParent(activeParent, true);
        }

        Undo.RegisterCreatedObjectUndo(instance, "Generate Decorations");
        placedCount++;
    }

    private GameObject PickWeightedRandom(List<WeightedDecor> options, System.Random rng)
    {
        float total = 0f;
        foreach (var o in options)
        {
            if (o.prefab != null) total += Mathf.Max(0f, o.weight);
        }
        if (total <= 0f) return null;

        float roll = (float)(rng.NextDouble() * total);
        float cumulative = 0f;
        foreach (var o in options)
        {
            if (o.prefab == null) continue;
            cumulative += Mathf.Max(0f, o.weight);
            if (roll <= cumulative) return o.prefab;
        }

        return null;
    }

    private PrefabPlacementInfo GetPrefabInfo(GameObject prefab)
    {
        GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        temp.transform.position = Vector3.zero;
        temp.transform.rotation = Quaternion.identity;

        Renderer[] renderers = temp.GetComponentsInChildren<Renderer>();
        PrefabPlacementInfo result = new PrefabPlacementInfo();

        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            result.size = new Vector2(bounds.size.x, bounds.size.z);
            result.centerOffsetXZ = new Vector2(bounds.center.x, bounds.center.z);
            result.height = bounds.size.y;
            result.centerOffsetY = bounds.center.y;
        }

        DestroyImmediate(temp);
        return result;
    }
#else
    private void Awake()
    {
        Debug.LogWarning("[MapFromImageGenerator] Esta herramienta est\u00e1 pensada para usarse desde el Editor, no en build.");
    }
#endif
}