using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public enum SurfaceType
{
    Grass = 0,
    Sand = 1,
    Rough = 2,
    Green = 3,
    Fairway = 4,
    Tee = 5,
    Water = 6,
    Cart_Path = 7
}

[System.Serializable]
public class SurfaceProperties
{
    [Header("Surface Settings")]
    public SurfaceType surfaceType;
    
    [Header("Ball Physics")]
    [Range(0.1f, 3f)]
    public float ballRollResistance = 1f;
    [Range(0f, 1f)]
    public float ballBounce = 0.5f;
    [Range(0.1f, 2f)]
    public float ballSpin = 1f;
    
    [Header("Audio & Effects")]
    public AudioClip impactSound;
    public ParticleSystem impactParticles;
    public Color debugColor = Color.white;
    
    public SurfaceProperties()
    {
        surfaceType = SurfaceType.Grass;
        ballRollResistance = 1f;
        ballBounce = 0.5f;
        ballSpin = 1f;
        debugColor = Color.white;
    }
}

[System.Serializable]
public class TerrainGenerationSettings
{
    [Header("Terrain Dimensions")]
    public int terrainWidth = 1024;
    public int terrainHeight = 1024;
    public int terrainDepth = 600;
    public float terrainScale = 20f;
    
    [Header("Heightmap Settings")]
    public AnimationCurve heightCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [Range(0.001f, 0.1f)]
    public float noiseScale = 0.01f;
    [Range(0.01f, 1f)]
    public float heightMultiplier = 0.1f;
    [Range(1, 8)]
    public int octaves = 4;
    [Range(0.1f, 1f)]
    public float persistence = 0.5f;
    [Range(1f, 4f)]
    public float lacunarity = 2f;
}

[System.Serializable]
public class TextureSettings
{
    [Header("Surface Textures")]
    [Tooltip("Regular grass texture for general areas")]
    public Texture2D grassTexture;
    public Texture2D grassNormal;
    [Range(5f, 50f)]
    public float grassTileSize = 20f;
    
    [Space(5)]
    [Tooltip("Sand texture for bunkers and hazards")]
    public Texture2D sandTexture;
    public Texture2D sandNormal;
    [Range(5f, 50f)]
    public float sandTileSize = 15f;
    
    [Space(5)]
    [Tooltip("Rough grass texture for difficult areas")]
    public Texture2D roughTexture;
    public Texture2D roughNormal;
    [Range(5f, 50f)]
    public float roughTileSize = 25f;
    
    [Space(5)]
    [Tooltip("High-quality green texture for putting areas")]
    public Texture2D greenTexture;
    public Texture2D greenNormal;
    [Range(5f, 50f)]
    public float greenTileSize = 10f;
    
    [Space(5)]
    [Tooltip("Maintained fairway grass texture")]
    public Texture2D fairwayTexture;
    public Texture2D fairwayNormal;
    [Range(5f, 50f)]
    public float fairwayTileSize = 18f;
    
    [Space(5)]
    [Tooltip("Tee box grass texture")]
    public Texture2D teeTexture;
    public Texture2D teeNormal;
    [Range(5f, 50f)]
    public float teeTileSize = 12f;
    
    [Space(5)]
    [Tooltip("Water texture for water hazards")]
    public Texture2D waterTexture;
    public Texture2D waterNormal;
    [Range(5f, 50f)]
    public float waterTileSize = 30f;
    
    [Space(5)]
    [Tooltip("Cart path concrete/gravel texture")]
    public Texture2D cartPathTexture;
    public Texture2D cartPathNormal;
    [Range(5f, 50f)]
    public float cartPathTileSize = 8f;
    
    // Getter methods to maintain array compatibility with existing code
    public Texture2D[] GetTerrainTextures()
    {
        return new Texture2D[]
        {
            grassTexture,     // SurfaceType.Grass = 0
            sandTexture,      // SurfaceType.Sand = 1
            roughTexture,     // SurfaceType.Rough = 2
            greenTexture,     // SurfaceType.Green = 3
            fairwayTexture,   // SurfaceType.Fairway = 4
            teeTexture,       // SurfaceType.Tee = 5
            waterTexture,     // SurfaceType.Water = 6
            cartPathTexture   // SurfaceType.Cart_Path = 7
        };
    }
    
    public Texture2D[] GetNormalMaps()
    {
        return new Texture2D[]
        {
            grassNormal,
            sandNormal,
            roughNormal,
            greenNormal,
            fairwayNormal,
            teeNormal,
            waterNormal,
            cartPathNormal
        };
    }
    
    public Vector2[] GetTileSizes()
    {
        return new Vector2[]
        {
            Vector2.one * grassTileSize,
            Vector2.one * sandTileSize,
            Vector2.one * roughTileSize,
            Vector2.one * greenTileSize,
            Vector2.one * fairwayTileSize,
            Vector2.one * teeTileSize,
            Vector2.one * waterTileSize,
            Vector2.one * cartPathTileSize
        };
    }
    
    // Get texture for specific surface type
    public Texture2D GetTextureForSurface(SurfaceType surfaceType)
    {
        switch (surfaceType)
        {
            case SurfaceType.Grass: return grassTexture;
            case SurfaceType.Sand: return sandTexture;
            case SurfaceType.Rough: return roughTexture;
            case SurfaceType.Green: return greenTexture;
            case SurfaceType.Fairway: return fairwayTexture;
            case SurfaceType.Tee: return teeTexture;
            case SurfaceType.Water: return waterTexture;
            case SurfaceType.Cart_Path: return cartPathTexture;
            default: return grassTexture;
        }
    }
    
    // Get normal map for specific surface type
    public Texture2D GetNormalForSurface(SurfaceType surfaceType)
    {
        switch (surfaceType)
        {
            case SurfaceType.Grass: return grassNormal;
            case SurfaceType.Sand: return sandNormal;
            case SurfaceType.Rough: return roughNormal;
            case SurfaceType.Green: return greenNormal;
            case SurfaceType.Fairway: return fairwayNormal;
            case SurfaceType.Tee: return teeNormal;
            case SurfaceType.Water: return waterNormal;
            case SurfaceType.Cart_Path: return cartPathNormal;
            default: return grassNormal;
        }
    }
    
    // Get tile size for specific surface type
    public float GetTileSizeForSurface(SurfaceType surfaceType)
    {
        switch (surfaceType)
        {
            case SurfaceType.Grass: return grassTileSize;
            case SurfaceType.Sand: return sandTileSize;
            case SurfaceType.Rough: return roughTileSize;
            case SurfaceType.Green: return greenTileSize;
            case SurfaceType.Fairway: return fairwayTileSize;
            case SurfaceType.Tee: return teeTileSize;
            case SurfaceType.Water: return waterTileSize;
            case SurfaceType.Cart_Path: return cartPathTileSize;
            default: return 15f;
        }
    }
}

public class GolfTerrainManager : MonoBehaviour
{
    [Header("═══ TERRAIN SETUP ═══")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private TerrainData terrainData;
    
    [Header("═══ GENERATION SETTINGS ═══")]
    [SerializeField] private TerrainGenerationSettings generationSettings = new TerrainGenerationSettings();
    
    [Header("═══ TEXTURE CONFIGURATION ═══")]
    [SerializeField] private TextureSettings textureSettings = new TextureSettings();
    
    [Header("═══ SURFACE PROPERTIES ═══")]
    [SerializeField] private SurfaceProperties[] surfaceProperties = new SurfaceProperties[8];
    
    [Header("═══ GOLF COURSE FEATURES ═══")]
    [Tooltip("Drag GameObjects here to mark tee positions")]
    [SerializeField] private Transform[] teePositions = new Transform[0];
    
    [Tooltip("Drag GameObjects here to mark pin/hole positions")]  
    [SerializeField] private Transform[] pinPositions = new Transform[0];
    
    [Tooltip("Drag GameObjects here to mark sand traps and water hazards")]
    [SerializeField] private Transform[] hazardAreas = new Transform[0];
    
    [Header("═══ DEBUG OPTIONS ═══")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool autoGenerateOnStart = true;
    
    private TerrainDetector terrainDetector;
    private Dictionary<int, SurfaceType> textureToSurfaceMap;
    
    void Start()
    {
        if (autoGenerateOnStart)
        {
            InitializeTerrain();
            InitializeSurfaceMapping();
            GenerateGolfCourse();
        }
    }
    
    void InitializeTerrain()
    {
        if (terrain == null)
            terrain = GetComponent<Terrain>();
            
        if (terrain == null)
        {
            GameObject terrainObject = Terrain.CreateTerrainGameObject(null);
            terrain = terrainObject.GetComponent<Terrain>();
            terrainObject.transform.SetParent(transform);
        }
        
        if (terrainData == null)
        {
            terrainData = new TerrainData();
            terrainData.heightmapResolution = generationSettings.terrainWidth + 1;
            terrainData.size = new Vector3(generationSettings.terrainWidth, generationSettings.terrainDepth, generationSettings.terrainHeight);
            terrain.terrainData = terrainData;
        }
        
        terrainDetector = new TerrainDetector();
    }
    
    void InitializeSurfaceMapping()
    {
        textureToSurfaceMap = new Dictionary<int, SurfaceType>();
        
        if (surfaceProperties == null || surfaceProperties.Length == 0)
        {
            surfaceProperties = new SurfaceProperties[8];
            for (int i = 0; i < surfaceProperties.Length; i++)
            {
                surfaceProperties[i] = new SurfaceProperties
                {
                    surfaceType = (SurfaceType)i,
                    ballRollResistance = 1f,
                    ballBounce = 0.5f,
                    ballSpin = 1f,
                    debugColor = Color.HSVToRGB(i / 8f, 0.7f, 1f)
                };
            }
        }
        
        for (int i = 0; i < surfaceProperties.Length; i++)
        {
            textureToSurfaceMap[i] = surfaceProperties[i].surfaceType;
        }
    }
    
    void GenerateGolfCourse()
    {
        GenerateHeightmap();
        SetupTerrainLayers();
        GenerateSplatMap();
        ConfigureTerrainSettings();
    }
    
    void GenerateHeightmap()
    {
        int width = terrainData.heightmapResolution;
        int height = terrainData.heightmapResolution;
        float[,] heights = new float[width, height];
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float noiseValue = GeneratePerlinNoise(x, y, width, height);
                heights[x, y] = generationSettings.heightCurve.Evaluate(noiseValue) * generationSettings.heightMultiplier;
            }
        }
        
        SmoothAroundFeatures(heights, width, height);
        terrainData.SetHeights(0, 0, heights);
    }
    
    float GeneratePerlinNoise(int x, int y, int width, int height)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = generationSettings.noiseScale;
        
        for (int i = 0; i < generationSettings.octaves; i++)
        {
            float sampleX = x * frequency;
            float sampleY = y * frequency;
            
            float noiseValue = Mathf.PerlinNoise(sampleX, sampleY);
            value += noiseValue * amplitude;
            
            amplitude *= generationSettings.persistence;
            frequency *= generationSettings.lacunarity;
        }
        
        return value;
    }
    
    void SmoothAroundFeatures(float[,] heights, int width, int height)
    {
        if (teePositions != null)
        {
            foreach (Transform tee in teePositions)
            {
                if (tee != null)
                    SmoothCircularArea(heights, tee.position, 20f, width, height);
            }
        }
        
        if (pinPositions != null)
        {
            foreach (Transform pin in pinPositions)
            {
                if (pin != null)
                    SmoothCircularArea(heights, pin.position, 30f, width, height);
            }
        }
    }
    
    void SmoothCircularArea(float[,] heights, Vector3 worldPos, float radius, int width, int height)
    {
        Vector3 terrainPos = worldPos - terrain.transform.position;
        int centerX = Mathf.RoundToInt((terrainPos.x / terrainData.size.x) * width);
        int centerY = Mathf.RoundToInt((terrainPos.z / terrainData.size.z) * height);
        
        int radiusInPixels = Mathf.RoundToInt((radius / terrainData.size.x) * width);
        
        for (int x = centerX - radiusInPixels; x <= centerX + radiusInPixels; x++)
        {
            for (int y = centerY - radiusInPixels; y <= centerY + radiusInPixels; y++)
            {
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                    if (distance <= radiusInPixels)
                    {
                        float smoothFactor = 1f - (distance / radiusInPixels);
                        float targetHeight = heights[centerX, centerY];
                        heights[x, y] = Mathf.Lerp(heights[x, y], targetHeight, smoothFactor * 0.8f);
                    }
                }
            }
        }
    }
    
    void SetupTerrainLayers()
    {
        // Get textures using the new named approach
        Texture2D[] textures = textureSettings.GetTerrainTextures();
        Texture2D[] normals = textureSettings.GetNormalMaps();
        Vector2[] tileSizes = textureSettings.GetTileSizes();
        
        if (textures == null || textures.Length == 0) return;
        
        TerrainLayer[] layers = new TerrainLayer[textures.Length];
        
        for (int i = 0; i < textures.Length; i++)
        {
            if (textures[i] == null) continue;
            
            layers[i] = new TerrainLayer();
            layers[i].diffuseTexture = textures[i];
            
            if (normals != null && i < normals.Length && normals[i] != null)
                layers[i].normalMapTexture = normals[i];
                
            if (tileSizes != null && i < tileSizes.Length)
                layers[i].tileSize = tileSizes[i];
            else
                layers[i].tileSize = Vector2.one * 25f; // Fallback tile size
                
            // Optimize for high-resolution textures
            layers[i].metallic = 0f; // Most golf surfaces aren't metallic
            layers[i].smoothness = GetSurfaceSmoothness((SurfaceType)i);
        }
        
        terrainData.terrainLayers = layers;
    }
    
    float GetSurfaceSmoothness(SurfaceType surfaceType)
    {
        switch (surfaceType)
        {
            case SurfaceType.Water: return 0.9f;
            case SurfaceType.Cart_Path: return 0.6f;
            case SurfaceType.Green: return 0.3f;
            case SurfaceType.Tee: return 0.25f;
            case SurfaceType.Fairway: return 0.2f;
            case SurfaceType.Grass: return 0.15f;
            case SurfaceType.Sand: return 0.1f;
            case SurfaceType.Rough: return 0.05f;
            default: return 0.2f;
        }
    }
    
    void GenerateSplatMap()
    {
        int alphamapWidth = terrainData.alphamapWidth;
        int alphamapHeight = terrainData.alphamapHeight;
        int numTextures = terrainData.terrainLayers.Length;
        
        if (numTextures == 0)
        {
            Debug.LogWarning("No terrain layers found! Please set up textures first.");
            return;
        }
        
        float[,,] splatmapData = new float[alphamapWidth, alphamapHeight, numTextures];
        
        for (int y = 0; y < alphamapHeight; y++)
        {
            for (int x = 0; x < alphamapWidth; x++)
            {
                Vector3 worldPos = new Vector3(
                    (x / (float)alphamapWidth) * terrainData.size.x,
                    0,
                    (y / (float)alphamapHeight) * terrainData.size.z
                ) + terrain.transform.position;
                
                float height = terrain.SampleHeight(worldPos);
                float normalizedHeight = height / terrainData.size.y;
                
                int primaryTexture = DetermineSurfaceType(worldPos, normalizedHeight);
                primaryTexture = Mathf.Clamp(primaryTexture, 0, numTextures - 1);
                
                splatmapData[y, x, primaryTexture] = 1f;
                BlendTextures(splatmapData, x, y, alphamapWidth, alphamapHeight, numTextures);
            }
        }
        
        terrainData.SetAlphamaps(0, 0, splatmapData);
    }
    
    int DetermineSurfaceType(Vector3 worldPos, float normalizedHeight)
    {
        if (IsNearFeature(worldPos, teePositions, 15f))
            return (int)SurfaceType.Tee;
            
        if (IsNearFeature(worldPos, pinPositions, 25f))
            return (int)SurfaceType.Green;
            
        if (IsNearFeature(worldPos, hazardAreas, 10f))
            return (int)SurfaceType.Sand;
        
        if (normalizedHeight < 0.3f)
            return (int)SurfaceType.Fairway;
        else if (normalizedHeight < 0.6f)
            return (int)SurfaceType.Grass;
        else
            return (int)SurfaceType.Rough;
    }
    
    bool IsNearFeature(Vector3 worldPos, Transform[] features, float distance)
    {
        if (features == null) return false;
        
        foreach (Transform feature in features)
        {
            if (feature != null && Vector3.Distance(worldPos, feature.position) <= distance)
                return true;
        }
        return false;
    }
    
    void BlendTextures(float[,,] splatmapData, int x, int y, int width, int height, int numTextures)
    {
        for (int i = 0; i < numTextures; i++)
        {
            if (splatmapData[y, x, i] > 0)
            {
                float noise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f);
                splatmapData[y, x, i] = Mathf.Clamp01(splatmapData[y, x, i] + noise * 0.1f);
            }
        }
    }
    
    void ConfigureTerrainSettings()
    {
        // Configure terrain rendering settings for high-res textures
        terrain.drawHeightmap = true;
        terrain.drawTreesAndFoliage = true;
        terrain.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
        
        // Optimized LOD settings for high-res textures
        terrain.heightmapPixelError = 8f; // Slightly higher for performance
        terrain.basemapDistance = 2000f; // Increase for better distant quality
        terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        
        // Texture streaming settings (Unity 2022.2+)
        #if UNITY_2022_2_OR_NEWER
        terrain.enableHeightmapRayTracing = false; // Disable if not using ray tracing
        #endif
        
        // Set terrain detail settings for performance
        terrain.detailObjectDistance = 150f;
        terrain.detailObjectDensity = 0.8f;
        terrain.treeDistance = 2000f;
        terrain.treeBillboardDistance = 100f;
        terrain.treeCrossFadeLength = 5f;
        terrain.treeMaximumFullLODCount = 50;
    }
    
    [ContextMenu("Generate New Golf Course")]
    public void RegenerateGolfCourse()
    {
        InitializeTerrain();
        InitializeSurfaceMapping();
        GenerateGolfCourse();
        Debug.Log("Golf course regenerated!");
    }
    
    [ContextMenu("Regenerate Heightmap Only")]
    public void RegenerateHeightmap()
    {
        GenerateHeightmap();
        Debug.Log("Heightmap regenerated!");
    }
    
    [ContextMenu("Regenerate Surface Map Only")]
    public void RegenerateSplatMap()
    {
        GenerateSplatMap();
        Debug.Log("Surface map regenerated!");
    }
    
    public SurfaceType GetSurfaceTypeAtPosition(Vector3 position)
    {
        if (terrainDetector == null)
            terrainDetector = new TerrainDetector();
            
        int textureIndex = terrainDetector.GetActiveTerrainTextureIdx(position);
        
        if (textureToSurfaceMap.ContainsKey(textureIndex))
            return textureToSurfaceMap[textureIndex];
            
        return SurfaceType.Grass;
    }
    
    public SurfaceProperties GetSurfacePropertiesAtPosition(Vector3 position)
    {
        SurfaceType surfaceType = GetSurfaceTypeAtPosition(position);
        return surfaceProperties[(int)surfaceType];
    }
    
    public float GetSlopeAtPosition(Vector3 position)
    {
        return terrain.terrainData.GetSteepness(
            (position.x - terrain.transform.position.x) / terrain.terrainData.size.x,
            (position.z - terrain.transform.position.z) / terrain.terrainData.size.z
        );
    }
    
    public void AddTeePosition(Vector3 position)
    {
        GameObject tee = new GameObject("Tee");
        tee.transform.position = position;
        tee.transform.SetParent(transform);
        
        System.Array.Resize(ref teePositions, teePositions.Length + 1);
        teePositions[teePositions.Length - 1] = tee.transform;
    }
    
    public void AddPinPosition(Vector3 position)
    {
        GameObject pin = new GameObject("Pin");
        pin.transform.position = position;
        pin.transform.SetParent(transform);
        
        System.Array.Resize(ref pinPositions, pinPositions.Length + 1);
        pinPositions[pinPositions.Length - 1] = pin.transform;
    }
    
    public Terrain TerrainComponent => terrain;
    public TerrainData TerrainDataAsset => terrainData;
    public int TeeCount => teePositions?.Length ?? 0;
    public int PinCount => pinPositions?.Length ?? 0;
    public int HazardCount => hazardAreas?.Length ?? 0;
    
    #if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        
        if (teePositions != null)
        {
            Gizmos.color = Color.blue;
            foreach (Transform tee in teePositions)
            {
                if (tee != null)
                {
                    Gizmos.DrawWireSphere(tee.position, 10f);
                    Gizmos.DrawCube(tee.position, Vector3.one * 2f);
                    UnityEditor.Handles.Label(tee.position + Vector3.up * 3f, "TEE");
                }
            }
        }
        
        if (pinPositions != null)
        {
            Gizmos.color = Color.red;
            foreach (Transform pin in pinPositions)
            {
                if (pin != null)
                {
                    Gizmos.DrawWireSphere(pin.position, 20f);
                    Gizmos.DrawCube(pin.position, Vector3.one * 2f);
                    UnityEditor.Handles.Label(pin.position + Vector3.up * 3f, "GREEN");
                }
            }
        }
        
        if (hazardAreas != null)
        {
            Gizmos.color = Color.yellow;
            foreach (Transform hazard in hazardAreas)
            {
                if (hazard != null)
                {
                    Gizmos.DrawWireSphere(hazard.position, 5f);
                    UnityEditor.Handles.Label(hazard.position + Vector3.up * 3f, "HAZARD");
                }
            }
        }
    }
    
    void OnValidate()
    {
        if (surfaceProperties == null || surfaceProperties.Length != 8)
        {
            surfaceProperties = new SurfaceProperties[8];
            for (int i = 0; i < 8; i++)
            {
                if (surfaceProperties[i] == null)
                {
                    surfaceProperties[i] = new SurfaceProperties
                    {
                        surfaceType = (SurfaceType)i,
                        ballRollResistance = 1f,
                        ballBounce = 0.5f,
                        ballSpin = 1f,
                        debugColor = Color.HSVToRGB(i / 8f, 0.7f, 1f)
                    };
                }
            }
        }
    }
    #endif
}

// Enhanced TerrainDetector
public class TerrainDetector
{
    private TerrainData terrainData;
    private int alphamapWidth;
    private int alphamapHeight;
    private float[,,] splatmapData;
    private int numTextures;
    private Terrain terrain;
    
    public TerrainDetector()
    {
        terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("No active terrain found!");
            return;
        }
        
        terrainData = terrain.terrainData;
        alphamapWidth = terrainData.alphamapWidth;
        alphamapHeight = terrainData.alphamapHeight;
        splatmapData = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);
        numTextures = splatmapData.Length / (alphamapWidth * alphamapHeight);
    }
    
    Vector3 ConvertToSplatMapCoordinate(Vector3 worldPosition)
    {
        Vector3 splatPosition = new Vector3();
        Vector3 terPosition = terrain.transform.position;
        splatPosition.x = ((worldPosition.x - terPosition.x) / terrainData.size.x) * alphamapWidth;
        splatPosition.z = ((worldPosition.z - terPosition.z) / terrainData.size.z) * alphamapHeight;
        return splatPosition;
    }
    
    public int GetActiveTerrainTextureIdx(Vector3 position)
    {
        Vector3 terrainCord = ConvertToSplatMapCoordinate(position);
        
        int x = Mathf.Clamp((int)terrainCord.x, 0, alphamapWidth - 1);
        int z = Mathf.Clamp((int)terrainCord.z, 0, alphamapHeight - 1);
        
        int activeTerrainIndex = 0;
        float largestOpacity = 0f;
        
        for (int i = 0; i < numTextures; i++)
        {
            float opacity = splatmapData[z, x, i];
            if (opacity > largestOpacity)
            {
                activeTerrainIndex = i;
                largestOpacity = opacity;
            }
        }
        
        return activeTerrainIndex;
    }
    
    public float[] GetTextureWeights(Vector3 position)
    {
        Vector3 terrainCord = ConvertToSplatMapCoordinate(position);
        int x = Mathf.Clamp((int)terrainCord.x, 0, alphamapWidth - 1);
        int z = Mathf.Clamp((int)terrainCord.z, 0, alphamapHeight - 1);
        
        float[] weights = new float[numTextures];
        for (int i = 0; i < numTextures; i++)
        {
            weights[i] = splatmapData[z, x, i];
        }
        
        return weights;
    }
    
    public float GetHeightAtPosition(Vector3 worldPosition)
    {
        return terrain.SampleHeight(worldPosition);
    }
    
    public Vector3 GetNormalAtPosition(Vector3 worldPosition)
    {
        Vector3 terrainLocalPos = worldPosition - terrain.transform.position;
        Vector2 normalizedPos = new Vector2(
            terrainLocalPos.x / terrainData.size.x,
            terrainLocalPos.z / terrainData.size.z
        );
        
        return terrainData.GetInterpolatedNormal(normalizedPos.x, normalizedPos.y);
    }
}