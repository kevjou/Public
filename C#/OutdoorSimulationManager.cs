using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using System.Globalization;
using System.Text;

[System.Serializable]
public class IndoorLaunchData
{
    public string playerId;
    public string clubType;
    
    // Pure launch conditions (indoor, no environmental factors)
    public float ballSpeed;      // m/s
    public float launchAngle;    // degrees
    public float launchDirection; // degrees (left/right deviation)
    public float totalSpin;      // rpm
    public float spinAxis;       // degrees (0 = pure backspin, 90 = pure sidespin)
    
    // Club data at impact
    public float clubheadSpeed;  // m/s
    public float smashFactor;    // ball speed / clubhead speed
    public float attackAngle;    // degrees
    public float clubPath;       // degrees
    public float faceAngle;      // degrees
    public float dynamicLoft;    // degrees
    
    // Indoor carry distance (for validation only)
    public float indoorCarryDistance; // meters (limited by simulator space)
    
    public DateTime timestamp;
}

[System.Serializable]
public class EnvironmentalConditions
{
    public float temperature;    // Celsius
    public float humidity;       // percentage (affects air density)
    public float pressure;       // hPa (altitude effect)
    public float windSpeed;      // m/s
    public float windDirection;  // degrees (0 = headwind, 90 = left crosswind)
    public float windGustFactor; // multiplier for wind variability
    
    public float GetAirDensity()
    {
        // Calculate air density from temperature, humidity, pressure
        float tempK = temperature + 273.15f;
        float saturationPressure = 6.1078f * Mathf.Pow(10f, (7.5f * temperature) / (237.3f + temperature));
        float vaporPressure = (humidity / 100f) * saturationPressure;
        float dryAirPressure = pressure - vaporPressure;
        
        // Dry air density + water vapor density
        float dryAirDensity = (dryAirPressure * 100f) / (287.05f * tempK);
        float waterVaporDensity = (vaporPressure * 100f) / (461.5f * tempK);
        
        return dryAirDensity + waterVaporDensity;
    }
    
    public Vector3 GetWindVector()
    {
        float radians = windDirection * Mathf.Deg2Rad;
        return new Vector3(
            windSpeed * Mathf.Sin(radians),  // x-component (crosswind)
            0f,                              // y-component (no vertical wind for now)
            windSpeed * Mathf.Cos(radians)   // z-component (headwind/tailwind)
        );
    }
}

[System.Serializable]
public class ClubPerformanceProfile
{
    public string clubName;
    
    // Launch condition statistics (from indoor data)
    public float averageBallSpeed;
    public float ballSpeedStdDev;
    public float averageLaunchAngle;
    public float launchAngleStdDev;
    public float averageTotalSpin;
    public float totalSpinStdDev;
    public float averageSpinAxis;
    public float spinAxisStdDev;
    public float directionStdDev;        // Launch direction consistency
    
    // Face angle statistics
    public float averageFaceAngle;
    public float faceAngleStdDev;
    
    // Consistency metrics
    public float smashFactorStdDev;      // How consistent is contact quality
    public float attackAngleStdDev;      // How consistent is swing plane
    
    // Performance ranges
    public float maxBallSpeed;
    public float minBallSpeed;
    public float maxSpin;
    public float minSpin;
    
    public List<IndoorLaunchData> historicalData = new List<IndoorLaunchData>();
    
    // Player skill level indicators
    public float skillLevel; // 0-1, affects dispersion in adverse conditions
    public float windAdaptability; // 0-1, how well player adjusts to wind
}

[System.Serializable]
public class OutdoorShotResult
{
    public IndoorLaunchData indoorLaunchData;
    public EnvironmentalConditions environmentalConditions;
    public Vector3 finalPosition;
    public float carryDistance;
    public float totalDistance;
    public float flightTime;
    public float maxHeight;
    public float lateralDeviation;
}

public class OutdoorSimulationManager : MonoBehaviour
{
    [Header("Data Management")]
    public List<IndoorLaunchData> indoorData = new List<IndoorLaunchData>();
    public Dictionary<string, ClubPerformanceProfile> clubProfiles = new Dictionary<string, ClubPerformanceProfile>();
    
    [Header("Environmental Simulation")]
    public List<EnvironmentalConditions> trainingEnvironments = new List<EnvironmentalConditions>();
    public GolfBallPhysics physicsEngine;
    
    [Header("AI Training")]
    public AIGolfBrain golfBrain;
    public int simulationsPerScenario = 500;
    
    [Header("MongoDB Integration")]
    [Tooltip("Enables integration with MongoDB via the ShotDataManagerExtended")]
    public bool enableMongoDBIntegration = true;
    [SerializeField] private ShotDataManager shotDataManager;
    [SerializeField] private MongoDBClient mongoClient;
    
    [Header("Shot Analysis")]
    [Tooltip("Enable real-time analysis of shots")]
    public bool enableShotAnalysis = true;
    public bool autoUpdateProfiles = true;
    
    void Start()
    {
        // Find required components if not assigned
        FindRequiredComponents();
        
        // Generate standard environmental conditions
        GenerateTrainingEnvironments();
        
        // Subscribe to shot data events from MongoDB integration
        SubscribeToShotDataEvents();
    }
    
    private void FindRequiredComponents()
    {
        if (physicsEngine == null)
            physicsEngine = FindObjectOfType<GolfBallPhysics>();
            
        if (enableMongoDBIntegration)
        {
            if (shotDataManager == null)
                shotDataManager = FindObjectOfType<ShotDataManager>();
                
            if (mongoClient == null)
                mongoClient = FindObjectOfType<MongoDBClient>();
        }
    }
    
    private void SubscribeToShotDataEvents()
    {
        // Listen for when new shots are added through the MongoDB integration
        if (enableMongoDBIntegration && shotDataManager != null)
        {
            shotDataManager.OnShotDataAdded += OnShotDataAddedFromMongoDB;
        }
    }
    
    // Handler for when shot data is added through MongoDB
    private void OnShotDataAddedFromMongoDB(IndoorLaunchData newShotData)
    {
        // Make sure we don't double-add the data
        if (!indoorData.Any(data => 
            data.playerId == newShotData.playerId && 
            data.timestamp == newShotData.timestamp))
        {
            // Add to our local data collection
            AddIndoorData(newShotData);
        }
    }
    
    void GenerateTrainingEnvironments()
    {
        // Create diverse environmental conditions for training
        trainingEnvironments.Clear();
        
        // Calm conditions
        trainingEnvironments.Add(new EnvironmentalConditions
        {
            temperature = 20f, humidity = 50f, pressure = 1013f,
            windSpeed = 0f, windDirection = 0f
        });
        
        // Various wind conditions
        for (int windDir = 0; windDir < 360; windDir += 45)
        {
            for (float windSpeed = 2f; windSpeed <= 15f; windSpeed += 3f)
            {
                trainingEnvironments.Add(new EnvironmentalConditions
                {
                    temperature = UnityEngine.Random.Range(5f, 35f),
                    humidity = UnityEngine.Random.Range(30f, 90f),
                    pressure = UnityEngine.Random.Range(950f, 1050f),
                    windSpeed = windSpeed,
                    windDirection = windDir,
                    windGustFactor = UnityEngine.Random.Range(0.8f, 1.3f)
                });
            }
        }
        
        // Extreme conditions
        trainingEnvironments.Add(new EnvironmentalConditions
        {
            temperature = -5f, humidity = 80f, pressure = 950f,
            windSpeed = 20f, windDirection = 180f // Strong tailwind
        });
        
        Debug.Log($"Generated {trainingEnvironments.Count} training environments");
    }
    
    public void ImportIndoorData(string csvFilePath)
    {
        // Parse CSV from launch monitor
        // Format: PlayerId,Club,BallSpeed,LaunchAngle,LaunchDirection,TotalSpin,SpinAxis,ClubheadSpeed,AttackAngle,etc.
        Debug.Log($"Importing indoor launch data from {csvFilePath}");
        
        // Implement CSV parsing using System.IO
        try {
            string[] lines = System.IO.File.ReadAllLines(csvFilePath);
            string[] headers = lines[0].Split(',');
            
            for (int i = 1; i < lines.Length; i++)
            {
                string[] values = lines[i].Split(',');
                IndoorLaunchData data = new IndoorLaunchData();
                
                data.playerId = values[0];
                data.clubType = values[1];
                data.ballSpeed = float.Parse(values[2], CultureInfo.InvariantCulture);
                data.launchAngle = float.Parse(values[3], CultureInfo.InvariantCulture);
                data.launchDirection = float.Parse(values[4], CultureInfo.InvariantCulture);
                data.totalSpin = float.Parse(values[5], CultureInfo.InvariantCulture);
                data.spinAxis = float.Parse(values[6], CultureInfo.InvariantCulture);
                data.clubheadSpeed = float.Parse(values[7], CultureInfo.InvariantCulture);
                data.smashFactor = data.ballSpeed / data.clubheadSpeed;
                data.attackAngle = float.Parse(values[8], CultureInfo.InvariantCulture);
                data.clubPath = float.Parse(values[9], CultureInfo.InvariantCulture);
                data.faceAngle = float.Parse(values[10], CultureInfo.InvariantCulture);
                data.dynamicLoft = float.Parse(values[11], CultureInfo.InvariantCulture);
                
                data.timestamp = DateTime.Now; // Or parse from file if available
                
                AddIndoorData(data);
                
                // Also add to MongoDB integration system if enabled
                if (enableMongoDBIntegration && shotDataManager != null)
                {
                    shotDataManager.AddShotData(data, "import-session", 0.8f, "imported");
                }
            }
            
            Debug.Log($"Successfully imported {lines.Length - 1} data points");
        }
        catch (Exception e) {
            Debug.LogError($"Error importing data: {e.Message}");
        }
    }
    
    public void AddIndoorData(IndoorLaunchData data)
    {
        indoorData.Add(data);
        UpdateClubProfile(data);
        
        // If MongoDB integration is enabled, ensure the data is also added there
        if (enableMongoDBIntegration && shotDataManager != null && 
            !shotDataManager.HasShot(data.playerId, data.timestamp))
        {
            string sessionId = data.timestamp.ToString("yyyyMMdd");
            shotDataManager.AddShotData(data, sessionId, 0.8f, "auto-added");
        }
    }
    
    void UpdateClubProfile(IndoorLaunchData data)
    {
        if (!clubProfiles.ContainsKey(data.clubType))
        {
            clubProfiles[data.clubType] = new ClubPerformanceProfile
            {
                clubName = data.clubType,
                historicalData = new List<IndoorLaunchData>()
            };
        }
        
        var clubProfile = clubProfiles[data.clubType];
        clubProfile.historicalData.Add(data);
        
        RecalculateProfile(clubProfile);
    }
    
    void RecalculateProfile(ClubPerformanceProfile profile)
    {
        if (profile.historicalData.Count == 0) return;
        
        // Calculate averages
        profile.averageBallSpeed = profile.historicalData.Average(d => d.ballSpeed);
        profile.averageLaunchAngle = profile.historicalData.Average(d => d.launchAngle);
        profile.averageTotalSpin = profile.historicalData.Average(d => d.totalSpin);
        profile.averageSpinAxis = profile.historicalData.Average(d => d.spinAxis);
        profile.averageFaceAngle = profile.historicalData.Average(d => d.faceAngle);
        
        // Calculate standard deviations
        profile.ballSpeedStdDev = CalculateStdDev(profile.historicalData.Select(d => d.ballSpeed).ToArray());
        profile.launchAngleStdDev = CalculateStdDev(profile.historicalData.Select(d => d.launchAngle).ToArray());
        profile.totalSpinStdDev = CalculateStdDev(profile.historicalData.Select(d => d.totalSpin).ToArray());
        profile.directionStdDev = CalculateStdDev(profile.historicalData.Select(d => d.launchDirection).ToArray());
        profile.faceAngleStdDev = CalculateStdDev(profile.historicalData.Select(d => d.faceAngle).ToArray());
        
        // Calculate ranges
        profile.maxBallSpeed = profile.historicalData.Max(d => d.ballSpeed);
        profile.minBallSpeed = profile.historicalData.Min(d => d.ballSpeed);
        profile.maxSpin = profile.historicalData.Max(d => d.totalSpin);
        profile.minSpin = profile.historicalData.Min(d => d.totalSpin);
        
        // Estimate skill level based on consistency
        float consistencyScore = 1f / (1f + profile.ballSpeedStdDev + profile.launchAngleStdDev + profile.directionStdDev + profile.faceAngleStdDev);
        profile.skillLevel = Mathf.Clamp01(consistencyScore);
        profile.windAdaptability = profile.skillLevel * 0.8f; // Assume wind skill correlates with general skill
        
        Debug.Log($"Updated profile for {profile.clubName}: Avg Speed: {profile.averageBallSpeed:F1}m/s, Skill: {profile.skillLevel:F2}");
    }
    
    float CalculateStdDev(float[] values)
    {
        float mean = values.Average();
        float variance = values.Average(v => (v - mean) * (v - mean));
        return Mathf.Sqrt(variance);
    }
    
    public OutdoorShotResult SimulateOutdoorShot(IndoorLaunchData indoorShot, EnvironmentalConditions environment)
    {
        // Make sure we have physics engine
        if (physicsEngine == null)
        {
            Debug.LogError("Physics engine not assigned in OutdoorSimulationManager");
            return null;
        }
        
        // Convert indoor launch data to physics engine parameters
        Player shotParams = new Player
        {
            velocity = indoorShot.ballSpeed,
            angle = indoorShot.launchAngle,
            rpm = indoorShot.totalSpin * Mathf.Cos(indoorShot.spinAxis * Mathf.Deg2Rad),
            sidespin = indoorShot.totalSpin * Mathf.Sin(indoorShot.spinAxis * Mathf.Deg2Rad),
            temperature_K = environment.temperature + 273.15f,
            faceAngle = indoorShot.faceAngle + indoorShot.launchDirection, // Combine face angle and launch direction
            attackAngle = indoorShot.attackAngle
        };
        
        // Apply environmental effects to physics engine
        physicsEngine.SetEnvironmentalConditions(environment);
        
        // Run simulation
        var result = physicsEngine.SimulateShot(shotParams);
        
        // Create and return result object
        return new OutdoorShotResult
        {
            indoorLaunchData = indoorShot,
            environmentalConditions = environment,
            finalPosition = result.finalPosition,
            carryDistance = result.carryDistance,
            totalDistance = result.totalDistance,
            flightTime = result.flightTime,
            maxHeight = result.maxHeight,
            lateralDeviation = result.lateralDeviation
        };
    }
    
    float SampleNormal(float mean, float stdDev)
    {
        float u1 = 1.0f - UnityEngine.Random.value;
        float u2 = 1.0f - UnityEngine.Random.value;
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        return mean + stdDev * randStdNormal;
    }
    
    public Player GenerateEnvironmentallyAdjustedShot(string clubType, EnvironmentalConditions environment)
    {
        // First check if we should use MongoDB-based shot generation
        ClubPerformanceProfile clubProfile;
        if (enableMongoDBIntegration && mongoClient != null && shotDataManager != null)
        {
            // Use MongoDB profile data if available
            Player result = null;
            bool completed = false;
            
            // Start the coroutine to generate the shot
            shotDataManager.GenerateRealisticShot(clubType, (player) => {
                result = player;
                completed = true;
            });
            
            // Wait for the coroutine to complete (up to 3 seconds)
            float waitTime = 0;
            while (!completed && waitTime < 3.0f)
            {
                waitTime += Time.deltaTime;
            }
            
            // If we got a result from MongoDB, use it
            if (result != null)
            {
                Debug.Log($"Using MongoDB-generated shot for {clubType}");
                // Add face angle if not already set
                if (result.faceAngle == 0 && clubProfiles.ContainsKey(clubType))
                {
                    clubProfile = clubProfiles[clubType];
                    result.faceAngle = SampleNormal(clubProfile.averageFaceAngle, clubProfile.faceAngleStdDev);
                }
                return result;
            }
        }
        
        // Fall back to local profile-based generation
        if (!clubProfiles.ContainsKey(clubType))
        {
            Debug.LogWarning($"No club profile found for {clubType}, falling back to defaults");
            return new Player
            {
                velocity = 50f,
                angle = 15f,
                rpm = 3000f,
                sidespin = 0f,
                temperature_K = environment.temperature + 273.15f,
                faceAngle = 0f,
                attackAngle = -3f
            };
        }
        
        clubProfile = clubProfiles[clubType];
        
        // Start with typical indoor launch conditions
        float ballSpeed = SampleNormal(clubProfile.averageBallSpeed, clubProfile.ballSpeedStdDev);
        float launchAngle = SampleNormal(clubProfile.averageLaunchAngle, clubProfile.launchAngleStdDev);
        float totalSpin = SampleNormal(clubProfile.averageTotalSpin, clubProfile.totalSpinStdDev);
        float spinAxis = SampleNormal(clubProfile.averageSpinAxis, clubProfile.spinAxisStdDev);
        float faceAngle = SampleNormal(clubProfile.averageFaceAngle, clubProfile.faceAngleStdDev);
        
        // Apply player adaptations to environmental conditions
        ballSpeed = ApplyEnvironmentalAdjustments(ballSpeed, environment, clubProfile);
        launchAngle = AdjustLaunchAngleForWind(launchAngle, environment, clubProfile);
        
        return new Player
        {
            velocity = ballSpeed,
            angle = launchAngle,
            rpm = totalSpin * Mathf.Cos(spinAxis * Mathf.Deg2Rad),
            sidespin = totalSpin * Mathf.Sin(spinAxis * Mathf.Deg2Rad),
            temperature_K = environment.temperature + 273.15f,
            faceAngle = faceAngle,
            attackAngle = -3f // Default attack angle
        };
    }
    
    float ApplyEnvironmentalAdjustments(float ballSpeed, EnvironmentalConditions env, ClubPerformanceProfile profile)
    {
        // Skilled players swing harder in headwinds, easier in tailwinds
        Vector3 wind = env.GetWindVector();
        float headwindComponent = -wind.z; // Negative = tailwind, positive = headwind
        
        float adjustment = headwindComponent * 0.02f * profile.windAdaptability;
        return Mathf.Clamp(ballSpeed + adjustment, profile.minBallSpeed, profile.maxBallSpeed);
    }
    
    float AdjustLaunchAngleForWind(float baseAngle, EnvironmentalConditions env, ClubPerformanceProfile profile)
    {
        // Skilled players lower trajectory in headwinds, raise in tailwinds
        Vector3 wind = env.GetWindVector();
        float headwindComponent = -wind.z; // Negative = tailwind, positive = headwind
        
        // Adjust angle based on wind and player skill
        float adjustment = headwindComponent * 0.5f * profile.windAdaptability;
        return Mathf.Clamp(baseAngle + adjustment, 0f, 45f);
    }
    
    public void SaveData(string filename)
    {
        // Convert dictionary to serializable format
        var profileList = clubProfiles.Values.ToList();
        string json = JsonUtility.ToJson(new SerializableProfileList { profiles = profileList });
        System.IO.File.WriteAllText(Application.persistentDataPath + "/" + filename, json);
        Debug.Log($"Data saved to {Application.persistentDataPath}/{filename}");
        
        // If MongoDB integration is enabled, also sync data
        if (enableMongoDBIntegration && mongoClient != null)
        {
            StartCoroutine(mongoClient.SyncData());
        }
    }
    
    public void LoadData(string filename)
    {
        string path = Application.persistentDataPath + "/" + filename;
        if (System.IO.File.Exists(path))
        {
            string json = System.IO.File.ReadAllText(path);
            var profileList = JsonUtility.FromJson<SerializableProfileList>(json);
            
            // Convert back to dictionary
            clubProfiles.Clear();
            foreach (var profile in profileList.profiles)
            {
                clubProfiles[profile.clubName] = profile;
            }
            
            Debug.Log($"Loaded {clubProfiles.Count} club profiles from {path}");
        }
        else
        {
            Debug.LogWarning($"Save file not found at {path}");
        }
    }
    
    /// <summary>
    /// Sync with MongoDB server and update local profiles
    /// </summary>
    public void SyncWithMongoDB()
    {
        if (!enableMongoDBIntegration || mongoClient == null)
        {
            Debug.LogWarning("MongoDB integration not enabled or client not found");
            return;
        }
        
        StartCoroutine(mongoClient.SyncData());
    }
    
    /// <summary>
    /// Get all available clubs from profiles
    /// </summary>
    public List<string> GetAvailableClubs()
    {
        return clubProfiles.Keys.ToList();
    }
    
    /// <summary>
    /// Get all available player IDs
    /// </summary>
    public List<string> GetAllPlayers()
    {
        return indoorData.Select(d => d.playerId).Distinct().ToList();
    }
    
    /// <summary>
    /// Export shot data to CSV
    /// </summary>
    public void ExportToCSV(string filePath)
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
            csv.AppendLine("player_id,timestamp,club_type,ball_speed,launch_angle,launch_direction,total_spin,spin_axis,clubhead_speed,smash_factor,attack_angle,club_path,face_angle,dynamic_loft,indoor_carry_distance");
            
            // Add data rows
            foreach (var data in indoorData)
            {
                csv.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14}",
                    data.playerId,
                    data.timestamp.ToString("o"),
                    data.clubType,
                    data.ballSpeed,
                    data.launchAngle,
                    data.launchDirection,
                    data.totalSpin,
                    data.spinAxis,
                    data.clubheadSpeed,
                    data.smashFactor,
                    data.attackAngle,
                    data.clubPath,
                    data.faceAngle,
                    data.dynamicLoft,
                    data.indoorCarryDistance
                ));
            }
            
            // Write to file
            File.WriteAllText(filePath, csv.ToString());
            
            Debug.Log($"Exported {indoorData.Count} shots to {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error exporting CSV: {ex.Message}");
        }
    }
}

// Helper class for serialization
[System.Serializable]
public class SerializableProfileList
{
    public List<ClubPerformanceProfile> profiles;
}

// Extension for ShotDataManagerExtended
public static class ShotDataManagerExtensions
{
    public static bool HasShot(this ShotDataManager manager, string playerId, DateTime timestamp)
    {
        // Check if this shot already exists in the manager
        var shots = manager.GetAllShots();
        return shots.Any(s => s.player_id == playerId && 
                          DateTime.Parse(s.timestamp).ToString("o") == timestamp.ToString("o"));
    }
    
    // Event for when shot data is added
    public static System.Action<IndoorLaunchData> OnShotDataAdded;
}

// Placeholder for AI system
public class AIGolfBrain : MonoBehaviour
{
    // AI implementation will go here
}