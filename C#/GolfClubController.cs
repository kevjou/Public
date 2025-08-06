using UnityEngine;
using System.Collections;

public enum ClubType
{
    Driver,
    Wood3,
    Wood5,
    Iron3,
    Iron4,
    Iron5,
    Iron6,
    Iron7,
    Iron8,
    Iron9,
    PitchingWedge,
    SandWedge,
    Putter
}

[System.Serializable]
public class ClubProperties
{
    public ClubType clubType;
    public float maxDistance = 250f;
    public float loft = 10f; // Degrees
    public float sweetSpotSize = 0.1f;
    public float accuracy = 0.9f;
    public float minPower = 0.1f;
    public float maxPower = 1.0f;
    public AnimationCurve powerCurve = AnimationCurve.Linear(0, 0, 1, 1);
    public AudioClip swingSound;
    public AudioClip impactSound;
    public GameObject clubModel;
}

public class GolfClubController : MonoBehaviour
{
    [Header("Club Setup")]
    public ClubProperties[] clubs;
    public int selectedClubIndex = 0;
    public Transform clubPosition;
    public Transform ballPosition;
    
    [Header("Swing Mechanics")]
    public float swingSpeed = 5f;
    public float maxSwingPower = 1000f;
    public float aimSensitivity = 50f;
    public float powerBuildRate = 2f;
    public float sweetSpotTolerance = 0.1f;
    
    [Header("Input Settings")]
    public KeyCode swingKey = KeyCode.Space;
    public KeyCode aimLeftKey = KeyCode.A;
    public KeyCode aimRightKey = KeyCode.D;
    public KeyCode clubUpKey = KeyCode.Q;
    public KeyCode clubDownKey = KeyCode.E;
    
    [Header("Visual Elements")]
    public LineRenderer trajectoryLine;
    public Transform aimIndicator;
    public GameObject powerMeter;
    public Camera playerCamera;
    
    [Header("Audio")]
    public AudioSource audioSource;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool showAimLine = true;
    public bool enableMouseAiming = true;
    
    private GolfBallController ballController;
    private ClubProperties currentClub;
    private GameObject currentClubModel;
    
    // Swing state
    private bool isAiming = false;
    private bool isSwinging = false;
    private bool isPowerBuilding = false;
    private float currentPower = 0f;
    private float aimDirection = 0f;
    private float swingTimer = 0f;
    
    // Aiming
    private Vector3 aimTarget;
    private float aimDistance = 100f;
    
    // UI Elements
    private PowerMeterUI powerMeterUI;
    
    void Start()
    {
        InitializeClub();
        FindBallController();
        SetupUI();
        SelectClub(selectedClubIndex);
    }
    
    void InitializeClub()
    {
        if (clubs == null || clubs.Length == 0)
        {
            CreateDefaultClubs();
        }
        
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        if (trajectoryLine == null)
        {
            GameObject lineObj = new GameObject("Trajectory Line");
            lineObj.transform.SetParent(transform);
            trajectoryLine = lineObj.AddComponent<LineRenderer>();
            SetupTrajectoryLine();
        }
    }
    
    void CreateDefaultClubs()
    {
        clubs = new ClubProperties[13];
        
        // Create default club properties
        clubs[0] = new ClubProperties { clubType = ClubType.Driver, maxDistance = 250f, loft = 10f, accuracy = 0.7f };
        clubs[1] = new ClubProperties { clubType = ClubType.Wood3, maxDistance = 220f, loft = 15f, accuracy = 0.75f };
        clubs[2] = new ClubProperties { clubType = ClubType.Wood5, maxDistance = 200f, loft = 18f, accuracy = 0.8f };
        clubs[3] = new ClubProperties { clubType = ClubType.Iron3, maxDistance = 180f, loft = 21f, accuracy = 0.8f };
        clubs[4] = new ClubProperties { clubType = ClubType.Iron4, maxDistance = 170f, loft = 24f, accuracy = 0.82f };
        clubs[5] = new ClubProperties { clubType = ClubType.Iron5, maxDistance = 160f, loft = 27f, accuracy = 0.84f };
        clubs[6] = new ClubProperties { clubType = ClubType.Iron6, maxDistance = 150f, loft = 30f, accuracy = 0.86f };
        clubs[7] = new ClubProperties { clubType = ClubType.Iron7, maxDistance = 140f, loft = 34f, accuracy = 0.88f };
        clubs[8] = new ClubProperties { clubType = ClubType.Iron8, maxDistance = 130f, loft = 38f, accuracy = 0.9f };
        clubs[9] = new ClubProperties { clubType = ClubType.Iron9, maxDistance = 120f, loft = 42f, accuracy = 0.92f };
        clubs[10] = new ClubProperties { clubType = ClubType.PitchingWedge, maxDistance = 100f, loft = 46f, accuracy = 0.94f };
        clubs[11] = new ClubProperties { clubType = ClubType.SandWedge, maxDistance = 80f, loft = 56f, accuracy = 0.95f };
        clubs[12] = new ClubProperties { clubType = ClubType.Putter, maxDistance = 30f, loft = 3f, accuracy = 0.98f };
    }
    
    void FindBallController()
    {
        ballController = FindFirstObjectByType<GolfBallController>();
        if (ballController == null)
        {
            Debug.LogError("GolfBallController not found!");
        }
    }
    
    void SetupUI()
    {
        // Create power meter UI if not assigned
        if (powerMeter == null)
        {
            powerMeter = new GameObject("Power Meter");
            powerMeter.transform.SetParent(transform);
            powerMeterUI = powerMeter.AddComponent<PowerMeterUI>();
        }
        else
        {
            powerMeterUI = powerMeter.GetComponent<PowerMeterUI>();
        }
    }
    
    void SetupTrajectoryLine()
    {
        trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
        trajectoryLine.material.color = Color.yellow;
        trajectoryLine.startWidth = 0.1f;
        trajectoryLine.endWidth = 0.05f;
        trajectoryLine.positionCount = 0;
        trajectoryLine.useWorldSpace = true;
    }
    
    void Update()
    {
        HandleInput();
        UpdateAiming();
        UpdateSwing();
        UpdateUI();
        
        if (showDebugInfo)
            DisplayDebugInfo();
    }
    
    void HandleInput()
    {
        if (ballController == null || ballController.GetBallSpeed() > 0.5f)
            return;
        
        // Club selection
        if (Input.GetKeyDown(clubUpKey))
        {
            SelectClub(Mathf.Max(0, selectedClubIndex - 1));
        }
        else if (Input.GetKeyDown(clubDownKey))
        {
            SelectClub(Mathf.Min(clubs.Length - 1, selectedClubIndex + 1));
        }
        
        // Aiming
        if (Input.GetKey(aimLeftKey))
        {
            aimDirection -= aimSensitivity * Time.deltaTime;
        }
        else if (Input.GetKey(aimRightKey))
        {
            aimDirection += aimSensitivity * Time.deltaTime;
        }
        
        // Mouse aiming
        if (enableMouseAiming)
        {
            HandleMouseAiming();
        }
        
        // Swing control
        if (Input.GetKeyDown(swingKey) && !isSwinging)
        {
            StartSwing();
        }
        else if (Input.GetKeyUp(swingKey) && isSwinging)
        {
            ExecuteSwing();
        }
    }
    
    void HandleMouseAiming()
    {
        if (Input.GetMouseButton(0))
        {
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit))
            {
                Vector3 targetDirection = hit.point - ballController.transform.position;
                targetDirection.y = 0;
                aimDirection = Mathf.Atan2(targetDirection.x, targetDirection.z) * Mathf.Rad2Deg;
                aimDistance = targetDirection.magnitude;
            }
        }
    }
    
    void UpdateAiming()
    {
        if (ballController == null) return;
        
        Vector3 ballPos = ballController.transform.position;
        Vector3 aimDir = new Vector3(Mathf.Sin(aimDirection * Mathf.Deg2Rad), 0, Mathf.Cos(aimDirection * Mathf.Deg2Rad));
        aimTarget = ballPos + aimDir * aimDistance;
        
        // Update aim indicator
        if (aimIndicator != null)
        {
            aimIndicator.position = aimTarget;
            aimIndicator.LookAt(ballPos);
        }
        
        // Update trajectory prediction
        if (showAimLine && !isSwinging)
        {
            UpdateTrajectoryPrediction();
        }
    }
    
    void UpdateTrajectoryPrediction()
    {
        if (trajectoryLine == null || ballController == null) return;
        
        Vector3 ballPos = ballController.transform.position;
        Vector3 aimDir = (aimTarget - ballPos).normalized;
        
        // Calculate estimated shot parameters
        float estimatedPower = currentPower * maxSwingPower;
        float loft = currentClub.loft * Mathf.Deg2Rad;
        float distance = currentClub.maxDistance * currentPower;
        
        // Generate trajectory points
        Vector3[] trajectoryPoints = CalculateTrajectory(ballPos, aimDir, estimatedPower, loft, 50);
        
        trajectoryLine.positionCount = trajectoryPoints.Length;
        trajectoryLine.SetPositions(trajectoryPoints);
        trajectoryLine.enabled = true;
    }
    
    Vector3[] CalculateTrajectory(Vector3 startPos, Vector3 direction, float power, float loft, int points)
    {
        Vector3[] trajectory = new Vector3[points];
        
        float timeStep = 0.1f;
        Vector3 velocity = direction * power * 0.1f;
        velocity.y = Mathf.Sin(loft) * power * 0.1f;
        
        Vector3 position = startPos;
        
        for (int i = 0; i < points; i++)
        {
            trajectory[i] = position;
            
            // Update position
            position += velocity * timeStep;
            
            // Apply gravity
            velocity.y += Physics.gravity.y * timeStep;
            
            // Simple air resistance
            velocity *= (1f - 0.001f * timeStep);
            
            // Stop if trajectory goes below ground
            if (position.y < startPos.y - 10f) break;
        }
        
        return trajectory;
    }
    
    void UpdateSwing()
    {
        if (isSwinging)
        {
            swingTimer += Time.deltaTime;
            
            if (isPowerBuilding)
            {
                currentPower = Mathf.PingPong(swingTimer * powerBuildRate, 1f);
            }
            
            // Auto-execute swing after certain time if not released
            if (swingTimer > 3f)
            {
                ExecuteSwing();
            }
        }
    }
    
    void StartSwing()
    {
        if (ballController == null || currentClub == null) return;
        
        isSwinging = true;
        isPowerBuilding = true;
        swingTimer = 0f;
        currentPower = 0f;
        
        // Play swing sound
        if (audioSource != null && currentClub.swingSound != null)
        {
            audioSource.PlayOneShot(currentClub.swingSound);
        }
        
        // Hide trajectory line during swing
        if (trajectoryLine != null)
            trajectoryLine.enabled = false;
    }
    
    void ExecuteSwing()
    {
        if (!isSwinging || ballController == null || currentClub == null) return;
        
        isSwinging = false;
        isPowerBuilding = false;
        
        // Calculate swing parameters
        Vector3 ballPos = ballController.transform.position;
        Vector3 aimDir = (aimTarget - ballPos).normalized;
        
        // Calculate power with sweet spot consideration
        float finalPower = CalculateFinalPower();
        
        // Calculate force direction with loft
        Vector3 forceDirection = CalculateForceDirection(aimDir);
        
        // Calculate force magnitude
        float forceMagnitude = finalPower * maxSwingPower;
        
        // Apply accuracy modifier
        Vector3 accuracyDeviation = CalculateAccuracyDeviation();
        forceDirection += accuracyDeviation;
        forceDirection.Normalize();
        
        // Calculate final force
        Vector3 finalForce = forceDirection * forceMagnitude;
        
        // Calculate spin
        Vector3 spin = CalculateSpinEffect(aimDir, finalPower);
        
        // Execute the hit
        Vector3 hitPoint = ballPos + Vector3.down * ballController.GetComponent<SphereCollider>().radius;
        ballController.HitBall(finalForce, spin, hitPoint);
        
        // Play impact sound
        if (audioSource != null && currentClub.impactSound != null)
        {
            audioSource.PlayOneShot(currentClub.impactSound);
        }
        
        // Reset swing state
        currentPower = 0f;
        swingTimer = 0f;
        
        Debug.Log($"Swing executed: Power={finalPower:F2}, Force={forceMagnitude:F1}N, Club={currentClub.clubType}");
    }
    
    float CalculateFinalPower()
    {
        float basePower = currentClub.powerCurve.Evaluate(currentPower);
        
        // Apply sweet spot modifier
        float sweetSpotMod = 1f;
        float sweetSpotDistance = Mathf.Abs(currentPower - 0.8f); // Sweet spot at 80% power
        
        if (sweetSpotDistance > currentClub.sweetSpotSize)
        {
            sweetSpotMod = 1f - (sweetSpotDistance - currentClub.sweetSpotSize) * 0.5f;
        }
        
        return Mathf.Clamp(basePower * sweetSpotMod, currentClub.minPower, currentClub.maxPower);
    }
    
    Vector3 CalculateForceDirection(Vector3 aimDirection)
    {
        float loftRadians = currentClub.loft * Mathf.Deg2Rad;
        
        // Create force direction with loft
        Vector3 forceDir = aimDirection;
        forceDir.y = Mathf.Sin(loftRadians);
        forceDir.x *= Mathf.Cos(loftRadians);
        forceDir.z *= Mathf.Cos(loftRadians);
        
        return forceDir.normalized;
    }
    
    Vector3 CalculateAccuracyDeviation()
    {
        float accuracyMod = 1f - currentClub.accuracy;
        float randomDeviation = Random.Range(-accuracyMod, accuracyMod);
        
        return new Vector3(randomDeviation, 0, randomDeviation * 0.5f);
    }
    
    Vector3 CalculateSpinEffect(Vector3 aimDirection, float power)
    {
        Vector3 spin = Vector3.zero;
        
        // Backspin based on loft
        float backspin = currentClub.loft * power * 0.1f;
        spin.x = -backspin;
        
        // Side spin based on accuracy and club face angle
        float sideSpin = Random.Range(-0.5f, 0.5f) * (1f - currentClub.accuracy) * power;
        spin.y = sideSpin;
        
        return spin;
    }
    
    public void SelectClub(int index)
    {
        if (index < 0 || index >= clubs.Length) return;
        
        selectedClubIndex = index;
        currentClub = clubs[index];
        
        // Update club model
        UpdateClubModel();
        
        Debug.Log($"Selected club: {currentClub.clubType} (Distance: {currentClub.maxDistance}m, Loft: {currentClub.loft}°)");
    }
    
    void UpdateClubModel()
    {
        // Remove old club model
        if (currentClubModel != null)
        {
            DestroyImmediate(currentClubModel);
        }
        
        // Instantiate new club model
        if (currentClub.clubModel != null && clubPosition != null)
        {
            currentClubModel = Instantiate(currentClub.clubModel, clubPosition);
        }
    }
    
    void UpdateUI()
    {
        if (powerMeterUI != null)
        {
            powerMeterUI.UpdatePowerMeter(currentPower, isSwinging);
        }
    }
    
    void DisplayDebugInfo()
    {
        // This would typically be implemented in OnGUI or with TextMeshPro
        string debugText = $"Club: {currentClub.clubType}\n";
        debugText += $"Power: {currentPower:F2}\n";
        debugText += $"Aim: {aimDirection:F1}°\n";
        debugText += $"Distance: {aimDistance:F1}m\n";
        debugText += $"Swinging: {isSwinging}";
        
        // Display at top-left of screen
        // You would implement this in OnGUI() method
    }
    
    // Public methods for external control
    public void SetAimDirection(float angle)
    {
        aimDirection = angle;
    }
    
    public void SetAimDistance(float distance)
    {
        aimDistance = Mathf.Clamp(distance, 10f, currentClub.maxDistance);
    }
    
    public ClubType GetCurrentClubType()
    {
        return currentClub.clubType;
    }
    
    public float GetCurrentPower()
    {
        return currentPower;
    }
    
    public bool IsSwinging()
    {
        return isSwinging;
    }
    
    void OnDrawGizmos()
    {
        if (showDebugInfo && ballController != null)
        {
            Vector3 ballPos = ballController.transform.position;
            
            // Draw aim direction
            Gizmos.color = Color.green;
            Vector3 aimDir = new Vector3(Mathf.Sin(aimDirection * Mathf.Deg2Rad), 0, Mathf.Cos(aimDirection * Mathf.Deg2Rad));
            Gizmos.DrawRay(ballPos, aimDir * aimDistance);
            
            // Draw aim target
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(aimTarget, 2f);
            
            // Draw club range indicator
            if (currentClub != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(ballPos, currentClub.maxDistance);
            }
        }
    }
}

// Simple Power Meter UI Component
public class PowerMeterUI : MonoBehaviour
{
    public RectTransform powerBar;
    public UnityEngine.UI.Image powerFill;
    public UnityEngine.UI.Text powerText;
    
    void Start()
    {
        // Create simple UI elements if not assigned
        if (powerBar == null)
        {
            CreatePowerMeterUI();
        }
    }
    
    void CreatePowerMeterUI()
    {
        // This would create a simple power meter UI
        // Implementation would depend on your UI framework
        // For now, this is a placeholder
    }
    
    public void UpdatePowerMeter(float power, bool isActive)
    {
        if (powerFill != null)
        {
            powerFill.fillAmount = power;
            powerFill.color = Color.Lerp(Color.green, Color.red, power);
        }
        
        if (powerText != null)
        {
            powerText.text = $"Power: {power:P0}";
        }
        
        gameObject.SetActive(isActive);
    }
}