using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class GolfBallController : MonoBehaviour
{
    [Header("Ball Physics")]
    public float mass = 0.045f; // Standard golf ball mass in kg
    public float radius = 0.021f; // Standard golf ball radius in meters
    public float airDensity = 1.225f; // Air density at sea level
    public float dragCoefficient = 0.47f; // Sphere drag coefficient
    public float magnusCoefficient = 0.25f; // Magnus effect coefficient
    
    [Header("Surface Interaction")]
    public LayerMask terrainLayer = 1;
    public float groundCheckDistance = 0.1f;
    public float minimumBounceVelocity = 0.5f;
    public float rollingFriction = 0.02f;
    public float airResistance = 0.001f;
    
    [Header("Audio & Effects")]
    public AudioSource audioSource;
    public ParticleSystem bounceParticles;
    public ParticleSystem rollParticles;
    public TrailRenderer ballTrail;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool showTrajectory = true;
    
    private Rigidbody rb;
    private SphereCollider ballCollider;
    private GolfTerrainManager terrainManager;
    private Vector3 lastVelocity;
    private Vector3 angularVelocity;
    private bool isGrounded;
    private bool isRolling;
    private SurfaceType currentSurface = SurfaceType.Grass;
    private SurfaceProperties currentSurfaceProps;
    
    // Ball state tracking
    private float totalDistance;
    private Vector3 startPosition;
    private float flightTime;
    private float maxHeight;
    
    void Start()
    {
        InitializeBall();
        FindTerrainManager();
        startPosition = transform.position;
    }
    
    void InitializeBall()
    {
        rb = GetComponent<Rigidbody>();
        ballCollider = GetComponent<SphereCollider>();
        
        // Set ball properties
        rb.mass = mass;
        rb.linearDamping = 0f; // We'll handle air resistance manually
        rb.angularDamping = 0f; // We'll handle spin decay manually
        
        ballCollider.radius = radius;
        ballCollider.material = CreateBallPhysicsMaterial();
        
        // Initialize audio source
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Initialize trail renderer
        if (ballTrail == null)
            ballTrail = GetComponent<TrailRenderer>();
    }
    
    PhysicsMaterial CreateBallPhysicsMaterial()
    {
        PhysicsMaterial ballMaterial = new PhysicsMaterial("Golf Ball Material");
        ballMaterial.dynamicFriction = 0.3f;
        ballMaterial.staticFriction = 0.3f;
        ballMaterial.bounciness = 0.6f;
        ballMaterial.frictionCombine = PhysicsMaterialCombine.Average;
        ballMaterial.bounceCombine = PhysicsMaterialCombine.Average;
        return ballMaterial;
    }
    
    void FindTerrainManager()
    {
        terrainManager = FindFirstObjectByType<GolfTerrainManager>();
        if (terrainManager == null)
        {
            Debug.LogWarning("GolfTerrainManager not found! Surface detection will not work.");
        }
    }
    
    void Update()
    {
        UpdateBallState();
        UpdateSurfaceDetection();
        UpdateEffects();
        
        if (showDebugInfo)
            DisplayDebugInfo();
    }
    
    void FixedUpdate()
    {
        ApplyPhysicsForces();
        UpdateBallStatistics();
    }
    
    void UpdateBallState()
    {
        lastVelocity = rb.linearVelocity;
        isGrounded = CheckGrounded();
        isRolling = isGrounded && rb.linearVelocity.magnitude < 5f;
        
        // Update flight time
        if (!isGrounded)
            flightTime += Time.deltaTime;
            
        // Track max height
        if (transform.position.y > maxHeight)
            maxHeight = transform.position.y;
    }
    
    bool CheckGrounded()
    {
        RaycastHit hit;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.01f;
        
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, groundCheckDistance + 0.01f, terrainLayer))
        {
            return hit.distance <= groundCheckDistance;
        }
        
        return false;
    }
    
    void UpdateSurfaceDetection()
    {
        if (terrainManager != null && isGrounded)
        {
            SurfaceType newSurface = terrainManager.GetSurfaceTypeAtPosition(transform.position);
            
            if (newSurface != currentSurface)
            {
                currentSurface = newSurface;
                currentSurfaceProps = terrainManager.GetSurfacePropertiesAtPosition(transform.position);
                OnSurfaceChanged();
            }
        }
    }
    
    void OnSurfaceChanged()
    {
        // Update physics material based on surface
        if (currentSurfaceProps != null)
        {
            PhysicsMaterial material = ballCollider.material;
            material.dynamicFriction = currentSurfaceProps.ballRollResistance * 0.3f;
            material.staticFriction = currentSurfaceProps.ballRollResistance * 0.3f;
            material.bounciness = currentSurfaceProps.ballBounce;
        }
        
        // Play surface-specific sound
        if (currentSurfaceProps?.impactSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(currentSurfaceProps.impactSound, 0.5f);
        }
    }
    
    void ApplyPhysicsForces()
    {
        if (rb.linearVelocity.magnitude > 0.1f)
        {
            ApplyAirResistance();
            ApplyMagnusEffect();
        }
        
        if (isRolling)
        {
            ApplyRollingResistance();
        }
        
        // Apply gravity (Unity handles this automatically, but we can modify it)
        // rb.AddForce(Physics.gravity * rb.mass, ForceMode.Force);
    }
    
    void ApplyAirResistance()
    {
        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;
        
        if (speed > 0.1f)
        {
            // Calculate drag force: F = 0.5 * ρ * v² * Cd * A
            float crossSectionalArea = Mathf.PI * radius * radius;
            float dragMagnitude = 0.5f * airDensity * speed * speed * dragCoefficient * crossSectionalArea;
            
            Vector3 dragForce = -velocity.normalized * dragMagnitude;
            rb.AddForce(dragForce, ForceMode.Force);
        }
    }
    
    void ApplyMagnusEffect()
    {
        Vector3 velocity = rb.linearVelocity;
        Vector3 spin = rb.angularVelocity;
        
        if (velocity.magnitude > 0.1f && spin.magnitude > 0.1f)
        {
            // Magnus force: F = 0.5 * ρ * v * ω × v * Cm * A
            Vector3 magnusDirection = Vector3.Cross(spin, velocity).normalized;
            float magnusMagnitude = 0.5f * airDensity * velocity.magnitude * spin.magnitude * magnusCoefficient * Mathf.PI * radius * radius;
            
            Vector3 magnusForce = magnusDirection * magnusMagnitude;
            rb.AddForce(magnusForce, ForceMode.Force);
        }
    }
    
    void ApplyRollingResistance()
    {
        if (currentSurfaceProps != null)
        {
            Vector3 resistanceForce = -rb.linearVelocity.normalized * rollingFriction * currentSurfaceProps.ballRollResistance;
            rb.AddForce(resistanceForce, ForceMode.Force);
            
            // Apply angular drag
            rb.angularVelocity *= (1f - Time.fixedDeltaTime * 2f);
        }
    }
    
    void UpdateBallStatistics()
    {
        totalDistance += Vector3.Distance(transform.position, startPosition);
        startPosition = transform.position;
    }
    
    void UpdateEffects()
    {
        // Update trail renderer
        if (ballTrail != null)
        {
            ballTrail.enabled = rb.linearVelocity.magnitude > 1f;
        }
        
        // Update roll particles
        if (rollParticles != null)
        {
            if (isRolling && rb.linearVelocity.magnitude > 0.5f)
            {
                if (!rollParticles.isPlaying)
                    rollParticles.Play();
            }
            else if (rollParticles.isPlaying)
            {
                rollParticles.Stop();
            }
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        HandleBallCollision(collision);
    }
    
    void HandleBallCollision(Collision collision)
    {
        if (collision.gameObject.layer == Mathf.Log(terrainLayer.value, 2))
        {
            Vector3 impactVelocity = lastVelocity;
            float impactSpeed = impactVelocity.magnitude;
            
            if (impactSpeed > minimumBounceVelocity)
            {
                // Play bounce sound
                if (currentSurfaceProps?.impactSound != null && audioSource != null)
                {
                    float volume = Mathf.Clamp01(impactSpeed / 10f);
                    audioSource.PlayOneShot(currentSurfaceProps.impactSound, volume);
                }
                
                // Trigger bounce particles
                if (bounceParticles != null)
                {
                    bounceParticles.transform.position = collision.contacts[0].point;
                    bounceParticles.Emit(Mathf.RoundToInt(impactSpeed * 2f));
                }
                
                // Apply spin based on impact angle and surface
                ApplyImpactSpin(collision);
            }
        }
    }
    
    void ApplyImpactSpin(Collision collision)
    {
        ContactPoint contact = collision.contacts[0];
        Vector3 impactNormal = contact.normal;
        Vector3 impactVelocity = lastVelocity;
        
        // Calculate spin based on impact angle
        float impactAngle = Vector3.Angle(impactVelocity, -impactNormal);
        Vector3 spinAxis = Vector3.Cross(impactVelocity, impactNormal).normalized;
        
        if (currentSurfaceProps != null)
        {
            float spinMagnitude = impactVelocity.magnitude * currentSurfaceProps.ballSpin * 0.1f;
            rb.angularVelocity += spinAxis * spinMagnitude;
        }
    }
    
    // Public methods for golf club interaction
    public void HitBall(Vector3 force, Vector3 spin, Vector3 hitPoint)
    {
        // Reset ball statistics
        totalDistance = 0f;
        flightTime = 0f;
        maxHeight = transform.position.y;
        startPosition = transform.position;
        
        // Apply force
        rb.AddForceAtPosition(force, hitPoint, ForceMode.Impulse);
        
        // Apply spin
        rb.angularVelocity = spin;
        
        // Enable trail
        if (ballTrail != null)
            ballTrail.enabled = true;
            
        Debug.Log($"Ball hit with force: {force.magnitude:F1}N, spin: {spin.magnitude:F1}rad/s");
    }
    
    public void StopBall()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        if (ballTrail != null)
            ballTrail.enabled = false;
    }
    
    public void ResetBall(Vector3 position)
    {
        transform.position = position;
        StopBall();
        
        // Reset statistics
        totalDistance = 0f;
        flightTime = 0f;
        maxHeight = position.y;
        startPosition = position;
    }
    
    // Getter methods for ball state
    public float GetBallSpeed() => rb.linearVelocity.magnitude;
    public Vector3 GetBallVelocity() => rb.linearVelocity;
    public Vector3 GetBallSpin() => rb.angularVelocity;
    public bool IsGrounded() => isGrounded;
    public bool IsRolling() => isRolling;
    public SurfaceType GetCurrentSurface() => currentSurface;
    public float GetTotalDistance() => totalDistance;
    public float GetFlightTime() => flightTime;
    public float GetMaxHeight() => maxHeight;
    
    void DisplayDebugInfo()
    {
        if (showDebugInfo)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2f);
            
            string debugText = $"Speed: {GetBallSpeed():F1} m/s\n";
            debugText += $"Surface: {currentSurface}\n";
            debugText += $"Distance: {totalDistance:F1}m\n";
            debugText += $"Flight Time: {flightTime:F1}s\n";
            debugText += $"Max Height: {maxHeight:F1}m\n";
            debugText += $"Grounded: {isGrounded}\n";
            debugText += $"Rolling: {isRolling}";
            
            // You would typically use GUI.Label here in OnGUI() method
            // or use TextMeshPro for better performance
        }
    }
    
    void OnDrawGizmos()
    {
        if (showDebugInfo)
        {
            // Draw velocity vector
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, rb.linearVelocity);
            
            // Draw angular velocity
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, rb.angularVelocity);
            
            // Draw ground check ray
            Gizmos.color = isGrounded ? Color.green : Color.yellow;
            Gizmos.DrawRay(transform.position, Vector3.down * groundCheckDistance);
            
            // Draw surface type indicator
            if (currentSurfaceProps != null)
            {
                Gizmos.color = currentSurfaceProps.debugColor;
                Gizmos.DrawWireSphere(transform.position, radius * 1.5f);
            }
        }
        
        if (showTrajectory && rb.linearVelocity.magnitude > 1f)
        {
            DrawTrajectoryPrediction();
        }
    }
    
    void DrawTrajectoryPrediction()
    {
        Vector3 pos = transform.position;
        Vector3 vel = rb.linearVelocity;
        float timeStep = 0.1f;
        int steps = 50;
        
        Gizmos.color = Color.cyan;
        
        for (int i = 0; i < steps; i++)
        {
            Vector3 nextPos = pos + vel * timeStep;
            nextPos.y += Physics.gravity.y * timeStep * timeStep * 0.5f;
            
            Gizmos.DrawLine(pos, nextPos);
            
            pos = nextPos;
            vel.y += Physics.gravity.y * timeStep;
            
            // Simple air resistance approximation
            vel *= (1f - airResistance * timeStep);
            
            // Stop if trajectory goes below ground
            if (pos.y < 0f) break;
        }
    }
    
    // Event system for UI updates
    public System.Action<float> OnSpeedChangedEvent;
    public System.Action<SurfaceType> OnSurfaceChangedEvent;
    public System.Action<Vector3> OnPositionChangedEvent;
    
    void LateUpdate()
    {
        // Trigger events for UI updates
        OnSpeedChangedEvent?.Invoke(GetBallSpeed());
        OnSurfaceChangedEvent?.Invoke(currentSurface);
        OnPositionChangedEvent?.Invoke(transform.position);
    }
}