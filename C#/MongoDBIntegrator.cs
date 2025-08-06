using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Main component for integrating MongoDB with the Golf Simulation
/// This class handles the connections between all components and provides UI hooks
/// </summary>
public class GolfMongoDBIntegrator : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private MongoDBClient mongoClient;
    [SerializeField] private ShotDataManager shotDataManager;
    [SerializeField] private OutdoorSimulationManager simulationManager;
    [SerializeField] private GolfTerrainManager terrainManager;
    [SerializeField] private GolfBallController ballController;
    [SerializeField] private GolfClubController clubController;
    [SerializeField] private GolfCameraController cameraController;
    
    [Header("Database Settings")]
    [SerializeField] private string serverUrl = "http://localhost:3000/api";
    [SerializeField] private string apiKey = "";
    [SerializeField] private bool autoSyncOnStart = true;
    [SerializeField] private float syncIntervalMinutes = 10f;
    
    [Header("UI Elements")]
    [SerializeField] private Button syncButton;
    [SerializeField] private Button exportButton;
    [SerializeField] private TMP_InputField playerIdInput;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI shotStatsText;
    [SerializeField] private GameObject syncProgressPanel;
    
    [Header("Options")]
    [SerializeField] private bool autoRecordShots = true;
    [SerializeField] private bool enableGolfClubIntegration = true;
    [SerializeField] private bool enableDebugLogging = true;
    
    // Connection state tracking
    private bool isConnected = false;
    private bool isSyncing = false;
    private string currentPlayerId = "";
    private int totalShotCount = 0;
    
    void Start()
    {
        // Find required components if not assigned
        FindRequiredComponents();
        
        // Setup UI elements
        SetupUIElements();
        
        // Configure MongoDB client
        ConfigureMongoDBClient();
        
        // Connect to GolfBallController to monitor shots
        if (autoRecordShots && ballController != null)
        {
            ConnectShotRecording();
        }
        
        // Auto test connection
        StartCoroutine(TestConnectionOnStart());
    }
    
    private void FindRequiredComponents()
    {
        if (mongoClient == null)
            mongoClient = FindObjectOfType<MongoDBClient>();
            
        if (shotDataManager == null)
            shotDataManager = FindObjectOfType<ShotDataManager>();
            
        if (simulationManager == null)
            simulationManager = FindObjectOfType<OutdoorSimulationManager>();
            
        if (terrainManager == null)
            terrainManager = FindObjectOfType<GolfTerrainManager>();
            
        if (ballController == null)
            ballController = FindObjectOfType<GolfBallController>();
            
        if (clubController == null)
            clubController = FindObjectOfType<GolfClubController>();
            
        if (cameraController == null)
            cameraController = FindObjectOfType<GolfCameraController>();
            
        // Create components if missing
        if (mongoClient == null)
        {
            GameObject obj = new GameObject("MongoDB Client");
            obj.transform.SetParent(transform);
            mongoClient = obj.AddComponent<MongoDBClient>();
            LogMessage("Created MongoDB Client");
        }
        
        if (shotDataManager == null)
        {
            GameObject obj = new GameObject("Shot Data Manager");
            obj.transform.SetParent(transform);
            shotDataManager = obj.AddComponent<ShotDataManager>();
            LogMessage("Created Shot Data Manager");
        }
    }
    
    private void SetupUIElements()
    {
        // Setup sync button
        if (syncButton != null)
        {
            syncButton.onClick.AddListener(SyncWithServer);
        }
        
        // Setup export button
        if (exportButton != null)
        {
            exportButton.onClick.AddListener(ExportData);
        }
        
        // Setup player ID input
        if (playerIdInput != null)
        {
            currentPlayerId = PlayerPrefs.GetString("PlayerId", "");
            playerIdInput.text = currentPlayerId;
            playerIdInput.onEndEdit.AddListener(OnPlayerIdChanged);
        }
        
        // Initialize status text
        if (statusText != null)
        {
            statusText.text = "Initializing...";
        }
        
        // Initialize shot stats text
        if (shotStatsText != null)
        {
            shotStatsText.text = "No shots recorded";
        }
        
        // Hide sync progress panel initially
        if (syncProgressPanel != null)
        {
            syncProgressPanel.SetActive(false);
        }
    }
    
    private void ConfigureMongoDBClient()
    {
        if (mongoClient != null)
        {
            mongoClient.SetApiBaseUrl(serverUrl);
            mongoClient.SetApiKey(apiKey);
            
            // Connect references
            if (shotDataManager != null)
            {
                shotDataManager.mongoClient = mongoClient;
                mongoClient.shotDataManager = shotDataManager;
            }
            
            if (simulationManager != null)
            {
                mongoClient.simulationManager = simulationManager;
            }
            
            LogMessage("MongoDB client configured");
        }
    }
    
    private void ConnectShotRecording()
    {
        // Setup ball event monitoring
        ballController.OnPositionChangedEvent += MonitorBallPosition;
        
        // Setup club controller event monitoring
        if (enableGolfClubIntegration && clubController != null)
        {
            // Ideally your GolfClubController would have an event for shot execution
            // For this implementation, we'll check in Update
            LogMessage("Connected to GolfBallController for shot monitoring");
        }
    }
    
    private IEnumerator TestConnectionOnStart()
    {
        yield return new WaitForSeconds(1f); // Small delay to let other systems initialize
        
        if (syncProgressPanel != null)
            syncProgressPanel.SetActive(true);
            
        UpdateStatusText("Testing connection to database...");
        
        yield return StartCoroutine(mongoClient.TestConnection((success) => {
            isConnected = success;
            
            if (success)
            {
                UpdateStatusText("Connected to database successfully");
                
                // Auto sync if enabled
                if (autoSyncOnStart)
                {
                    SyncWithServer();
                }
            }
            else
            {
                UpdateStatusText("Failed to connect to database. Will work in offline mode.");
            }
            
            if (syncProgressPanel != null)
                syncProgressPanel.SetActive(false);
        }));
    }
    
    void Update()
    {
        // Monitor shots if golf club integration is enabled
        if (enableGolfClubIntegration && clubController != null && ballController != null)
        {
            MonitorGolfClubShots();
        }
        
        // Update shot count display
        if (shotStatsText != null && shotDataManager != null)
        {
            int currentCount = shotDataManager.GetAllShots().Count;
            if (currentCount != totalShotCount)
            {
                totalShotCount = currentCount;
                UpdateShotStatsText();
            }
        }
    }
    
    private Vector3 lastBallPosition;
    private float ballStationaryTime = 0f;
    private bool shotWasHit = false;
    
    private void MonitorBallPosition(Vector3 position)
    {
        if (ballController == null) return;
        
        // Track ball movement for shot detection
        float ballSpeed = ballController.GetBallSpeed();
        
        if (ballSpeed > 5f && !shotWasHit)
        {
            // Ball was just hit
            shotWasHit = true;
            lastBallPosition = position;
            ballStationaryTime = 0f;
            
            // We'll record the shot after it stops
        }
        else if (shotWasHit && ballSpeed < 0.1f)
        {
            // Ball is stopping
            ballStationaryTime += Time.deltaTime;
            
            if (ballStationaryTime > 1f)
            {
                // Ball has stopped, record the shot
                RecordShot();
                shotWasHit = false;
            }
        }
    }
    
    private void MonitorGolfClubShots()
    {
        // This would ideally be event-based instead of polling
        if (clubController.IsSwinging())
        {
            // Club is in swing, monitor for hit
        }
    }
    
    private void RecordShot()
    {
        if (shotDataManager != null && clubController != null && ballController != null)
        {
            // Get current club
            ClubType currentClub = clubController.GetCurrentClubType();
            
            // Calculate shot forces based on ball velocity and spin
            Vector3 shotForce = ballController.GetBallVelocity() * 10f; // Scale to appropriate force
            Vector3 shotSpin = ballController.GetBallSpin();
            
            // Record the shot
            shotDataManager.RecordShot(currentClub, shotForce, shotSpin);
            
            LogMessage($"Recorded shot with {currentClub}");
        }
    }
    
    public void SyncWithServer()
    {
        if (isSyncing || mongoClient == null || shotDataManager == null) return;
        
        StartCoroutine(SyncProcess());
    }
    
    private IEnumerator SyncProcess()
    {
        isSyncing = true;
        
        if (syncProgressPanel != null)
            syncProgressPanel.SetActive(true);
            
        if (syncButton != null)
            syncButton.interactable = false;
            
        UpdateStatusText("Synchronizing with server...");
        
        // Start sync process
        yield return StartCoroutine(mongoClient.SyncData());
        
        // Update UI after sync
        UpdateStatusText("Synchronization complete");
        UpdateShotStatsText();
        
        if (syncProgressPanel != null)
            syncProgressPanel.SetActive(false);
            
        if (syncButton != null)
            syncButton.interactable = true;
            
        isSyncing = false;
        
        yield return new WaitForSeconds(3f);
        UpdateStatusText("Ready");
    }
    
    public void ExportData()
    {
        if (shotDataManager == null) return;
        
        string path = System.IO.Path.Combine(Application.persistentDataPath, "shot_data_export.csv");
        shotDataManager.SaveToCSV(path);
        
        UpdateStatusText($"Data exported to {path}");
    }
    
    private void OnPlayerIdChanged(string newId)
    {
        if (string.IsNullOrWhiteSpace(newId) || newId.Length < 3) return;
        
        currentPlayerId = newId;
        PlayerPrefs.SetString("PlayerId", currentPlayerId);
        
        if (mongoClient != null)
        {
            mongoClient.SetPlayerId(currentPlayerId);
        }
        
        UpdateStatusText($"Player ID set to: {currentPlayerId}");
    }
    
    private void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        
        LogMessage(message);
    }
    
    private void UpdateShotStatsText()
    {
        if (shotStatsText == null || shotDataManager == null) return;
        
        List<PlayerShotData> allShots = shotDataManager.GetAllShots();
        List<PlayerShotData> unsyncedShots = shotDataManager.GetUnsyncedShots();
        
        // Build stats string
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Total Shots: {allShots.Count}");
        sb.AppendLine($"Unsynced Shots: {unsyncedShots.Count}");
        
        if (allShots.Count > 0)
        {
            var stats = shotDataManager.CalculateAverageStats(allShots);
            
            sb.AppendLine($"Avg. Ball Speed: {stats["ball_speed"]:F1} m/s");
            sb.AppendLine($"Avg. Carry: {stats["carry_distance"]:F1} m");
            
            // Add club breakdown
            Dictionary<string, int> clubCounts = new Dictionary<string, int>();
            foreach (var shot in allShots)
            {
                if (!clubCounts.ContainsKey(shot.club_type))
                    clubCounts[shot.club_type] = 0;
                    
                clubCounts[shot.club_type]++;
            }
            
            sb.AppendLine("\nClub Breakdown:");
            foreach (var club in clubCounts.Keys)
            {
                sb.AppendLine($"  {club}: {clubCounts[club]} shots");
            }
        }
        
        shotStatsText.text = sb.ToString();
    }
    
    private void LogMessage(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[GolfMongoDB] {message}");
        }
    }
    
    void OnDestroy()
    {
        // Clean up event handlers
        if (ballController != null)
        {
            ballController.OnPositionChangedEvent -= MonitorBallPosition;
        }
    }
}