using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Genera un mapa 3D a partir de una imagen (Texture2D) donde cada bloque de color
/// representa un prefab distinto (pared, escalera, piso, puerta).
///
/// IMPORTANTE - Configuraci�n de la textura en el Import Settings:
///  - Read/Write Enabled: ON
///  - Non-Power of 2: None
///  - Compression: None
///  - Filter Mode: Point (No Filter)
///
/// v2: corrige dos problemas de la v1:
///  1) Las paredes ahora rotan 90� autom�ticamente seg�n si el tramo es horizontal o vertical,
///     analizando los vecinos en la grilla (no solo copian la rotaci�n del prefab).
///  2) Se corrige el "pivot offset": si el prefab no tiene su pivot centrado en su mesh,
///     el script ahora compensa esa diferencia para que la geometr�a quede centrada
///     exactamente en su celda (elimina los gaps entre pared y piso).
/// </summary>
public class MapFromImageGenerator : MonoBehaviour
{
    private enum TileType { None, Wall, Stairs, Floor, Door, Column }

    [Header("Imagen fuente")]
    public Texture2D mapImage;

    [Tooltip("Tama\u00f1o en p\u00edxeles de UN bloque/tile en la imagen.")]
    public float pixelsPerTile = 90f;

    [Header("Prefabs")]
    public GameObject wallPrefab;
    public GameObject stairsPrefab;
    public GameObject floorPrefab;
    public GameObject doorPrefab;

    [Header("Prefabs adicionales")]
    [Tooltip("Se genera uno por cada celda de Piso, a la altura indicada m\u00e1s abajo.")]
    public GameObject ceilingPrefab;

    [Tooltip("Se genera en cada esquina donde dos tramos de pared se cruzan en \u00e1ngulo recto.")]
    public GameObject columnPrefab;

    [Header("Colores (coinciden con tu leyenda)")]
    public Color32 wallColor = new Color32(0x49, 0x5b, 0x24, 0xFF);
    public Color32 stairsColor = new Color32(0x24, 0x89, 0xc7, 0xFF);
    public Color32 floorColor = new Color32(0x8e, 0x21, 0x21, 0xFF);
    public Color32 doorColor = new Color32(0xf1, 0xaf, 0x15, 0xFF);

    [Tooltip("Color que marca en la imagen d\u00f3nde va una COLUMNA. Se coloca ah\u00ed y se empuja contra la(s) pared(es) que la tocan.")]
    public Color32 columnColor = new Color32(0xc1, 0x12, 0xdc, 0xFF); // #c112dc

    [Range(1, 100)]
    public float colorMatchThreshold = 30f;

    private enum TileSizeReference { AutoMinimo, Wall, Stairs, Floor, Door, Manual }

    [Header("Espaciado en el mundo")]
    [Tooltip("Qu\u00e9 prefab define el tama\u00f1o real de UNA celda de la grilla. 'AutoMinimo' usa el m\u00e1s chico de los 4 (recomendado: si tu Floor es una losa grande que representa varias celdas juntas, NO lo uses como referencia, va a estirar toda la grilla).")]
    [SerializeField] private TileSizeReference tileSizeReference = TileSizeReference.AutoMinimo;
    public Vector2 manualWorldTileSize = new Vector2(10f, 10f);

    [Header("Rotaci\u00f3n de paredes / puertas")]
    [Tooltip("Si tu prefab de pared, en rotaci\u00f3n 0, corre a lo largo del eje X (horizontal), dejalo en 0. Si corre a lo largo del eje Z (vertical), poné 90 ac\u00e1 para invertir la l\u00f3gica.")]
    public float wallBaseRotationY = 0f;

    [Tooltip("Aplicar la misma l\u00f3gica de rotaci\u00f3n autom\u00e1tica a las puertas (\u00fatil si la puerta tambi\u00e9n es un tramo direccional).")]
    public bool autoRotateDoors = true;

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

    [Header("Columnas (marcadas con color en la imagen)")]
    [Tooltip("Si est\u00e1 activo, la columna se empuja hacia la(s) pared(es) vecina(s) para quedar pegada, en vez de flotar centrada en el medio de su celda.")]
    public bool snapColumnsToWalls = true;

    [Header("Puerta compuesta (m\u00faltiples GameObjects)")]
    [Tooltip("Substring (sin distinguir may\u00fasculas) para encontrar, DENTRO del prefab de la puerta (que ahora tiene hoja + piso + 2 paredes), el GameObject que representa la HOJA de la puerta. Ese hijo se rota para mirar hacia el cuarto; el resto del prefab (piso/paredes internas) mantiene la rotaci\u00f3n general de la celda.")]
    public string doorLeafNameContains = "Door";

    [Tooltip("Activ\u00e1 esto si la hoja de la puerta queda mirando para el lado contrario del que deber\u00eda (hacia afuera del cuarto en vez de hacia adentro).")]
    public bool invertDoorFacing = false;

    [Header("Organizaci\u00f3n")]
    public bool parentToThis = true;
    public bool clearBeforeGenerating = true;

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
        if (mapImage == null)
        {
            Debug.LogError("[MapFromImageGenerator] Asign\u00e1 una Map Image antes de generar.");
            return;
        }

        if (!mapImage.isReadable)
        {
            Debug.LogError("[MapFromImageGenerator] La textura no tiene 'Read/Write Enabled' activado.");
            return;
        }

        if (wallPrefab == null || stairsPrefab == null || floorPrefab == null || doorPrefab == null)
        {
            Debug.LogError("[MapFromImageGenerator] Falta asignar alg\u00fan prefab.");
            return;
        }

        if (clearBeforeGenerating)
        {
            ClearGeneratedMap();
        }

        int columns = Mathf.RoundToInt(mapImage.width / pixelsPerTile);
        int rows = Mathf.RoundToInt(mapImage.height / pixelsPerTile);

        if (columns <= 0 || rows <= 0)
        {
            Debug.LogError("[MapFromImageGenerator] El c\u00e1lculo de columnas/filas dio 0. Revis\u00e1 Pixels Per Tile.");
            return;
        }

        // Paso 1: muestrear toda la grilla UNA vez y guardar el tipo de cada celda.
        // Esto nos permite despu\u00e9s mirar vecinos sin volver a leer la textura.
        TileType[,] grid = new TileType[columns, rows];
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                grid[col, row] = SampleTileType(col, row, columns, rows);
            }
        }

        // Paso 2: medir tama\u00f1o y offset de pivot de cada prefab (una sola vez por tipo).
        var wallInfo = GetPrefabInfo(wallPrefab);
        var stairsInfo = GetPrefabInfo(stairsPrefab);
        var floorInfo = GetPrefabInfo(floorPrefab);
        var doorInfo = GetPrefabInfo(doorPrefab);
        var columnInfo = columnPrefab != null ? GetPrefabInfo(columnPrefab) : default;

        Vector2 worldTileSize;
        switch (tileSizeReference)
        {
            case TileSizeReference.Wall: worldTileSize = wallInfo.size; break;
            case TileSizeReference.Stairs: worldTileSize = stairsInfo.size; break;
            case TileSizeReference.Floor: worldTileSize = floorInfo.size; break;
            case TileSizeReference.Door: worldTileSize = doorInfo.size; break;
            case TileSizeReference.Manual: worldTileSize = manualWorldTileSize; break;
            default:
                // AutoMinimo: elige el prefab cuya relaci\u00f3n de aspecto (lado largo / lado corto)
                // sea m\u00e1s cercana a 1, es decir, el m\u00e1s "cuadrado". Un prefab angosto como una pared
                // (ej. 4.91 x 0.65) NO deber\u00eda ganar solo por tener poca \u00e1rea: su lado corto es
                // su grosor f\u00edsico, no representa el tama\u00f1o de una celda de grilla.
                worldTileSize = wallInfo.size;
                float bestAspect = float.MaxValue;
                foreach (var candidate in new[] { wallInfo, stairsInfo, floorInfo, doorInfo })
                {
                    if (candidate.size.x <= 0f || candidate.size.y <= 0f) continue;
                    float aspect = Mathf.Max(candidate.size.x, candidate.size.y) / Mathf.Min(candidate.size.x, candidate.size.y);
                    if (aspect < bestAspect)
                    {
                        bestAspect = aspect;
                        worldTileSize = candidate.size;
                    }
                }
                break;
        }

        if (worldTileSize.x <= 0f || worldTileSize.y <= 0f)
        {
            Debug.LogError("[MapFromImageGenerator] No se pudo detectar un tama\u00f1o de celda v\u00e1lido.");
            return;
        }

        Debug.Log($"[MapFromImageGenerator] Tama\u00f1os detectados -> Wall: {wallInfo.size}, Stairs: {stairsInfo.size}, Floor: {floorInfo.size}, Door: {doorInfo.size}. Tile Size Reference elegida: {tileSizeReference} -> worldTileSize usado: {worldTileSize}. Grilla: {columns}x{rows}.");

        int placedCount = 0;

        // Paso 3: instanciar, ahora s\u00ed con rotaci\u00f3n correcta y pivot corregido.
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                TileType type = grid[col, row];
                if (type == TileType.None) continue;
                if (type == TileType.Column && columnPrefab == null)
                {
                    Debug.LogWarning($"[MapFromImageGenerator] Hay una celda rosa (Column) en ({col},{row}) pero no asignaste Column Prefab. Se salte\u00f3.");
                    continue;
                }

                GameObject prefab;
                PrefabPlacementInfo info;
                bool useAutoRotation;

                switch (type)
                {
                    case TileType.Wall:
                        prefab = wallPrefab; info = wallInfo; useAutoRotation = true; break;
                    case TileType.Door:
                        prefab = doorPrefab; info = doorInfo; useAutoRotation = autoRotateDoors; break;
                    case TileType.Stairs:
                        prefab = stairsPrefab; info = stairsInfo; useAutoRotation = false; break;
                    case TileType.Column:
                        prefab = columnPrefab; info = columnInfo; useAutoRotation = false; break;
                    default:
                        prefab = floorPrefab; info = floorInfo; useAutoRotation = false; break;
                }

                float rotationY = prefab.transform.eulerAngles.y;
                Vector2Int floorNeighborOffset = Vector2Int.zero;
                if (useAutoRotation)
                {
                    var segment = DetectSegmentRotation(grid, col, row, columns, rows);
                    rotationY = wallBaseRotationY + segment.rotation;
                    floorNeighborOffset = segment.floorNeighborOffset;
                }

                Quaternion rotation = Quaternion.Euler(0f, rotationY, 0f);

                // Centro de la celda en el mundo (esto define D\u00d3NDE est\u00e1 la celda en la grilla,
                // basado siempre en worldTileSize, que a su vez viene del Floor)
                Vector3 cellCenterWorld = GetCellCenterWorld(col, row, rows, worldTileSize);

                // Offset del pivot rotado, para que el CENTRO de la mesh (no el pivot) caiga en el centro
                // de la celda. Para Door no se usa (esa se posiciona por medici\u00f3n directa m\u00e1s abajo).
                Vector3 rotatedOffset = rotation * new Vector3(info.centerOffsetXZ.x, 0f, info.centerOffsetXZ.y);
                Vector3 finalPosition = cellCenterWorld - rotatedOffset;

                // Direcci\u00f3n hacia el piso vecino (world space). Se usa tanto para el "pegado"
                // como para orientar la hoja de la puerta, as\u00ed que la calculamos una sola vez.
                Vector3 directionToFloor = Vector3.zero;
                if (floorNeighborOffset != Vector2Int.zero)
                {
                    Vector3 neighborCellCenter = GetCellCenterWorld(col + floorNeighborOffset.x, row + floorNeighborOffset.y, rows, worldTileSize);
                    directionToFloor = (neighborCellCenter - cellCenterWorld).normalized;
                }

                // "Pegado" al piso: en vez de dejar la pared/puerta centrada en el medio de
                // una celda que puede ser mucho m\u00e1s grande que su propio grosor, la empujamos
                // hacia el borde que toca al piso vecino, dejando solo su grosor real como separaci\u00f3n.
                bool shouldSnap = (type == TileType.Wall && snapWallsToFloorEdge) ||
                                   (type == TileType.Door && snapDoorsToFloorEdge && useAutoRotation);

                if (shouldSnap && floorNeighborOffset != Vector2Int.zero)
                {
                    float thickness = Mathf.Min(info.size.x, info.size.y);
                    float cellDepthInPushDirection = (floorNeighborOffset.y != 0) ? worldTileSize.y : worldTileSize.x;
                    float pushDistance = Mathf.Max(0f, (cellDepthInPushDirection - thickness) * 0.5f);

                    finalPosition += directionToFloor * pushDistance;
                }

                // Columna: se empuja contra CADA pared/puerta vecina que la toque (izq, der, arriba, abajo),
                // hasta el BORDE completo de su propia celda (medio-cell, sin restar el grosor de la columna).
                // Esto hace que el CENTRO del GameObject quede exactamente en el v\u00e9rtice donde la celda de
                // la columna se cruza con la celda de la pared. Si tiene dos paredes perpendiculares (una
                // esquina real), los dos empujes se suman y el centro cae justo en la esquina.
                if (type == TileType.Column && snapColumnsToWalls)
                {
                    Vector3 columnPush = Vector3.zero;
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

                        bool isRowAxis = dir.y != 0; // moverse en row cambia principalmente Z
                        float axisCellSize = isRowAxis ? worldTileSize.y : worldTileSize.x;
                        float pushDist = axisCellSize * 0.5f; // medio-cell completo, sin restar grosor

                        columnPush += dirWorld * pushDist;
                    }

                    finalPosition += columnPush;
                }

                GameObject instance;

                if (type == TileType.Door)
                {
                    // Puerta compuesta: la rotamos a su rotaci\u00f3n FINAL directamente (sin RotateAround
                    // ni offsets precalculados sobre una copia temporal). Despu\u00e9s medimos el centro
                    // REAL de su geometr\u00eda YA ROTADA (en la instancia real, en escena) y corregimos
                    // la posici\u00f3n para que ese centro caiga exactamente donde corresponde. As\u00ed no
                    // importa el \u00e1ngulo de rotaci\u00f3n: siempre se mide y corrige sobre el resultado real.
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.transform.position = cellCenterWorld; // posici\u00f3n temporal, se corrige abajo
                    instance.transform.rotation = rotation;
                    instance.name = $"{prefab.name}_{col}_{row}";

                    if (parentToThis)
                    {
                        instance.transform.SetParent(transform, true);
                    }

                    Vector3 desiredCenter = cellCenterWorld;
                    if (shouldSnap && floorNeighborOffset != Vector2Int.zero)
                    {
                        float thickness = Mathf.Min(info.size.x, info.size.y);
                        float cellDepthInPushDirection = (floorNeighborOffset.y != 0) ? worldTileSize.y : worldTileSize.x;
                        float pushDistance = Mathf.Max(0f, (cellDepthInPushDirection - thickness) * 0.5f);
                        desiredCenter += directionToFloor * pushDistance;
                    }

                    Vector3 currentCenter = GetInstanceBoundsCenter(instance);
                    instance.transform.position += desiredCenter - currentCenter;
                }
                else
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.transform.position = finalPosition;
                    instance.transform.rotation = rotation;
                    instance.name = $"{prefab.name}_{col}_{row}";

                    if (parentToThis)
                    {
                        instance.transform.SetParent(transform, true);
                    }
                }

                Undo.RegisterCreatedObjectUndo(instance, "Generate Map From Image");
                placedCount++;

                // Puerta compuesta: adentro del prefab hay varios GameObjects (hoja, piso, 2 paredes).
                // Buscamos el que representa la hoja de la puerta y lo rotamos para que mire al cuarto,
                // sin tocar la rotaci\u00f3n general del resto del conjunto.
                if (type == TileType.Door && directionToFloor != Vector3.zero)
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

        // Paso 4: techo. Se genera un ceilingPrefab por cada celda de Piso, a la altura calculada.
        if (generateCeiling && ceilingPrefab != null)
        {
            var ceilingInfo = GetPrefabInfo(ceilingPrefab);
            float ceilingHeight = autoDetectCeilingHeight ? wallInfo.height : manualCeilingHeight;
            if (ceilingHeight <= 0f)
            {
                Debug.LogWarning("[MapFromImageGenerator] La altura de techo detectada fue 0 (revis\u00e1 que el Wall Prefab tenga un Renderer con altura v\u00e1lida). Se us\u00f3 Manual Ceiling Height como respaldo.");
                ceilingHeight = manualCeilingHeight;
            }

            int ceilingCount = 0;
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
                        ceilingInstance.transform.SetParent(transform, true);
                    }

                    Undo.RegisterCreatedObjectUndo(ceilingInstance, "Generate Map From Image");
                    ceilingCount++;
                }
            }

            Debug.Log($"[MapFromImageGenerator] Techo generado: {ceilingCount} tiles a altura {ceilingHeight}.");
        }

        Debug.Log($"[MapFromImageGenerator] Grilla {columns}x{rows}. Tiles colocados: {placedCount}.");
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

    /// <summary>
    /// Analiza los vecinos de una celda de pared/puerta para determinar:
    /// - rotation: 0 si el tramo es horizontal, 90 si es vertical.
    /// - floorNeighborOffset: la direcci\u00f3n (en coordenadas de grilla) hacia el vecino que es Piso,
    ///   usada despu\u00e9s para "pegar" la pared/puerta contra ese borde en vez de dejarla centrada.
    /// </summary>
    private (float rotation, Vector2Int floorNeighborOffset) DetectSegmentRotation(TileType[,] grid, int col, int row, int columns, int rows)
    {
        bool InRange(int c, int r) => c >= 0 && c < columns && r >= 0 && r < rows;
        bool IsBoundary(int c, int r) => InRange(c, r) && (grid[c, r] == TileType.Wall || grid[c, r] == TileType.Door);
        bool IsFloor(int c, int r) => InRange(c, r) && grid[c, r] == TileType.Floor;

        bool leftRight = IsBoundary(col - 1, row) || IsBoundary(col + 1, row);
        bool upDown = IsBoundary(col, row - 1) || IsBoundary(col, row + 1);

        float rotation;
        if (leftRight && !upDown) rotation = 0f;        // tramo horizontal -> el piso vecino est\u00e1 arriba o abajo
        else if (upDown && !leftRight) rotation = 90f;  // tramo vertical -> el piso vecino est\u00e1 a izq o der
        else rotation = 0f;                              // caso ambiguo (esquina/aislada): default horizontal

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

        // Si no encontramos piso en la direcci\u00f3n "esperada" seg\u00fan la orientaci\u00f3n, probamos las 4 direcciones igual
        // (\u00fatil para esquinas o casos ambiguos donde el piso puede estar en cualquier lado).
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
    /// Mide el centro real (world space) de la geometr\u00eda de un objeto YA instanciado en la escena,
    /// combinando los bounds de todos sus Renderers. A diferencia de GetPrefabInfo (que mide sobre
    /// una copia temporal en el origen), esto se usa para rotar un objeto real alrededor de su propio
    /// centro geom\u00e9trico actual con RotateAround, sin depender de c\u00e1lculos de offset precomputados.
    /// </summary>
    private Vector3 GetInstanceBoundsCenter(GameObject instance)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return instance.transform.position;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds.center;
    }

    /// <summary>
    /// Calcula el centro en el mundo de una celda de grilla dada (misma f\u00f3rmula usada en el bucle principal).
    /// </summary>
    private Vector3 GetCellCenterWorld(int col, int row, int rows, Vector2 worldTileSize)
    {
        float x = (col + 0.5f) * worldTileSize.x;
        float z = (rows - 1 - row + 0.5f) * worldTileSize.y;
        return transform.position + new Vector3(x, 0f, z);
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
            // Como el transform est\u00e1 en (0,0,0) con rotaci\u00f3n identidad, bounds.center
            // en world space equivale directamente al offset local entre el pivot y el centro de la mesh.
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