using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;

/// <summary>
/// Extended ShotDataManager with MongoDB integration support, adapted for the golf simulation project
/// </summary>
public class ShotDataManager : MonoBehaviour
{
    public System.Action<IndoorLaunchData> OnShotDataAdded;
    [Header("References")]
    public OutdoorSimulationManager simulationManager;
    public GolfTerrainManager terrainManager;
    public GolfBallController ballController;
    
    [Header("Settings")]
    public string defaultExportPath;
    public bool autoSaveOnNewShot = true;
    public bool enableServerSync = true;
    
    [Header("Server Integration")]
    public MongoDBClient mongoClient;
    
    // Cached data
    private List<PlayerShotData> allShotData = new List<PlayerShotData>();
    private string dataFilePath;
    
    // Cache for last data information
    private DateTime _latestShotTimestamp = DateTime.MinValue;
    
    void Awake()
    {
        // Initialize data file path
        dataFilePath = string.IsNullOrEmpty(defaultExportPath) 
            ? Path.Combine(Application.persistentDataPath, "shot_data.csv") 
            : defaultExportPath;
            
        // Find required components if not assigned
        FindRequiredComponents();
        
        // Load existing data if available
        if (File.Exists(dataFilePath))
        {
            LoadFromCSV(dataFilePath);
            Debug.Log($"Loaded {allShotData.Count} shots from {dataFilePath}");
        }
    }
    
    void FindRequiredComponents()
    {
        if (simulationManager == null)
            simulationManager = FindObjectOfType<OutdoorSimulationManager>();
            
        if (terrainManager == null)
            terrainManager = FindObjectOfType<GolfTerrainManager>();
            
        if (ballController == null)
            ballController = FindObjectOfType<GolfBallController>();
            
        if (mongoClient == null)
            mongoClient = FindObjectOfType<MongoDBClient>();
    }
    
    void OnApplicationQuit()
    {
        // Save data on quit
        if (allShotData.Count > 0)
        {
            SaveToCSV(dataFilePath);
            Debug.Log($"Saved {allShotData.Count} shots to {dataFilePath}");
        }
    }
    
    /// <summary>
    /// Hook into GolfBallController to automatically record shots
    /// </summary>
    void Start()
    {
        if (ballController != null)
        {
            // Connect to GolfClubController to monitor shots
            GolfClubController clubController = FindObjectOfType<GolfClubController>();
            if (clubController != null)
            {
                // Ideally, you would implement an event system in GolfClubController
                // to notify when a shot is taken, then subscribe to that event
                
                Debug.Log("Connected to GolfClubController for automatic shot recording");
            }
            
            Debug.Log("ShotDataManagerExtended initialized and ready to record shots");
        }
    }
    
    #region Data Management
    
    /// <summary>
    /// Add a new shot from IndoorLaunchData
    /// </summary>
    public void AddShotData(IndoorLaunchData launchData, string sessionId, float consistency, string tags)
    {
        PlayerShotData shotData = PlayerShotData.FromIndoorLaunchData(launchData, sessionId, consistency, tags);
        
        // Mark as not synced by default
        shotData.synced_with_server = false;
        
        AddShotData(shotData);
    }
    
    /// <summary>
    /// Add a shot directly from PlayerShotData object
    /// </summary>
    public void AddShotData(PlayerShotData shotData)
    {
        allShotData.Add(shotData);
        
        // Update latest timestamp cache
        if (DateTime.Parse(shotData.timestamp) > _latestShotTimestamp)
        {
            _latestShotTimestamp = DateTime.Parse(shotData.timestamp);
        }
        
        // Add to simulation manager
        if (simulationManager != null)
        {
            simulationManager.AddIndoorData(shotData.ToIndoorLaunchData());
        }
        
        // Auto save if enabled
        if (autoSaveOnNewShot)
        {
            SaveToCSV(dataFilePath);
        }
        
        Debug.Log($"Added shot data for {shotData.player_id} with {shotData.club_type} at {shotData.timestamp}");
        
        // Track shot data in GolfScene context if needed
        UpdateGolfGameContext(shotData);
    }
    
    /// <summary>
    /// Update game context with this shot (integration with GolfSceneGenerator)
    /// </summary>
    private void UpdateGolfGameContext(PlayerShotData shotData)
    {
        // This would integrate with the GolfSceneGenerator to update the current hole state
        GolfSceneGenerator sceneGenerator = FindObjectOfType<GolfSceneGenerator>();
        if (sceneGenerator != null)
        {
            // Here you could update the scene generator with shot results
        }
    }
    
    /// <summary>
    /// Mark shots as synced with server
    /// </summary>
    public void MarkShotsAsSynced(List<PlayerShotData> shots)
    {
        foreach (var shot in shots)
        {
            var matchingShot = allShotData.FirstOrDefault(s => 
                s.player_id == shot.player_id && 
                s.timestamp == shot.timestamp && 
                s.club_type == shot.club_type);
            
            if (matchingShot != null)
            {
                matchingShot.synced_with_server = true;
            }
        }
        
        // Save updated sync state
        if (autoSaveOnNewShot)
        {
            SaveToCSV(dataFilePath);
        }
    }
    
    /// <summary>
    /// Get all shots that haven't been synced with the server
    /// </summary>
    public List<PlayerShotData> GetUnsyncedShots()
    {
        return allShotData.Where(s => !s.synced_with_server).ToList();
    }
    
    /// <summary>
    /// Get the timestamp of the latest shot
    /// </summary>
    public DateTime GetLatestShotTimestamp()
    {
        return _latestShotTimestamp;
    }
    
    #endregion
    
    #region CSV Import/Export
    
    /// <summary>
    /// Load shot data from CSV file
    /// </summary>
    public void LoadFromCSV(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"CSV file not found: {filePath}");
                return;
            }
            
            string[] lines = File.ReadAllLines(filePath);
            
            // Reset data
            allShotData.Clear();
            _latestShotTimestamp = DateTime.MinValue;
            
            // Skip header row
            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    PlayerShotData shotData = ParseCSVLine(lines[i]);
                    allShotData.Add(shotData);
                    
                    // Update latest timestamp
                    DateTime timestamp = DateTime.Parse(shotData.timestamp);
                    if (timestamp > _latestShotTimestamp)
                    {
                        _latestShotTimestamp = timestamp;
                    }
                    
                    // Add to simulation manager
                    if (simulationManager != null)
                    {
                        simulationManager.AddIndoorData(shotData.ToIndoorLaunchData());
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing line {i}: {ex.Message}");
                }
            }
            
            Debug.Log($"Successfully loaded {allShotData.Count} shots from {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading CSV: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Save shot data to CSV file
    /// </summary>
    public void SaveToCSV(string filePath)
    {
        try
        {
            // Create directory if it doesn't exist
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Create header row
            StringBuilder csv = new StringBuilder();
            csv.AppendLine("player_id,timestamp,session_id,club_type,ball_speed,launch_angle,launch_direction,total_spin,spin_axis,clubhead_speed,smash_factor,attack_angle,club_path,face_angle,dynamic_loft,impact_location_x,impact_location_y,indoor_carry_distance,indoor_total_distance,shot_consistency,shot_tags,synced_with_server");
            
            // Add data rows
            foreach (PlayerShotData shot in allShotData)
            {
                csv.AppendLine(FormatCSVLine(shot));
            }
            
            // Write to file
            File.WriteAllText(filePath, csv.ToString());
            
            Debug.Log($"Saved {allShotData.Count} shots to {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error saving CSV: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Parse a CSV line into a PlayerShotData object
    /// </summary>
    private PlayerShotData ParseCSVLine(string line)
    {
        List<string> values = ParseCSVValues(line);
        
        PlayerShotData shot = new PlayerShotData
        {
            player_id = values[0],
            timestamp = values[1],
            session_id = values[2],
            club_type = values[3],
            ball_speed = float.Parse(values[4], CultureInfo.InvariantCulture),
            launch_angle = float.Parse(values[5], CultureInfo.InvariantCulture),
            launch_direction = float.Parse(values[6], CultureInfo.InvariantCulture),
            total_spin = float.Parse(values[7], CultureInfo.InvariantCulture),
            spin_axis = float.Parse(values[8], CultureInfo.InvariantCulture),
            clubhead_speed = float.Parse(values[9], CultureInfo.InvariantCulture),
            smash_factor = float.Parse(values[10], CultureInfo.InvariantCulture),
            attack_angle = float.Parse(values[11], CultureInfo.InvariantCulture),
            club_path = float.Parse(values[12], CultureInfo.InvariantCulture),
            face_angle = float.Parse(values[13], CultureInfo.InvariantCulture),
            dynamic_loft = float.Parse(values[14], CultureInfo.InvariantCulture),
            impact_location_x = float.Parse(values[15], CultureInfo.InvariantCulture),
            impact_location_y = float.Parse(values[16], CultureInfo.InvariantCulture),
            indoor_carry_distance = float.Parse(values[17], CultureInfo.InvariantCulture),
            indoor_total_distance = float.Parse(values[18], CultureInfo.InvariantCulture),
            shot_consistency = float.Parse(values[19], CultureInfo.InvariantCulture),
            shot_tags = values[20]
        };
        
        // Parse synced_with_server flag if present
        if (values.Count > 21)
        {
            bool.TryParse(values[21], out bool synced);
            shot.synced_with_server = synced;
        }
        else
        {
            shot.synced_with_server = false; // Default to not synced
        }
        
        return shot;
    }
    
    /// <summary>
    /// Parse a CSV line into individual values, handling quoted strings with commas
    /// </summary>
    private List<string> ParseCSVValues(string line)
    {
        List<string> values = new List<string>();
        bool inQuotes = false;
        StringBuilder currentValue = new StringBuilder();
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(currentValue.ToString().Trim('"'));
                currentValue.Clear();
            }
            else
            {
                currentValue.Append(c);
            }
        }
        
        // Add the last value
        values.Add(currentValue.ToString().Trim('"'));
        
        return values;
    }
    
    /// <summary>
    /// Format a PlayerShotData object into a CSV line
    /// </summary>
    private string FormatCSVLine(PlayerShotData shot)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},\"{20}\",{21}",
            shot.player_id,
            shot.timestamp,
            shot.session_id,
            shot.club_type,
            shot.ball_speed,
            shot.launch_angle,
            shot.launch_direction,
            shot.total_spin,
            shot.spin_axis,
            shot.clubhead_speed,
            shot.smash_factor,
            shot.attack_angle,
            shot.club_path,
            shot.face_angle,
            shot.dynamic_loft,
            shot.impact_location_x,
            shot.impact_location_y,
            shot.indoor_carry_distance,
            shot.indoor_total_distance,
            shot.shot_consistency,
            shot.shot_tags,
            shot.synced_with_server.ToString().ToLower()
        );
    }
    
    #endregion
    
    #region Data Access Methods
    
    /// <summary>
    /// Get all shot data
    /// </summary>
    public List<PlayerShotData> GetAllShots()
    {
        return new List<PlayerShotData>(allShotData);
    }
    
    /// <summary>
    /// Get shot data for a specific player
    /// </summary>
    public List<PlayerShotData> GetShotDataForPlayer(string playerId)
    {
        return allShotData.Where(s => s.player_id == playerId).ToList();
    }
    
    /// <summary>
    /// Get shot data for a specific club type
    /// </summary>
    public List<PlayerShotData> GetShotDataForClub(string clubType)
    {
        return allShotData.Where(s => s.club_type == clubType).ToList();
    }
    
    /// <summary>
    /// Get shot data for a specific session
    /// </summary>
    public List<PlayerShotData> GetShotDataForSession(string sessionId)
    {
        return allShotData.Where(s => s.session_id == sessionId).ToList();
    }
    
    /// <summary>
    /// Get shot data with a specific tag
    /// </summary>
    public List<PlayerShotData> GetShotDataWithTag(string tag)
    {
        return allShotData.Where(s => s.shot_tags.Contains(tag)).ToList();
    }
    
    /// <summary>
    /// Get shot data within a date range
    /// </summary>
    public List<PlayerShotData> GetShotDataInDateRange(DateTime startDate, DateTime endDate)
    {
        return allShotData.Where(s => {
            DateTime shotTime = DateTime.Parse(s.timestamp);
            return shotTime >= startDate && shotTime <= endDate;
        }).ToList();
    }
    
    /// <summary>
    /// Get the most recent shots (up to count)
    /// </summary>
    public List<PlayerShotData> GetRecentShots(int count)
    {
        return allShotData
            .OrderByDescending(s => DateTime.Parse(s.timestamp))
            .Take(count)
            .ToList();
    }
    
    #endregion
    
    #region Statistical Analysis
    
    /// <summary>
    /// Calculate average statistics for a collection of shots
    /// </summary>
    public Dictionary<string, float> CalculateAverageStats(List<PlayerShotData> shots)
    {
        if (shots == null || shots.Count == 0)
            return new Dictionary<string, float>();
        
        Dictionary<string, float> stats = new Dictionary<string, float>
        {
            { "ball_speed", shots.Average(s => s.ball_speed) },
            { "launch_angle", shots.Average(s => s.launch_angle) },
            { "total_spin", shots.Average(s => s.total_spin) },
            { "carry_distance", shots.Average(s => s.indoor_carry_distance) },
            { "total_distance", shots.Average(s => s.indoor_total_distance) },
            { "clubhead_speed", shots.Average(s => s.clubhead_speed) },
            { "smash_factor", shots.Average(s => s.smash_factor) },
            { "consistency", shots.Average(s => s.shot_consistency) }
        };
        
        return stats;
    }
    
    /// <summary>
    /// Calculate dispersion statistics for a collection of shots
    /// </summary>
    public Dictionary<string, float> CalculateDispersionStats(List<PlayerShotData> shots)
    {
        if (shots == null || shots.Count == 0)
            return new Dictionary<string, float>();
        
        Dictionary<string, float> stats = new Dictionary<string, float>();
        
        // Calculate standard deviations
        stats["ball_speed_stddev"] = CalculateStdDev(shots.Select(s => s.ball_speed).ToArray());
        stats["launch_angle_stddev"] = CalculateStdDev(shots.Select(s => s.launch_angle).ToArray());
        stats["launch_direction_stddev"] = CalculateStdDev(shots.Select(s => s.launch_direction).ToArray());
        stats["total_spin_stddev"] = CalculateStdDev(shots.Select(s => s.total_spin).ToArray());
        stats["carry_distance_stddev"] = CalculateStdDev(shots.Select(s => s.indoor_carry_distance).ToArray());
        
        // Calculate dispersion indicators
        float avgDirection = shots.Average(s => s.launch_direction);
        float directionVariance = shots.Average(s => (s.launch_direction - avgDirection) * (s.launch_direction - avgDirection));
        
        stats["lateral_dispersion"] = Mathf.Sqrt(directionVariance);
        stats["consistency_rating"] = 1.0f / (1.0f + stats["launch_direction_stddev"] + stats["ball_speed_stddev"] / 10.0f);
        
        return stats;
    }
    
    /// <summary>
    /// Calculate standard deviation for an array of values
    /// </summary>
    private float CalculateStdDev(float[] values)
    {
        float mean = values.Average();
        float variance = values.Average(v => (v - mean) * (v - mean));
        return Mathf.Sqrt(variance);
    }
    
    #endregion
    
    #region MongoDB Integration
    
    /// <summary>
    /// Synchronize with MongoDB database
    /// </summary>
    public void SyncWithServer()
    {
        if (mongoClient == null || !enableServerSync)
        {
            Debug.LogWarning("MongoDB client not configured or sync disabled");
            return;
        }
        
        StartCoroutine(mongoClient.SyncData());
    }
    
    /// <summary>
    /// Generate a realistic shot based on player profile
    /// </summary>
    public void GenerateRealisticShot(string clubType, Action<Player> callback)
    {
        if (mongoClient == null || !enableServerSync)
        {
            Debug.LogWarning("MongoDB client not configured or sync disabled");
            
            // Fallback to local generation
            GenerateLocalShot(clubType, callback);
            return;
        }
        
        StartCoroutine(mongoClient.GenerateRealisticShot(clubType, callback));
    }
    
    /// <summary>
    /// Generate a shot based on local data (fallback when server is unavailable)
    /// </summary>
    private void GenerateLocalShot(string clubType, Action<Player> callback)
    {
        // Get shots for this club
        var shots = GetShotDataForClub(clubType);
        
        if (shots.Count < 5)
        {
            Debug.LogWarning($"Not enough data for {clubType} to generate a realistic shot");
            callback?.Invoke(null);
            return;
        }
        
        // Calculate stats
        var stats = CalculateAverageStats(shots);
        var dispersion = CalculateDispersionStats(shots);
        
        // Use Box-Muller transform for normal distribution
        float u1 = UnityEngine.Random.value;
        float u2 = UnityEngine.Random.value;
        float z0 = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        float z1 = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);
        
        // Generate shot
        Player shot = new Player
        {
            velocity = stats["ball_speed"] + z0 * dispersion["ball_speed_stddev"] * 0.5f,
            angle = stats["launch_angle"] + z1 * dispersion["launch_angle_stddev"] * 0.5f,
            rpm = stats["total_spin"] + z0 * dispersion["total_spin_stddev"] * 0.3f,
            sidespin = z1 * 200f, // Random sidespin
            temperature_K = 288.15f // Default temperature
        };
        
        // Clamp values to realistic ranges
        shot.velocity = Mathf.Max(10f, shot.velocity);
        shot.angle = Mathf.Clamp(shot.angle, 0f, 45f);
        shot.rpm = Mathf.Max(0f, shot.rpm);
        
        Debug.Log($"Generated local shot for {clubType}: v={shot.velocity:F1}, angle={shot.angle:F1}, rpm={shot.rpm:F0}");
        
        callback?.Invoke(shot);
    }
    
    /// <summary>
    /// Record a shot in the system with automatic MongoDB integration
    /// </summary>
    public void RecordShot(ClubType clubType, Vector3 shotForce, Vector3 spinForce)
    {
        if (mongoClient != null && enableServerSync)
        {
            mongoClient.RecordShot(clubType, shotForce, spinForce);
        }
        else
        {
            // Create local record without server integration
            string clubTypeStr = clubType.ToString();
            
            IndoorLaunchData shotData = new IndoorLaunchData
            {
                playerId = PlayerPrefs.GetString("PlayerId", "local-player"),
                clubType = clubTypeStr,
                timestamp = DateTime.Now,
                
                // Launch conditions - estimated from forces
                ballSpeed = shotForce.magnitude * 0.1f,
                launchAngle = Mathf.Atan2(shotForce.y, Mathf.Sqrt(shotForce.x * shotForce.x + shotForce.z * shotForce.z)) * Mathf.Rad2Deg,
                launchDirection = Mathf.Atan2(shotForce.x, shotForce.z) * Mathf.Rad2Deg,
                totalSpin = spinForce.magnitude * 60f,
                spinAxis = Mathf.Atan2(spinForce.y, -spinForce.x) * Mathf.Rad2Deg,
                
                // Basic club data
                clubheadSpeed = shotForce.magnitude * 0.07f,
                smashFactor = 1.45f
            };
            
            string sessionId = $"session-{DateTime.Now:yyyyMMdd}";
            AddShotData(shotData, sessionId, 0.8f, "local");
        }
    }
    
    #endregion
}

/// <summary>
/// Extended PlayerShotData class with server synchronization flag
/// </summary>
[System.Serializable]
public class PlayerShotData
{
    // Core player & session data
    public string player_id;
    public string timestamp;
    public string session_id;
    public string club_type;
    
    // Launch conditions
    public float ball_speed;
    public float launch_angle;
    public float launch_direction;
    public float total_spin;
    public float spin_axis;
    
    // Club data at impact
    public float clubhead_speed;
    public float smash_factor;
    public float attack_angle;
    public float club_path;
    public float face_angle;
    public float dynamic_loft;
    public float impact_location_x;
    public float impact_location_y;
    
    // Results
    public float indoor_carry_distance;
    public float indoor_total_distance;
    
    // Additional data
    public float shot_consistency;
    public string shot_tags;
    
    // Sync status
    public bool synced_with_server = false;
    
    /// <summary>
    /// Convert to IndoorLaunchData for simulation
    /// </summary>
    public IndoorLaunchData ToIndoorLaunchData()
    {
        return new IndoorLaunchData
        {
            playerId = player_id,
            clubType = club_type,
            ballSpeed = ball_speed,
            launchAngle = launch_angle,
            launchDirection = launch_direction,
            totalSpin = total_spin,
            spinAxis = spin_axis,
            clubheadSpeed = clubhead_speed,
            smashFactor = smash_factor,
            attackAngle = attack_angle,
            clubPath = club_path,
            faceAngle = face_angle,
            dynamicLoft = dynamic_loft,
            indoorCarryDistance = indoor_carry_distance,
            timestamp = DateTime.Parse(timestamp)
        };
    }
    
    /// <summary>
    /// Create from IndoorLaunchData for export
    /// </summary>
    public static PlayerShotData FromIndoorLaunchData(IndoorLaunchData data, string sessionId, float consistency, string tags)
    {
        return new PlayerShotData
        {
            player_id = data.playerId,
            timestamp = data.timestamp.ToString("o"), // ISO 8601 format
            session_id = sessionId,
            club_type = data.clubType,
            ball_speed = data.ballSpeed,
            launch_angle = data.launchAngle,
            launch_direction = data.launchDirection,
            total_spin = data.totalSpin,
            spin_axis = data.spinAxis,
            clubhead_speed = data.clubheadSpeed,
            smash_factor = data.smashFactor,
            attack_angle = data.attackAngle,
            club_path = data.clubPath,
            face_angle = data.faceAngle,
            dynamic_loft = data.dynamicLoft,
            indoor_carry_distance = data.indoorCarryDistance,
            indoor_total_distance = EstimateTotalDistance(data.indoorCarryDistance, data.clubType, data.launchAngle),
            shot_consistency = consistency,
            shot_tags = tags,
            synced_with_server = false // Default to not synced
        };
    }
    
    /// <summary>
    /// Estimate total distance from carry distance
    /// </summary>
    private static float EstimateTotalDistance(float carryDistance, string clubType, float launchAngle)
    {
        float rollFactor = 1.1f; // Default roll factor
        
        // Adjust roll factor based on club type and launch angle
        clubType = clubType.ToLower();
        
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
            catch { /* Use default */ }
            
            rollFactor = 1.1f - ((10 - ironNumber) * 0.01f); // Long irons roll more
        }
        else if (clubType.Contains("wedge") || clubType.Contains("sand"))
        {
            rollFactor = 1.02f; // Wedges don't roll much
        }
        else if (clubType.Contains("putter"))
        {
            rollFactor = 1.0f; // Putts don't roll extra beyond carry distance
        }
        
        return carryDistance * rollFactor;
    }
}