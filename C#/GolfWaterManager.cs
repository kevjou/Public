using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public enum WaterType
{
    Pond,           // Static water hazard
    Stream,         // Flowing water
    Fountain,       // Decorative fountain
    WaterHazard,    // Competition water hazard
    Creek          // Small flowing water
}

[System.Serializable]
public class WaterZoneSettings
{
    [Header("Water Type")]
    public WaterType waterType = WaterType.Pond;
    
    [Header("Physics Settings")]
    [Tooltip("Water level height")]
    public float waterLevel = 0f;
    
    [Tooltip("Ball enters water penalty")]
    public bool isPenaltyArea = true;
    
    [Tooltip("Ball floating buoyancy")]
    [Range(0f, 10f)]
    public float buoyancy = 2f;
    
    [Tooltip("Water drag on ball")]
    [Range(0f, 5f)]
    public float waterDrag = 3f;
    
    [Header("Visual Effects")]
    [Tooltip("Splash particles when ball enters")]
    public ParticleSystem splashEffect;
    
    [Tooltip("Ripple effect prefab")]
    public GameObject ripplePrefab;
    
    [Tooltip("Ambient water sound")]
    public AudioClip waterAmbientSound;
    
    [Tooltip("Splash sound")]
    public AudioClip splashSound;
    
    [Header("Flow Settings (for streams)")]
    [Tooltip("Water flow direction")]
    public Vector3 flowDirection = Vector3.forward;
    
    [Tooltip("Flow speed")]
    [Range(0f, 5f)]
    public float flowSpeed = 1f;
}

public class GolfWaterManager : MonoBehaviour
{
    [Header("═══ WATER SYSTEM SETUP ═══")]
    [SerializeField] private WaterZoneSettings defaultWaterSettings = new WaterZoneSettings();
    
    [Header("═══ WATER ZONES ═══")]
    [SerializeField] private List<WaterZone> waterZones = new List<WaterZone>();
    
    [Header("═══ WATER MATERIALS ═══")]
    [SerializeField] private Material pondWaterMaterial;
    [SerializeField] private Material streamWaterMaterial;
    [SerializeField] private Material fountainWaterMaterial;
    
    [Header("═══ GLOBAL WATER SETTINGS ═══")]
    [Range(0f, 1f)]
    [SerializeField] private float globalWaterTransparency = 0.7f;
    
    [Range(0f, 2f)]
    [SerializeField] private float waveStrength = 0.5f;
    
    [Range(0f, 5f)]
    [SerializeField] private float waveSpeed = 1f;
    
    [SerializeField] private Color waterTint = new Color(0.2f, 0.6f, 0.8f, 0.7f);
    
    [Header("═══ BALL INTERACTION ═══")]
    [SerializeField] private LayerMask ballLayer = 1;
    [SerializeField] private bool enableWaterPhysics = true;
    [SerializeField] private bool enablePenaltySystem = true;
    
    private List<Rigidbody> ballsInWater = new List<Rigidbody>();
    private GolfTerrainManager terrainManager;
    
    void Start()
    {
        terrainManager = FindFirstObjectByType<GolfTerrainManager>();
        InitializeWaterZones();
        SetupWaterMaterials();
    }
    
    void InitializeWaterZones()
    {
        // Auto-find existing water zones in scene
        WaterZone[] foundZones = FindObjectsByType<WaterZone>(FindObjectsSortMode.None);
        
        foreach (WaterZone zone in foundZones)
        {
            if (!waterZones.Contains(zone))
            {
                waterZones.Add(zone);
                zone.Initialize(this);
            }
        }
        
        Debug.Log($"Initialized {waterZones.Count} water zones");
    }
    
    void SetupWaterMaterials()
    {
        // Create default water materials if not assigned
        if (pondWaterMaterial == null)
            pondWaterMaterial = CreateWaterMaterial("Pond Water", WaterType.Pond);
            
        if (streamWaterMaterial == null)
            streamWaterMaterial = CreateWaterMaterial("Stream Water", WaterType.Stream);
            
        if (fountainWaterMaterial == null)
            fountainWaterMaterial = CreateWaterMaterial("Fountain Water", WaterType.Fountain);
    }
    
    Material CreateWaterMaterial(string name, WaterType type)
    {
        // Create a basic water material (can be enhanced with custom shaders)
        Material waterMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        waterMat.name = name;
        
        // Basic water properties
        waterMat.SetFloat("_Metallic", 0f);
        waterMat.SetFloat("_Smoothness", 0.9f);
        waterMat.SetColor("_BaseColor", waterTint);
        
        // Make it transparent
        waterMat.SetFloat("_Surface", 1); // Transparent
        waterMat.SetFloat("_Blend", 0); // Alpha blend
        
        // Enable transparency
        waterMat.renderQueue = 3000;
        waterMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        waterMat.EnableKeyword("_ALPHABLEND_ON");
        
        return waterMat;
    }
    
    public WaterZone CreateWaterZone(Vector3 position, Vector3 size, WaterType type = WaterType.Pond)
    {
        GameObject waterObject = new GameObject($"Water Zone - {type}");
        waterObject.transform.position = position;
        waterObject.transform.SetParent(transform);
        
        WaterZone zone = waterObject.AddComponent<WaterZone>();
        zone.Setup(size, type, defaultWaterSettings);
        zone.Initialize(this);
        
        waterZones.Add(zone);
        
        Debug.Log($"Created {type} water zone at {position}");
        return zone;
    }
    
    public void OnBallEnterWater(Rigidbody ballRb, WaterZone waterZone)
    {
        if (!ballsInWater.Contains(ballRb))
        {
            ballsInWater.Add(ballRb);
            
            if (enableWaterPhysics)
            {
                ApplyWaterPhysics(ballRb, waterZone);
            }
            
            // Trigger penalty if enabled
            if (enablePenaltySystem && waterZone.Settings.isPenaltyArea)
            {
                TriggerWaterPenalty(ballRb, waterZone);
            }
            
            // Visual/audio effects
            PlayWaterEffects(ballRb.transform.position, waterZone);
            
            Debug.Log($"Ball entered {waterZone.Settings.waterType} water zone");
        }
    }
    
    public void OnBallExitWater(Rigidbody ballRb, WaterZone waterZone)
    {
        if (ballsInWater.Contains(ballRb))
        {
            ballsInWater.Remove(ballRb);
            RemoveWaterPhysics(ballRb);
            
            Debug.Log($"Ball exited {waterZone.Settings.waterType} water zone");
        }
    }
    
    void ApplyWaterPhysics(Rigidbody ballRb, WaterZone waterZone)
    {
        WaterZoneSettings settings = waterZone.Settings;
        
        // Add water physics component
        WaterPhysics waterPhysics = ballRb.gameObject.GetComponent<WaterPhysics>();
        if (waterPhysics == null)
        {
            waterPhysics = ballRb.gameObject.AddComponent<WaterPhysics>();
        }
        
        waterPhysics.Setup(settings.buoyancy, settings.waterDrag, settings.flowDirection * settings.flowSpeed);
    }
    
    void RemoveWaterPhysics(Rigidbody ballRb)
    {
        WaterPhysics waterPhysics = ballRb.gameObject.GetComponent<WaterPhysics>();
        if (waterPhysics != null)
        {
            Destroy(waterPhysics);
        }
    }
    
    void TriggerWaterPenalty(Rigidbody ballRb, WaterZone waterZone)
    {
        // This would integrate with your golf scoring system
        Debug.Log("WATER PENALTY! +1 stroke");
        
        // You could add penalty UI, sound effects, etc.
        // For now, we'll just log it
    }
    
    void PlayWaterEffects(Vector3 position, WaterZone waterZone)
    {
        WaterZoneSettings settings = waterZone.Settings;
        
        // Splash particles
        if (settings.splashEffect != null)
        {
            ParticleSystem splash = Instantiate(settings.splashEffect, position, Quaternion.identity);
            splash.Play();
            Destroy(splash.gameObject, 3f);
        }
        
        // Ripple effect
        if (settings.ripplePrefab != null)
        {
            GameObject ripple = Instantiate(settings.ripplePrefab, position, Quaternion.identity);
            Destroy(ripple, 5f);
        }
        
        // Splash sound
        if (settings.splashSound != null)
        {
            AudioSource.PlayClipAtPoint(settings.splashSound, position);
        }
    }
    
    void Update()
    {
        // Update water physics for balls in water
        foreach (Rigidbody ballRb in ballsInWater)
        {
            if (ballRb != null)
            {
                UpdateBallInWater(ballRb);
            }
        }
        
        // Update water material properties
        UpdateWaterMaterials();
    }
    
    void UpdateBallInWater(Rigidbody ballRb)
    {
        // Apply continuous water effects
        Vector3 waterForce = Vector3.up * 0.5f; // Gentle upward buoyancy
        ballRb.AddForce(waterForce, ForceMode.Force);
    }
    
    void UpdateWaterMaterials()
    {
        // Animate water materials (waves, etc.)
        float time = Time.time;
        
        if (pondWaterMaterial != null)
        {
            // Simple water animation
            float wave = Mathf.Sin(time * waveSpeed) * waveStrength;
            pondWaterMaterial.SetFloat("_BumpScale", 1f + wave * 0.1f);
        }
    }
    
    public Material GetWaterMaterial(WaterType type)
    {
        switch (type)
        {
            case WaterType.Pond:
            case WaterType.WaterHazard:
                return pondWaterMaterial;
            case WaterType.Stream:
            case WaterType.Creek:
                return streamWaterMaterial;
            case WaterType.Fountain:
                return fountainWaterMaterial;
            default:
                return pondWaterMaterial;
        }
    }
    
    [ContextMenu("Create Test Water Zones")]
    public void CreateTestWaterZones()
    {
        // Create a pond
        CreateWaterZone(new Vector3(50, 0, 50), new Vector3(20, 2, 15), WaterType.Pond);
        
        // Create a stream
        CreateWaterZone(new Vector3(100, 0, 30), new Vector3(5, 1, 50), WaterType.Stream);
        
        Debug.Log("Created test water zones");
    }
    
    void OnDrawGizmos()
    {
        // Draw water zones in scene view
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.3f);
        
        foreach (WaterZone zone in waterZones)
        {
            if (zone != null)
            {
                Gizmos.matrix = zone.transform.localToWorldMatrix;
                Gizmos.DrawCube(Vector3.zero, Vector3.one);
                
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            }
        }
    }
}

// Individual water zone component
public class WaterZone : MonoBehaviour
{
    [SerializeField] private WaterZoneSettings settings = new WaterZoneSettings();
    [SerializeField] private BoxCollider waterCollider;
    [SerializeField] private MeshRenderer waterRenderer;
    [SerializeField] private AudioSource waterAudioSource;
    
    private GolfWaterManager waterManager;
    
    public WaterZoneSettings Settings => settings;
    
    public void Setup(Vector3 size, WaterType type, WaterZoneSettings defaultSettings)
    {
        settings = new WaterZoneSettings();
        settings.waterType = type;
        settings.buoyancy = defaultSettings.buoyancy;
        settings.waterDrag = defaultSettings.waterDrag;
        settings.isPenaltyArea = (type == WaterType.WaterHazard || type == WaterType.Pond);
        
        CreateWaterVisuals(size);
        CreateWaterCollider(size);
        SetupAudio();
    }
    
    public void Initialize(GolfWaterManager manager)
    {
        waterManager = manager;
    }
    
    void CreateWaterVisuals(Vector3 size)
    {
        // Create water mesh
        GameObject waterMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        waterMesh.transform.SetParent(transform);
        waterMesh.transform.localPosition = Vector3.zero;
        waterMesh.transform.localScale = size;
        waterMesh.name = "Water Mesh";
        
        // Remove the default collider (we'll add our own)
        Destroy(waterMesh.GetComponent<BoxCollider>());
        
        waterRenderer = waterMesh.GetComponent<MeshRenderer>();
    }
    
    void CreateWaterCollider(Vector3 size)
    {
        waterCollider = gameObject.AddComponent<BoxCollider>();
        waterCollider.size = size;
        waterCollider.isTrigger = true; // Important: must be trigger for physics detection
    }
    
    void SetupAudio()
    {
        if (settings.waterAmbientSound != null)
        {
            waterAudioSource = gameObject.AddComponent<AudioSource>();
            waterAudioSource.clip = settings.waterAmbientSound;
            waterAudioSource.loop = true;
            waterAudioSource.volume = 0.3f;
            waterAudioSource.spatialBlend = 1f; // 3D audio
            waterAudioSource.Play();
        }
    }
    
    void Start()
    {
        // Apply appropriate water material
        if (waterManager != null && waterRenderer != null)
        {
            waterRenderer.material = waterManager.GetWaterMaterial(settings.waterType);
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (IsGolfBall(other))
        {
            Rigidbody ballRb = other.GetComponent<Rigidbody>();
            if (ballRb != null && waterManager != null)
            {
                waterManager.OnBallEnterWater(ballRb, this);
            }
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (IsGolfBall(other))
        {
            Rigidbody ballRb = other.GetComponent<Rigidbody>();
            if (ballRb != null && waterManager != null)
            {
                waterManager.OnBallExitWater(ballRb, this);
            }
        }
    }
    
    bool IsGolfBall(Collider collider)
    {
        return collider.GetComponent<GolfBallController>() != null;
    }
}

// Component that handles water physics for the golf ball
public class WaterPhysics : MonoBehaviour
{
    private float buoyancy = 2f;
    private float drag = 3f;
    private Vector3 flow = Vector3.zero;
    private Rigidbody rb;
    
    public void Setup(float buoyancyForce, float dragForce, Vector3 flowForce)
    {
        buoyancy = buoyancyForce;
        drag = dragForce;
        flow = flowForce;
        rb = GetComponent<Rigidbody>();
    }
    
    void FixedUpdate()
    {
        if (rb == null) return;
        
        // Apply buoyancy (upward force)
        Vector3 buoyancyForce = Vector3.up * buoyancy;
        rb.AddForce(buoyancyForce, ForceMode.Force);
        
        // Apply water drag
        Vector3 dragForce = -rb.linearVelocity * drag;
        rb.AddForce(dragForce, ForceMode.Force);
        
        // Apply flow/current
        if (flow.magnitude > 0)
        {
            rb.AddForce(flow, ForceMode.Force);
        }
        
        // Limit vertical velocity (simulate water resistance)
        Vector3 velocity = rb.linearVelocity;
        velocity.y = Mathf.Clamp(velocity.y, -5f, 2f);
        rb.linearVelocity = velocity;
    }
}