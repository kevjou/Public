using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;

/// <summary>
/// Client for interacting with the MongoDB-backed Golf API, integrated with GolfTerrainManager and other golf systems
/// </summary>
public class MongoDBClient : MonoBehaviour
{
    [Header("API Configuration")] [SerializeField]
    private string apiBaseUrl = "http://localhost:3000/api";

    [SerializeField] private string apiKey = "";
    [SerializeField] private float timeoutSeconds = 10f;

    [Header("Auto Sync")] [SerializeField] private bool autoSyncOnStart = false;
    [SerializeField] private float autoSyncIntervalMinutes = 15f;

    [Header("References")] [SerializeField]
    internal ShotDataManager shotDataManager;

    [SerializeField] internal OutdoorSimulationManager simulationManager;
    [SerializeField] private GolfTerrainManager terrainManager;
    [SerializeField] private GolfBallController ballController;
    [SerializeField] private GolfClubController clubController;

    [Header("Debug")] [SerializeField] private bool debugLogging = true;

    private string playerId = "unknown-player";
    private Coroutine autoSyncCoroutine;

    // Statistics
    private int totalShotsImported = 0;
    private int totalShotsExported = 0;
    private DateTime lastSyncTime;

    // Cached player profile data
    private Dictionary<string, object> playerProfile = new Dictionary<string, object>();

    void Start()
    {
        // Initialize player ID
        playerId = PlayerPrefs.GetString("PlayerId", $"player-{SystemInfo.deviceUniqueIdentifier.Substring(0, 8)}");

        // Find required components if not assigned
        FindRequiredComponents();

        if (autoSyncOnStart)
        {
            StartCoroutine(SyncData());
        }

        if (autoSyncIntervalMinutes > 0)
        {
            StartAutoSync();
        }

        LogMessage("MongoDB client initialized with player ID: " + playerId);
    }

    void FindRequiredComponents()
    {
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
    }

    void OnDestroy()
    {
        if (autoSyncCoroutine != null)
        {
            StopCoroutine(autoSyncCoroutine);
        }
    }

    /// <summary>
    /// Start automatic synchronization with the server
    /// </summary>
    public void StartAutoSync()
    {
        if (autoSyncCoroutine != null)
        {
            StopCoroutine(autoSyncCoroutine);
        }

        autoSyncCoroutine = StartCoroutine(AutoSyncRoutine());
        LogMessage("Automatic sync started");
    }

    /// <summary>
    /// Test connection to the server
    /// </summary>
    public IEnumerator TestConnection(Action<bool> callback)
    {
        LogMessage("Testing connection to server...");

        using (UnityWebRequest webRequest = UnityWebRequest.Get($"{apiBaseUrl}/health"))
        {
            webRequest.SetRequestHeader("X-API-Key", apiKey);
            webRequest.timeout = Mathf.RoundToInt(timeoutSeconds);

            yield return webRequest.SendWebRequest();

            bool success = webRequest.result == UnityWebRequest.Result.Success;

            if (success)
            {
                LogMessage("Connection test successful!");
            }
            else
            {
                LogError($"Connection test failed: {webRequest.error}");
            }

            callback?.Invoke(success);
        }
    }

    /// <summary>
    /// Fetch shot data from the server and import into the local system
    /// </summary>
    public IEnumerator SyncData()
    {
        LogMessage("Syncing with server...");

        // Step 1: Fetch player profile
        yield return StartCoroutine(FetchPlayerProfile(playerId));

        // Step 2: Fetch recent shots
        yield return StartCoroutine(FetchPlayerShots(playerId));

        // Step 3: Upload any pending local shots
        yield return StartCoroutine(UploadLocalShots());

        // Update last sync time
        lastSyncTime = DateTime.Now;
        LogMessage($"Sync completed at {lastSyncTime}. Imported: {totalShotsImported}, Exported: {totalShotsExported}");
    }

    /// <summary>
    /// Coroutine to handle automatic synchronization at set intervals
    /// </summary>
    private IEnumerator AutoSyncRoutine()
    {
        while (true)
        {
            yield return StartCoroutine(SyncData());

            // Wait for the specified interval
            yield return new WaitForSeconds(autoSyncIntervalMinutes * 60f);
        }
    }

    /// <summary>
    /// Fetch player profile from the server
    /// </summary>
    private IEnumerator FetchPlayerProfile(string playerId)
    {
        string url = $"{apiBaseUrl}/players/{playerId}";

        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            webRequest.SetRequestHeader("X-API-Key", apiKey);
            webRequest.SetRequestHeader("X-Unity-Platform", Application.platform.ToString());
            webRequest.timeout = Mathf.RoundToInt(timeoutSeconds);

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string jsonResult = webRequest.downloadHandler.text;
                LogMessage($"Player profile fetched: {jsonResult.Substring(0, Mathf.Min(100, jsonResult.Length))}...");

                // Parse player profile
                try
                {
                    PlayerProfile profile = JsonUtility.FromJson<PlayerProfile>(jsonResult);
                    if (profile != null)
                    {
                        // Update local data based on profile
                        UpdateLocalProfileData(profile);
                    }
                }
                catch (Exception e)
                {
                    LogError($"Error parsing player profile: {e.Message}");
                }
            }
            else if (webRequest.result == UnityWebRequest.Result.ConnectionError ||
                     webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                LogError($"Error fetching player profile: {webRequest.error}");

                // If player not found (404), we'll create this player later when uploading shots
                if (webRequest.responseCode == 404)
                {
                    LogMessage("Player not found. Will be created on first shot upload.");
                }
            }
        }
    }

    /// <summary>
    /// Update local game components based on player profile data
    /// </summary>
    private void UpdateLocalProfileData(PlayerProfile profile)
    {
        if (profile == null) return;

        LogMessage($"Updating local profile for {profile.playerId}");

        // We could update simulation parameters based on profile data
        if (simulationManager != null && profile.clubProfiles != null)
        {
            // This would adjust simulation parameters based on player stats
        }

        // Update terrain conditions if needed
        if (terrainManager != null)
        {
            // This could adjust terrain parameters based on player preferences
        }

        // Update UI elements (could be implemented elsewhere)
    }

    /// <summary>
    /// Fetch player shots from the server
    /// </summary>
    private IEnumerator FetchPlayerShots(string playerId, int limit = 500)
    {
        // Get timestamp of most recent local shot for incremental sync
        DateTime lastLocalShotTime = DateTime.MinValue;
        if (shotDataManager != null && shotDataManager.GetLatestShotTimestamp() != DateTime.MinValue)
        {
            lastLocalShotTime = shotDataManager.GetLatestShotTimestamp();
            LogMessage($"Fetching shots newer than {lastLocalShotTime.ToString("o")}");
        }

        string startDateParam = lastLocalShotTime != DateTime.MinValue
            ? $"&startDate={Uri.EscapeDataString(lastLocalShotTime.ToString("o"))}"
            : "";

        string url = $"{apiBaseUrl}/shots?playerId={playerId}&limit={limit}{startDateParam}";

        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            webRequest.SetRequestHeader("X-API-Key", apiKey);
            webRequest.SetRequestHeader("X-Unity-Platform", Application.platform.ToString());
            webRequest.timeout = Mathf.RoundToInt(timeoutSeconds);

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string jsonResult = webRequest.downloadHandler.text;

                // Parse shot data
                try
                {
                    ShotsResponse response = JsonUtility.FromJson<ShotsResponse>(jsonResult);

                    if (response != null && response.shots != null && response.shots.Count > 0)
                    {
                        int importedCount = 0;

                        // Convert from API format to our CSV format and import
                        foreach (var shot in response.shots)
                        {
                            // Convert to PlayerShotData format
                            PlayerShotData playerShot = ConvertApiShotToPlayerShotData(shot);

                            // Add to ShotDataManager
                            if (shotDataManager != null)
                            {
                                // Mark as already synced since it came from server
                                playerShot.synced_with_server = true;

                                shotDataManager.AddShotData(playerShot);
                                importedCount++;
                            }
                        }

                        totalShotsImported += importedCount;
                        LogMessage($"Imported {importedCount} shots from server");
                    }
                    else
                    {
                        LogMessage("No new shots to import from server");
                    }
                }
                catch (Exception e)
                {
                    LogError($"Error parsing shots: {e.Message}");
                }
            }
            else
            {
                LogError($"Error fetching shots: {webRequest.error}");
            }
        }
    }

    /// <summary>
    /// Upload local shots to the server
    /// </summary>
    private IEnumerator UploadLocalShots()
    {
        if (shotDataManager == null)
        {
            LogError("Shot data manager reference missing!");
            yield break;
        }

        // Get unsynchronized shots
        List<PlayerShotData> unsyncedShots = shotDataManager.GetUnsyncedShots();

        if (unsyncedShots == null || unsyncedShots.Count == 0)
        {
            LogMessage("No local shots to upload");
            yield break;
        }

        LogMessage($"Uploading {unsyncedShots.Count} local shots to server");

        // Convert to API format
        List<ApiShotData> apiShots = unsyncedShots.Select(ConvertPlayerShotDataToApiShot).ToList();

        // Create batch request
        BatchShotRequest batchRequest = new BatchShotRequest
        {
            shots = apiShots
        };

        string jsonData = JsonUtility.ToJson(batchRequest);

        using (UnityWebRequest webRequest = new UnityWebRequest($"{apiBaseUrl}/shots/batch", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("X-API-Key", apiKey);
            webRequest.SetRequestHeader("X-Unity-Platform", Application.platform.ToString());
            webRequest.timeout = Mathf.RoundToInt(timeoutSeconds);

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string response = webRequest.downloadHandler.text;
                try
                {
                    BatchShotResponse batchResponse = JsonUtility.FromJson<BatchShotResponse>(response);

                    if (batchResponse != null)
                    {
                        LogMessage($"Successfully uploaded {batchResponse.imported} shots to server");
                        totalShotsExported += batchResponse.imported;

                        // Mark shots as synced
                        shotDataManager.MarkShotsAsSynced(unsyncedShots);
                    }
                }
                catch (Exception e)
                {
                    LogError($"Error parsing batch response: {e.Message}");
                }
            }
            else
            {
                LogError($"Error uploading shots: {webRequest.error}");
                LogError($"Response: {webRequest.downloadHandler.text}");
            }
        }
    }

    /// <summary>
    /// Generate a realistic shot based on player profile
    /// </summary>
    public IEnumerator GenerateRealisticShot(string clubType, Action<Player> callback)
    {
        string url = $"{apiBaseUrl}/players/{playerId}/generate-shot";

        GenerateShotRequest request = new GenerateShotRequest
        {
            clubType = clubType
        };

        string jsonData = JsonUtility.ToJson(request);

        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("X-API-Key", apiKey);
            webRequest.timeout = Mathf.RoundToInt(timeoutSeconds);

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string response = webRequest.downloadHandler.text;
                try
                {
                    Player generatedShot = JsonUtility.FromJson<Player>(response);

                    LogMessage(
                        $"Generated realistic {clubType} shot: v={generatedShot.velocity}, angle={generatedShot.angle}, rpm={generatedShot.rpm}");

                    callback?.Invoke(generatedShot);
                }
                catch (Exception e)
                {
                    LogError($"Error parsing shot response: {e.Message}");
                    callback?.Invoke(null);
                }
            }
            else
            {
                LogError($"Error generating shot: {webRequest.error}");
                callback?.Invoke(null);
            }
        }
    }

    /// <summary>
    /// Save a shot taken by the user to both local storage and server
    /// </summary>
    public void RecordShot(ClubType clubType, Vector3 force, Vector3 spin)
    {
        if (shotDataManager == null || ballController == null) return;

        // Translate the current club enum to a string format matching MongoDB schema
        string clubTypeStr = clubType.ToString();
        if (clubTypeStr.Contains("Wood"))
        {
            clubTypeStr = clubTypeStr.Replace("Wood", "Wood"); // Already correct format
        }
        else if (clubTypeStr.Contains("Iron"))
        {
            clubTypeStr = clubTypeStr.Replace("Iron", "Iron"); // Already correct format
        }
        else if (clubTypeStr.Contains("Wedge"))
        {
            clubTypeStr = clubTypeStr.Replace("Wedge", "Wedge"); // Already correct format
        }
        else if (clubTypeStr == "Putter")
        {
            clubTypeStr = "Putter";
        }

        // Create shot data
        IndoorLaunchData shotData = new IndoorLaunchData
        {
            playerId = playerId,
            clubType = clubTypeStr,
            timestamp = DateTime.Now,

            // Launch conditions
            ballSpeed = force.magnitude * 0.1f, // Convert force to ball speed
            launchAngle = Mathf.Atan2(force.y, Mathf.Sqrt(force.x * force.x + force.z * force.z)) * Mathf.Rad2Deg,
            launchDirection = Mathf.Atan2(force.x, force.z) * Mathf.Rad2Deg,
            totalSpin = spin.magnitude * 60f, // Convert angular velocity to RPM
            spinAxis = Mathf.Atan2(spin.y, -spin.x) * Mathf.Rad2Deg,

            // Estimate clubhead speed from ball speed
            clubheadSpeed = force.magnitude * 0.07f,
            smashFactor = 1.45f,

            // Get terrain data
            attackAngle = -2f + UnityEngine.Random.Range(-1f, 1f),
            clubPath = UnityEngine.Random.Range(-2f, 2f),
            faceAngle = UnityEngine.Random.Range(-2f, 2f),
            dynamicLoft = GetLoftForClub(clubType) + UnityEngine.Random.Range(-2f, 2f)
        };

        // Estimate carry distance
        shotData.indoorCarryDistance = EstimateCarryDistance(shotData.ballSpeed, shotData.launchAngle);

        // Save to shot data manager
        string sessionId = $"session-{DateTime.Now:yyyyMMdd}";
        shotDataManager.AddShotData(shotData, sessionId, 0.8f, "gameplay");

        LogMessage(
            $"Recorded shot with {clubTypeStr}, speed: {shotData.ballSpeed:F1}m/s, angle: {shotData.launchAngle:F1}°");
    }

    private float GetLoftForClub(ClubType clubType)
    {
        switch (clubType)
        {
            case ClubType.Driver: return 10f;
            case ClubType.Wood3: return 15f;
            case ClubType.Wood5: return 18f;
            case ClubType.Iron3: return 21f;
            case ClubType.Iron4: return 24f;
            case ClubType.Iron5: return 27f;
            case ClubType.Iron6: return 30f;
            case ClubType.Iron7: return 34f;
            case ClubType.Iron8: return 38f;
            case ClubType.Iron9: return 42f;
            case ClubType.PitchingWedge: return 46f;
            case ClubType.SandWedge: return 56f;
            case ClubType.Putter: return 3f;
            default: return 20f;
        }
    }

    /// <summary>
    /// Convert server API shot format to our PlayerShotData format
    /// </summary>
    private PlayerShotData ConvertApiShotToPlayerShotData(ApiShotData apiShot)
    {
        PlayerShotData playerShot = new PlayerShotData
        {
            player_id = apiShot.playerId,
            timestamp = apiShot.timestamp,
            session_id = apiShot.sessionId ?? "session-unknown",
            club_type = apiShot.clubType,

            // Launch conditions
            ball_speed = apiShot.ballData.speed,
            launch_angle = apiShot.ballData.launchAngle,
            launch_direction = apiShot.ballData.launchDirection,
            total_spin = apiShot.ballData.totalSpin,
            spin_axis = apiShot.ballData.spinAxis,

            // Club data
            clubhead_speed = apiShot.clubData.clubheadSpeed,
            smash_factor = apiShot.clubData.smashFactor,
            attack_angle = apiShot.clubData.attackAngle,
            club_path = apiShot.clubData.clubPath,
            face_angle = apiShot.clubData.faceAngle,
            dynamic_loft = apiShot.clubData.dynamicLoft,

            // Contact data 
            impact_location_x = apiShot.contactData?.impactToe ?? 0,
            impact_location_y = apiShot.contactData?.impactHeight ?? 0,

            // Other fields
            shot_consistency = apiShot.contactData?.centeredness ?? 0.5f,
            shot_tags = string.Join(",", apiShot.tags ?? new List<string>()),

            // Calculated indoor distances - might need adjustment
            indoor_carry_distance = EstimateCarryDistance(apiShot),
            indoor_total_distance = EstimateTotalDistance(apiShot),

            // Mark as synced since it came from server
            synced_with_server = true
        };

        return playerShot;
    }

    /// <summary>
    /// Convert our PlayerShotData format to server API shot format
    /// </summary>
    private ApiShotData ConvertPlayerShotDataToApiShot(PlayerShotData playerShot)
    {
        ApiShotData apiShot = new ApiShotData
        {
            playerId = playerShot.player_id,
            sessionId = playerShot.session_id,
            clubType = playerShot.club_type,
            timestamp = playerShot.timestamp,

            ballData = new BallData
            {
                speed = playerShot.ball_speed,
                launchAngle = playerShot.launch_angle,
                launchDirection = playerShot.launch_direction,
                totalSpin = playerShot.total_spin,
                spinAxis = playerShot.spin_axis
            },

            clubData = new ClubData
            {
                clubheadSpeed = playerShot.clubhead_speed,
                smashFactor = playerShot.smash_factor,
                attackAngle = playerShot.attack_angle,
                clubPath = playerShot.club_path,
                faceAngle = playerShot.face_angle,
                dynamicLoft = playerShot.dynamic_loft
            },

            contactData = new ContactData
            {
                centeredness = playerShot.shot_consistency,
                impactHeight = playerShot.impact_location_y,
                impactToe = playerShot.impact_location_x
            },

            tags = playerShot.shot_tags?.Split(',').Select(t => t.Trim()).ToList() ?? new List<string>()
        };

        return apiShot;
    }

    /// <summary>
    /// Estimate carry distance from shot parameters
    /// </summary>
    private float EstimateCarryDistance(ApiShotData shot)
    {
        // Simple estimation based on ball speed and launch angle
        // For a proper calculation, we'd use our physics model
        float speed = shot.ballData.speed;
        float angle = shot.ballData.launchAngle;

        return EstimateCarryDistance(speed, angle);
    }

    /// <summary>
    /// Estimate carry distance from ball speed and launch angle
    /// </summary>
    private float EstimateCarryDistance(float speed, float angle)
    {
        // Very simplified formula for estimating carry distance in meters
        float estimatedCarry = speed * 4.5f * Mathf.Sin(angle * Mathf.Deg2Rad * 2) / 0.5f;

        return Mathf.Max(10, estimatedCarry);
    }

    /// <summary>
    /// Estimate total distance from shot parameters and carry
    /// </summary>
    private float EstimateTotalDistance(ApiShotData shot)
    {
        float carry = EstimateCarryDistance(shot);
        float rollFactor = 1.1f; // Default roll factor

        // Adjust roll factor based on club and launch conditions
        string clubType = shot.clubType.ToLower();
        float launchAngle = shot.ballData.launchAngle;

        if (clubType.Contains("driver") || clubType.Contains("wood"))
        {
            rollFactor = 1.15f - (launchAngle * 0.005f); // Less roll with higher launch
        }
        else if (clubType.Contains("iron"))
        {
            int ironNumber = 7; // Default to 7-iron
            try
            {
                if (clubType.Contains("3")) ironNumber = 3;
                else if (clubType.Contains("4")) ironNumber = 4;
                else if (clubType.Contains("5")) ironNumber = 5;
                else if (clubType.Contains("6")) ironNumber = 6;
                else if (clubType.Contains("7")) ironNumber = 7;
                else if (clubType.Contains("8")) ironNumber = 8;
                else if (clubType.Contains("9")) ironNumber = 9;
            }
            catch
            {
                /* Use default */
            }

            rollFactor = 1.1f - ((10 - ironNumber) * 0.01f); // Long irons roll more
        }
        else if (clubType.Contains("wedge") || clubType.Contains("sand"))
        {
            rollFactor = 1.02f; // Wedges don't roll much
        }

        return carry * rollFactor;
    }

    /// <summary>
    /// Set the player ID for this client
    /// </summary>
    public void SetPlayerId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length < 3)
        {
            LogError("Invalid player ID");
            return;
        }

        playerId = id;
        PlayerPrefs.SetString("PlayerId", playerId);
        PlayerPrefs.Save();

        LogMessage($"Player ID set to: {playerId}");
    }

    /// <summary>
    /// Get the current player ID
    /// </summary>
    public string GetPlayerId()
    {
        return playerId;
    }

    /// <summary>
    /// Get the API base URL
    /// </summary>
    public string GetApiBaseUrl()
    {
        return apiBaseUrl;
    }

    /// <summary>
    /// Set the API base URL
    /// </summary>
    public void SetApiBaseUrl(string url)
    {
        apiBaseUrl = url;
    }

    /// <summary>
    /// Set the API key
    /// </summary>
    public void SetApiKey(string key)
    {
        apiKey = key;
    }

    /// <summary>
    /// Log message (with debug logging option)
    /// </summary>
    private void LogMessage(string message)
    {
        if (debugLogging)
            Debug.Log($"[MongoDB] {message}");
    }

    /// <summary>
    /// Log error
    /// </summary>
    private void LogError(string message)
    {
        Debug.LogError($"[MongoDB] {message}");
    }

    #region Data Classes

    // API request/response classes

    [Serializable]
    private class BatchShotRequest
    {
        public List<ApiShotData> shots;
    }

    [Serializable]
    private class BatchShotResponse
    {
        public bool success;
        public int imported;
        public int total;
        public int failed;
        public string message;
    }

    [Serializable]
    private class ShotsResponse
    {
        public List<ApiShotData> shots;
        public PaginationInfo pagination;
    }

    [Serializable]
    private class PaginationInfo
    {
        public int total;
        public int limit;
        public int skip;
        public bool hasMore;
    }

    [Serializable]
    private class GenerateShotRequest
    {
        public string clubType;
    }

    [Serializable]
    private class ApiShotData
    {
        public string playerId;
        public string sessionId;
        public string clubType;
        public string timestamp;
        public BallData ballData;
        public ClubData clubData;
        public ContactData contactData;
        public ShotContext shotContext;
        public List<string> tags;
        public string notes;
    }

    [Serializable]
    private class BallData
    {
        public float speed;
        public float launchAngle;
        public float launchDirection;
        public float totalSpin;
        public float spinAxis;
    }

    [Serializable]
    private class ClubData
    {
        public float clubheadSpeed;
        public float smashFactor;
        public float attackAngle;
        public float clubPath;
        public float faceAngle;
        public float dynamicLoft;
    }

    [Serializable]
    private class ContactData
    {
        public float centeredness;
        public float impactHeight;
        public float impactToe;
    }

    [Serializable]
    private class ShotContext
    {
        public string shotType;
        public string lieCondition;
        public float targetDistance;
        public string shotOutcome;
    }

    [Serializable]
    private class PlayerProfile
    {
        public string playerId;
        public string playerName;
        public DateTime lastUpdated;
        public int totalShots;
        public DateTime lastSession;
        public OverallStats overallStats;
        public PlayingCharacteristics playingCharacteristics;
        public Dictionary<string, ClubProfile> clubProfiles;
    }

    [Serializable]
    private class OverallStats
    {
        public float overallSkillLevel;
        public float consistency;
        public float improvement;
    }

    [Serializable]
    private class PlayingCharacteristics
    {
        public float aggressiveness;
        public float courseManagement;
        public float shortGameSkill;
        public float drivingAccuracy;
        public float weatherAdaptability;
    }

    [Serializable]
    private class ClubProfile
    {
        public string clubName;
        public string clubCategory;
        public int shotCount;
        public StatisticsData ballSpeedStats;
        public StatisticsData launchAngleStats;
        public StatisticsData spinStats;
        public AccuracyStats accuracyStats;
        public PerformanceMetrics performanceMetrics;
    }

    [Serializable]
    private class StatisticsData
    {
        public float mean;
        public float standardDeviation;
        public float min;
        public float max;
        public float median;
        public float percentile25;
        public float percentile75;
    }

    [Serializable]
    private class AccuracyStats
    {
        public float centerednessAverage;
        public float shotDispersion;
        public float smashFactorConsistency;
    }

    [Serializable]
    private class PerformanceMetrics
    {
        public float skillLevel;
        public float contactQuality;
        public float recentTrend;
        public bool hasRecentData;
    }
}

#endregion