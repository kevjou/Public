using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class HoleConfiguration
{
    [Header("Hole Information")]
    public int holeNumber = 1;
    public int par = 4;
    [Range(50f, 600f)]
    public float distance = 300f;
    
    [Header("Layout")]
    public Vector3 teePosition;
    public Vector3 pinPosition;
    public AnimationCurve fairwayShape = AnimationCurve.Linear(0, 1, 1, 1);
    
    [Header("Hazards")]
    public Vector3[] sandTrapPositions;
    public Vector3[] waterHazardPositions;
    public Vector3[] treePositions;
    
    [Header("Difficulty")]
    [Range(1, 5)]
    public int difficulty = 3;
    public bool hasDoglegs = false;
    public float doglegAngle = 0f;
}

[System.Serializable]
public class CourseSettings
{
    [Header("Course Information")]
    public string courseName = "Procedural Golf Course";
    public int numberOfHoles = 18;
    public Texture2D courseLogoTexture;
    
    [Header("Terrain Settings")]
    [Range(500f, 2000f)]
    public float courseWidth = 1200f;
    [Range(500f, 2000f)]
    public float courseLength = 1500f;
    [Range(10f, 100f)]
    public float terrainHeight = 50f;
    
    [Header("Environment")]
    public GameObject[] treePrefabs;
    public GameObject[] bushPrefabs;
    public GameObject clubhousePrefab;
    public GameObject cartPrefab;
    public GameObject flagPrefab;
    
    [Header("Lighting")]
    public Material skyboxMaterial;
    public Gradient lightingGradient = new Gradient();
    public bool enableDynamicLighting = true;
}

public class GolfSceneGenerator : MonoBehaviour
{
    [Header("═══ GOLF SCENE GENERATOR ═══")]
    [SerializeField] private CourseSettings courseSettings = new CourseSettings();
    [SerializeField] private HoleConfiguration[] holes = new HoleConfiguration[18];
    
    [Header("═══ SYSTEM REFERENCES ═══")]
    [SerializeField] private GolfTerrainManager terrainManager;
    [SerializeField] private GolfWaterManager waterManager;
    [SerializeField] private GolfClubController clubController;
    [SerializeField] private GolfBallController ballController;
    [SerializeField] private GolfCameraController cameraController;
    
    [Header("═══ GENERATION OPTIONS ═══")]
    [SerializeField] private bool generateOnStart = false;
    [SerializeField] private bool generateRandomLayout = true;
    [SerializeField] private int randomSeed = 12345;
    
    [Header("═══ CAMERA SETUP ═══")]
    [SerializeField] private Camera playerCamera;
    [Tooltip("Height of player figure for camera framing")]
    [Range(1.5f, 2.2f)]
    [SerializeField] private float playerHeight = 1.8f;
    
    [Header("═══ UI ELEMENTS ═══")]
    [SerializeField] private Canvas gameUI;
    [SerializeField] private GameObject scoreCard;
    [SerializeField] private GameObject miniMap;
    
    private List<GameObject> generatedObjects = new List<GameObject>();
    private int currentHole = 1;
    private Vector3 courseCenter;
    
    void Start()
    {
        if (generateOnStart)
        {
            GenerateGolfScene();
        }
    }
    
    [ContextMenu("Generate Complete Golf Scene")]
    public void GenerateGolfScene()
    {
        Debug.Log("Starting golf scene generation...");
        
        // Clear existing scene
        ClearGeneratedObjects();
        
        // Set random seed for consistent generation
        if (randomSeed != 0)
            Random.InitState(randomSeed);
        
        // Step 1: Initialize core systems
        InitializeCoreSystems();
        
        // Step 2: Generate terrain
        GenerateTerrain();
        
        // Step 3: Generate holes layout
        GenerateHolesLayout();
        
        // Step 4: Add water hazards
        GenerateWaterHazards();
        
        // Step 5: Add environment details
        GenerateEnvironmentDetails();
        
        // Step 6: Setup player and camera
        SetupPlayerAndCamera();
        
        // Step 7: Setup lighting and atmosphere
        SetupLightingAndAtmosphere();
        
        // Step 8: Generate UI elements
        SetupGameUI();
        
        Debug.Log($"Golf scene generation complete! Generated {courseSettings.numberOfHoles} hole course.");
    }
    
    void InitializeCoreSystems()
    {
        courseCenter = new Vector3(courseSettings.courseWidth / 2f, 0, courseSettings.courseLength / 2f);
        
        // Create terrain manager if not assigned
        if (terrainManager == null)
        {
            GameObject terrainGO = new GameObject("Terrain Manager");
            terrainManager = terrainGO.AddComponent<GolfTerrainManager>();
            generatedObjects.Add(terrainGO);
        }
        
        // Create water manager if not assigned
        if (waterManager == null)
        {
            GameObject waterGO = new GameObject("Water Manager");
            waterManager = waterGO.AddComponent<GolfWaterManager>();
            generatedObjects.Add(waterGO);
        }
        
        // Create ball if not assigned
        if (ballController == null)
        {
            ballController = CreateGolfBall(Vector3.zero);
        }
        
        // Create club controller if not assigned
        if (clubController == null)
        {
            GameObject clubGO = new GameObject("Club Controller");
            clubController = clubGO.AddComponent<GolfClubController>();
            generatedObjects.Add(clubGO);
        }
    }
    
    void GenerateTerrain()
    {
        Debug.Log("Generating terrain...");
        
        // Configure terrain generation settings
        if (terrainManager != null)
        {
            terrainManager.RegenerateGolfCourse();
        }
    }
    
    void GenerateHolesLayout()
    {
        Debug.Log("Generating holes layout...");
        
        if (generateRandomLayout)
        {
            GenerateRandomHoleLayout();
        }
        else
        {
            GeneratePredesignedHoles();
        }
        
        // Create tee and pin objects for each hole
        for (int i = 0; i < holes.Length && i < courseSettings.numberOfHoles; i++)
        {
            CreateHoleObjects(holes[i]);
        }
    }
    
    void GenerateRandomHoleLayout()
    {
        float courseWidth = courseSettings.courseWidth;
        float courseLength = courseSettings.courseLength;
        
        // Create a rough course path
        Vector3[] coursePath = GenerateCoursePath(courseSettings.numberOfHoles);
        
        for (int i = 0; i < courseSettings.numberOfHoles && i < holes.Length; i++)
        {
            holes[i] = new HoleConfiguration();
            holes[i].holeNumber = i + 1;
            
            // Determine par based on distance
            float distance = Random.Range(100f, 500f);
            holes[i].distance = distance;
            
            if (distance < 150f) holes[i].par = 3;
            else if (distance > 400f) holes[i].par = 5;
            else holes[i].par = 4;
            
            // Position along course path
            Vector3 pathPosition = coursePath[i];
            Vector3 holeDirection = (i < coursePath.Length - 1) ? (coursePath[i + 1] - pathPosition).normalized : Vector3.forward;
            
            holes[i].teePosition = pathPosition;
            holes[i].pinPosition = pathPosition + holeDirection * distance;
            
            // Add some randomness to pin position
            holes[i].pinPosition += new Vector3(Random.Range(-20f, 20f), 0, Random.Range(-10f, 10f));
            
            // Generate hazards
            GenerateHoleHazards(holes[i]);
        }
    }
    
    Vector3[] GenerateCoursePath(int numberOfHoles)
    {
        Vector3[] path = new Vector3[numberOfHoles];
        float coursePerimeter = (courseSettings.courseWidth + courseSettings.courseLength) * 2;
        float segmentLength = coursePerimeter / numberOfHoles;
        
        for (int i = 0; i < numberOfHoles; i++)
        {
            float progress = (float)i / numberOfHoles;
            
            // Create a roughly rectangular course layout
            if (progress < 0.25f) // Bottom edge
            {
                float x = progress * 4 * courseSettings.courseWidth;
                path[i] = new Vector3(x, 0, 50f);
            }
            else if (progress < 0.5f) // Right edge
            {
                float z = (progress - 0.25f) * 4 * courseSettings.courseLength;
                path[i] = new Vector3(courseSettings.courseWidth - 50f, 0, z + 50f);
            }
            else if (progress < 0.75f) // Top edge
            {
                float x = courseSettings.courseWidth - (progress - 0.5f) * 4 * courseSettings.courseWidth;
                path[i] = new Vector3(x, 0, courseSettings.courseLength - 50f);
            }
            else // Left edge
            {
                float z = courseSettings.courseLength - (progress - 0.75f) * 4 * courseSettings.courseLength;
                path[i] = new Vector3(50f, 0, z);
            }
        }
        
        return path;
    }
    
    void GenerateHoleHazards(HoleConfiguration hole)
    {
        List<Vector3> sandTraps = new List<Vector3>();
        List<Vector3> waterHazards = new List<Vector3>();
        
        Vector3 direction = (hole.pinPosition - hole.teePosition).normalized;
        float holeLength = Vector3.Distance(hole.teePosition, hole.pinPosition);
        
        // Add sand traps (1-3 per hole)
        int sandTrapCount = Random.Range(1, 4);
        for (int i = 0; i < sandTrapCount; i++)
        {
            float distanceAlongHole = Random.Range(holeLength * 0.3f, holeLength * 0.8f);
            Vector3 trapPosition = hole.teePosition + direction * distanceAlongHole;
            trapPosition += new Vector3(Random.Range(-30f, 30f), 0, Random.Range(-15f, 15f));
            sandTraps.Add(trapPosition);
        }
        
        // Add water hazards (0-2 per hole, more likely on longer holes)
        if (hole.par >= 4 && Random.Range(0f, 1f) < 0.6f)
        {
            int waterHazardCount = Random.Range(0, 3);
            for (int i = 0; i < waterHazardCount; i++)
            {
                float distanceAlongHole = Random.Range(holeLength * 0.4f, holeLength * 0.9f);
                Vector3 hazardPosition = hole.teePosition + direction * distanceAlongHole;
                hazardPosition += new Vector3(Random.Range(-40f, 40f), 0, Random.Range(-20f, 20f));
                waterHazards.Add(hazardPosition);
            }
        }
        
        hole.sandTrapPositions = sandTraps.ToArray();
        hole.waterHazardPositions = waterHazards.ToArray();
    }
    
    void GeneratePredesignedHoles()
    {
        // Use the holes array as configured in the inspector
        Debug.Log("Using pre-designed hole layout");
    }
    
    void CreateHoleObjects(HoleConfiguration hole)
    {
        // Create tee markers
        GameObject teeMarker = CreateTeeMarker(hole.teePosition, hole.holeNumber);
        generatedObjects.Add(teeMarker);
        
        // Create pin/flag
        GameObject pin = CreatePin(hole.pinPosition, hole.holeNumber);
        generatedObjects.Add(pin);
        
        // Add to terrain manager for surface generation
        if (terrainManager != null)
        {
            terrainManager.AddTeePosition(hole.teePosition);
            terrainManager.AddPinPosition(hole.pinPosition);
        }
        
        // Create sand traps
        if (hole.sandTrapPositions != null)
        {
            foreach (Vector3 pos in hole.sandTrapPositions)
            {
                GameObject sandTrap = CreateSandTrap(pos);
                generatedObjects.Add(sandTrap);
            }
        }
    }
    
    GameObject CreateTeeMarker(Vector3 position, int holeNumber)
    {
        GameObject tee = new GameObject($"Tee {holeNumber}");
        tee.transform.position = position;
        
        // Create tee box visual
        GameObject teeBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        teeBox.transform.SetParent(tee.transform);
        teeBox.transform.localPosition = Vector3.zero;
        teeBox.transform.localScale = new Vector3(6f, 0.1f, 4f);
        teeBox.name = "Tee Box";
        
        // Set material color
        Renderer renderer = teeBox.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.4f, 0.7f, 0.3f); // Tee green
        }
        
        // Add hole number sign
        GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.transform.SetParent(tee.transform);
        sign.transform.localPosition = new Vector3(0, 1.5f, -3f);
        sign.transform.localScale = new Vector3(2f, 2f, 0.1f);
        sign.name = $"Hole {holeNumber} Sign";
        
        return tee;
    }
    
    GameObject CreatePin(Vector3 position, int holeNumber)
    {
        GameObject pin = new GameObject($"Pin {holeNumber}");
        pin.transform.position = position;
        
        // Create the hole (cup)
        GameObject cup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cup.transform.SetParent(pin.transform);
        cup.transform.localPosition = new Vector3(0, -0.05f, 0);
        cup.transform.localScale = new Vector3(0.2f, 0.1f, 0.2f);
        cup.name = "Cup";
        
        Renderer cupRenderer = cup.GetComponent<Renderer>();
        if (cupRenderer != null)
        {
            cupRenderer.material.color = Color.black;
        }
        
        // Create flag pole
        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.transform.SetParent(pin.transform);
        pole.transform.localPosition = new Vector3(0, 1f, 0);
        pole.transform.localScale = new Vector3(0.02f, 2f, 0.02f);
        pole.name = "Flag Pole";
        
        // Create flag
        GameObject flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
        flag.transform.SetParent(pin.transform);
        flag.transform.localPosition = new Vector3(0.3f, 1.5f, 0);
        flag.transform.localScale = new Vector3(0.6f, 0.4f, 0.02f);
        flag.name = "Flag";
        
        Renderer flagRenderer = flag.GetComponent<Renderer>();
        if (flagRenderer != null)
        {
            flagRenderer.material.color = Color.red;
        }
        
        return pin;
    }
    
    GameObject CreateSandTrap(Vector3 position)
    {
        GameObject sandTrap = new GameObject("Sand Trap");
        sandTrap.transform.position = position;
        
        // Create sand trap mesh
        GameObject trap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trap.transform.SetParent(sandTrap.transform);
        trap.transform.localPosition = Vector3.zero;
        trap.transform.localScale = new Vector3(8f, 0.2f, 6f);
        trap.name = "Sand";
        
        Renderer renderer = trap.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.9f, 0.8f, 0.6f); // Sand color
        }
        
        return sandTrap;
    }
    
    void GenerateWaterHazards()
    {
        Debug.Log("Generating water hazards...");
        
        foreach (HoleConfiguration hole in holes)
        {
            if (hole.waterHazardPositions != null)
            {
                foreach (Vector3 pos in hole.waterHazardPositions)
                {
                    // Create water zone
                    WaterZone waterZone = waterManager.CreateWaterZone(
                        pos, 
                        new Vector3(15f, 2f, 10f), 
                        WaterType.WaterHazard
                    );
                    
                    generatedObjects.Add(waterZone.gameObject);
                }
            }
        }
    }
    
    void GenerateEnvironmentDetails()
    {
        Debug.Log("Generating environment details...");
        
        // Add trees around the course
        GenerateTrees();
        
        // Add clubhouse
        if (courseSettings.clubhousePrefab != null)
        {
            Vector3 clubhousePos = new Vector3(50f, 0, 50f);
            GameObject clubhouse = Instantiate(courseSettings.clubhousePrefab, clubhousePos, Quaternion.identity);
            clubhouse.name = "Clubhouse";
            generatedObjects.Add(clubhouse);
        }
    }
    
    void GenerateTrees()
    {
        if (courseSettings.treePrefabs == null || courseSettings.treePrefabs.Length == 0) return;
        
        int treeCount = Random.Range(50, 150);
        
        for (int i = 0; i < treeCount; i++)
        {
            Vector3 treePos = new Vector3(
                Random.Range(0, courseSettings.courseWidth),
                0,
                Random.Range(0, courseSettings.courseLength)
            );
            
            // Make sure trees don't spawn too close to holes
            bool tooCloseToHole = false;
            foreach (HoleConfiguration hole in holes)
            {
                if (Vector3.Distance(treePos, hole.teePosition) < 30f ||
                    Vector3.Distance(treePos, hole.pinPosition) < 30f)
                {
                    tooCloseToHole = true;
                    break;
                }
            }
            
            if (!tooCloseToHole)
            {
                GameObject treePrefab = courseSettings.treePrefabs[Random.Range(0, courseSettings.treePrefabs.Length)];
                GameObject tree = Instantiate(treePrefab, treePos, Quaternion.Euler(0, Random.Range(0, 360), 0));
                tree.name = $"Tree {i}";
                generatedObjects.Add(tree);
            }
        }
    }
    
    GolfBallController CreateGolfBall(Vector3 position)
    {
        GameObject ballGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ballGO.name = "Golf Ball";
        ballGO.transform.position = position;
        ballGO.transform.localScale = Vector3.one * 0.042f; // Standard golf ball size
        
        // Add required components
        Rigidbody rb = ballGO.GetComponent<Rigidbody>();
        rb.mass = 0.045f; // Standard golf ball mass
        
        SphereCollider collider = ballGO.GetComponent<SphereCollider>();
        collider.radius = 0.5f; // Radius in local space
        
        GolfBallController ballController = ballGO.AddComponent<GolfBallController>();
        
        generatedObjects.Add(ballGO);
        return ballController;
    }
    
    void SetupPlayerAndCamera()
    {
        Debug.Log("Setting up camera system...");
        
        // Setup camera
        if (playerCamera == null)
        {
            GameObject cameraGO = new GameObject("Golf Camera");
            playerCamera = cameraGO.AddComponent<Camera>();
            generatedObjects.Add(cameraGO);
        }
        
        // Add camera controller
        if (cameraController == null)
        {
            cameraController = playerCamera.gameObject.AddComponent<GolfCameraController>();
        }
        
        // Configure camera controller
        if (cameraController != null && ballController != null)
        {
            Transform firstPin = null;
            if (holes.Length > 0)
            {
                // Find the first pin GameObject
                GameObject pinObject = GameObject.Find($"Pin {holes[0].holeNumber}");
                if (pinObject != null)
                {
                    firstPin = pinObject.transform;
                }
            }
            
            // Set up camera controller references
            cameraController.SetBallAndPin(ballController.transform, firstPin);
            
            // Position for first tee shot
            if (holes.Length > 0)
            {
                cameraController.PositionForTeeShot(holes[0].teePosition, holes[0].pinPosition);
            }
        }
        
        // Setup club controller reference
        if (clubController != null && ballController != null)
        {
            clubController.ballPosition = ballController.transform;
            clubController.playerCamera = playerCamera;
        }
        
        // Position ball at first tee
        if (ballController != null && holes.Length > 0)
        {
            ballController.transform.position = holes[0].teePosition + Vector3.up * 0.1f;
        }
        
        Debug.Log("Camera system setup complete");
    }
    
    void SetupLightingAndAtmosphere()
    {
        Debug.Log("Setting up lighting and atmosphere...");
        
        // Setup skybox
        if (courseSettings.skyboxMaterial != null)
        {
            RenderSettings.skybox = courseSettings.skyboxMaterial;
        }
        
        // Setup directional light (sun)
        Light sunLight = FindObjectOfType<Light>();
        if (sunLight == null)
        {
            GameObject lightGO = new GameObject("Sun Light");
            sunLight = lightGO.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            generatedObjects.Add(lightGO);
        }
        
        sunLight.transform.rotation = Quaternion.Euler(45f, 45f, 0);
        sunLight.intensity = 1.2f;
        sunLight.color = new Color(1f, 0.95f, 0.8f);
        sunLight.shadows = LightShadows.Soft;
    }
    
    void SetupGameUI()
    {
        Debug.Log("Setting up game UI...");
        
        if (gameUI == null)
        {
            GameObject uiGO = new GameObject("Game UI");
            gameUI = uiGO.AddComponent<Canvas>();
            gameUI.renderMode = RenderMode.ScreenSpaceOverlay;
            generatedObjects.Add(uiGO);
        }
        
        // Create basic UI elements here
        // Score card, mini-map, etc.
    }
    
    void ClearGeneratedObjects()
    {
        foreach (GameObject obj in generatedObjects)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
        generatedObjects.Clear();
    }
    
    [ContextMenu("Move to Next Hole")]
    public void MoveToNextHole()
    {
        currentHole++;
        if (currentHole > courseSettings.numberOfHoles)
        {
            currentHole = 1; // Loop back to first hole
        }
        
        HoleConfiguration nextHole = holes[currentHole - 1];
        Vector3 nextTeePos = nextHole.teePosition;
        Vector3 nextPinPos = nextHole.pinPosition;
        
        // Move ball to next tee
        if (ballController != null)
        {
            ballController.ResetBall(nextTeePos + Vector3.up * 0.1f);
        }
        
        // Update camera for new hole
        if (cameraController != null)
        {
            // Find the pin for this hole
            Transform pinTransform = null;
            GameObject pinObject = GameObject.Find($"Pin {nextHole.holeNumber}");
            if (pinObject != null)
            {
                pinTransform = pinObject.transform;
            }
            
            // Update camera controller references
            cameraController.SetBallAndPin(ballController.transform, pinTransform);
            
            // Position camera for tee shot
            cameraController.PositionForTeeShot(nextTeePos, nextPinPos);
        }
        
        Debug.Log($"Moved to hole {currentHole} - Par {nextHole.par}, {nextHole.distance:F0}m");
    }
    
    [ContextMenu("Reset to First Hole")]
    public void ResetToFirstHole()
    {
        currentHole = 1;
        MoveToNextHole();
    }
    
    void OnDrawGizmos()
    {
        // Draw course boundaries
        Gizmos.color = Color.yellow;
        Vector3 center = new Vector3(courseSettings.courseWidth / 2f, 0, courseSettings.courseLength / 2f);
        Vector3 size = new Vector3(courseSettings.courseWidth, 1f, courseSettings.courseLength);
        Gizmos.DrawWireCube(center, size);
        
        // Draw holes
        for (int i = 0; i < holes.Length && i < courseSettings.numberOfHoles; i++)
        {
            HoleConfiguration hole = holes[i];
            
            // Draw tee
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(hole.teePosition, 2f);
            
            // Draw pin
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hole.pinPosition, 2f);
            
            // Draw line between tee and pin
            Gizmos.color = Color.green;
            Gizmos.DrawLine(hole.teePosition, hole.pinPosition);
            
            // Draw hazards
            if (hole.sandTrapPositions != null)
            {
                Gizmos.color = Color.yellow;
                foreach (Vector3 pos in hole.sandTrapPositions)
                {
                    Gizmos.DrawWireSphere(pos, 3f);
                }
            }
            
            if (hole.waterHazardPositions != null)
            {
                Gizmos.color = Color.cyan;
                foreach (Vector3 pos in hole.waterHazardPositions)
                {
                    Gizmos.DrawWireCube(pos, new Vector3(15f, 2f, 10f));
                }
            }
        }
    }
}