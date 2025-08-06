using UnityEngine;
using System.Collections;

[System.Serializable]
public class Player
{
    public float velocity;
    public float angle;
    public float rpm;
    public float sidespin;
    public float temperature_K;
    public float faceAngle; // degrees from target line (+ = open/right, - = closed/left)
    public float attackAngle; // vertical attack angle (+ = down, - = up)
}

[System.Serializable]
public class Ball
{
    public float mass;
    public float radius;
    public Vector3 position;
}

[System.Serializable]
public class ShotData
{
    public Player playerInput;
    public Vector3 startPosition;
    public Vector3 finalPosition;
    public float distanceToTarget;
    public float flightTime;
    public int bounces;
    public float maxHeight;
    public float carryDistance;
    public float totalDistance;
    public float lateralDeviation;
}

[System.Serializable]
public class ShotResult
{
    public Vector3 finalPosition;
    public float carryDistance;
    public float totalDistance;
    public float flightTime;
    public float maxHeight;
    public float lateralDeviation;
    public int bounces;
}

public partial class GolfBallPhysics : MonoBehaviour
{
    // Constants
    private const float SPIN_DECAY_RATE = 0.96f;
    private const float MOLAR_MASS_AIR = 0.02896f;
    private const float SEA_LEVEL_PRESSURE = 101.325f;
    private const float TEMPERATURE_LAPSE_RATE = 0.0065f;
    private const float GAS_CONSTANT = 8.314f;
    private const float GRAVITY = 9.81f;
    private const int MAX_ITERATIONS = 10000;

    [Header("Physics Settings")] 
    public Player player;
    public Ball ball;
    public string surface = "hard";
    public float friction = 0.2f;
    public float dt = 0.01f;
    public LayerMask groundLayer = 1;

    [Header("Debug")] 
    public bool showDebugInfo = true;
    public bool recordTrajectory = true;

    // Events
    public System.Action<Vector3> OnPositionUpdate;
    public System.Action<ShotData> OnShotComplete;
    public System.Action<Vector3, Vector3> OnBounce; // position, velocity
    public System.Action<float> OnHeightUpdate;

    // Environmental variables
    private Vector3 windVector = Vector3.zero;
    private float airDensity = 1.225f; // Default sea level
    private float temperature = 20f; // Celsius
    private float humidity = 50f; // percentage
    private float pressure = 1013f; // hPa

    // Private variables
    private Coroutine simulationCoroutine;
    private ShotData currentShotData;
    private float lastGroundHeight = 0f;
    private int bounceCount = 0;
    private float maxHeight = 0f;
    private Vector3 intendedDirection = Vector3.forward;

    void Awake()
    {
        // Initialize shot data
        currentShotData = new ShotData();
    }

    void Start()
    {
        if (player != null && ball != null)
        {
            StartSimulation();
        }
    }

    public void SetEnvironmentalConditions(EnvironmentalConditions env)
    {
        if (env == null)
        {
            Debug.LogWarning("Environmental conditions are null, using defaults");
            return;
        }

        temperature = env.temperature;
        humidity = env.humidity;
        pressure = env.pressure;
        windVector = env.GetWindVector();
        airDensity = env.GetAirDensity();
        
        // Update player temperature if set
        if (player != null)
        {
            player.temperature_K = temperature + 273.15f;
        }
    }

    public ShotResult SimulateShot(Player shotParams)
    {
        if (shotParams == null)
        {
            Debug.LogError("Shot parameters are null!");
            return null;
        }

        // Stop any existing simulation
        StopSimulation();

        // Set the player parameters
        player = shotParams;
        
        // Run the simulation synchronously
        var result = SimulateBallFlightSync();
        
        return result;
    }

    public void StartSimulation()
    {
        // Input validation
        if (player == null || ball == null)
        {
            Debug.LogError("Player or Ball data is missing!");
            return;
        }
        
        if (player.velocity < 0 || player.velocity > 100)
        {
            Debug.LogWarning($"Velocity {player.velocity} out of reasonable range!");
        }
        
        if (player.angle < -10 || player.angle > 60)
        {
            Debug.LogWarning($"Launch angle {player.angle} out of reasonable range!");
        }

        if (simulationCoroutine != null)
        {
            StopCoroutine(simulationCoroutine);
        }

        // Initialize shot data
        currentShotData.playerInput = player;
        currentShotData.startPosition = ball.position;
        currentShotData.bounces = 0;
        bounceCount = 0;
        maxHeight = ball.position.y;
        
        // Calculate intended direction from initial velocity
        float angleRad = player.angle * Mathf.Deg2Rad;
        intendedDirection = new Vector3(Mathf.Cos(angleRad), 0, 0).normalized;

        simulationCoroutine = StartCoroutine(SimulateBallFlight());
    }

    public void StopSimulation()
    {
        if (simulationCoroutine != null)
        {
            StopCoroutine(simulationCoroutine);
            simulationCoroutine = null;
        }
    }

    private bool CheckGroundCollision(Vector3 currentPos, Vector3 velocity, out RaycastHit hit)
    {
        Vector3 rayStart = currentPos;
        Vector3 rayDirection = velocity.normalized;
        float rayDistance = velocity.magnitude * dt + ball.radius;

        return Physics.SphereCast(rayStart, ball.radius, rayDirection, out hit, rayDistance, groundLayer);
    }

    private void HandleGroundCollisionWithNormal(Vector3 pos, Vector3 groundNormal, ref float vx, ref float vy, ref float vz,
        ref float rpm, ref float sidespin)
    {
        bounceCount++;

        // Calculate coefficient of restitution based on surface
        float CoR = 0.9f; // Default for hard surfaces

        switch (surface.ToLower())
        {
            case "hard":
                CoR = 0.9f;
                break;
            case "soft":
                CoR = 0.675f;
                break;
            case "sand":
                CoR = 0.05f; // Very low bounce for sand
                break;
            default:
                CoR = 0.9f;
                break;
        }

        // Calculate bounce based on surface normal
        Vector3 velocity = new Vector3(vx, vy, vz);
        Vector3 normalVelocity = Vector3.Project(velocity, groundNormal);
        Vector3 tangentialVelocity = velocity - normalVelocity;

        // Check if this is a low bounce that should start rolling
        float bounceApex = (normalVelocity.magnitude * normalVelocity.magnitude) / (2 * GRAVITY);

        // For sand, immediately start rolling
        if (surface.ToLower() == "sand" || bounceApex <= ball.radius)
        {
            // Start rolling physics
            velocity = tangentialVelocity;
            vy = 0;

            // Apply rolling resistance - higher for sand
            float rollingResistance = ball.mass * GRAVITY * (surface.ToLower() == "sand" ? 0.4f : friction);
            float rollAcceleration = -(rollingResistance / ball.mass);

            vx += rollAcceleration * Mathf.Sign(vx) * dt;
            vz += rollAcceleration * Mathf.Sign(vz) * dt;

            // Apply spin effects on rolling
            float spinEffect = ball.radius * 2 * Mathf.PI * rpm / 60;
            vx += spinEffect * dt;

            if (Mathf.Abs(vx) < 0.1f && Mathf.Abs(vz) < 0.1f)
            {
                vx = 0;
                vz = 0;
            }
        }
        else
        {
            // Apply bounce physics
            velocity = tangentialVelocity * ((CoR + friction) / 2) - normalVelocity * CoR;

            vx = velocity.x;
            vy = velocity.y;
            vz = velocity.z;

            // Update spin based on surface interaction
            rpm *= ((CoR + friction) / 2);
            sidespin *= ((CoR + friction) / 2);
        }

        // Fire bounce event
        OnBounce?.Invoke(pos, new Vector3(vx, vy, vz));
    }

    private void HandleGroundCollision(RaycastHit groundHit, ref Vector3 pos, ref float vx, ref float vy, ref float vz,
        ref float rpm, ref float sidespin)
    {
        // Set position to ground contact point
        pos = groundHit.point + groundHit.normal * ball.radius;
        
        // Use the normal from the hit
        HandleGroundCollisionWithNormal(pos, groundHit.normal, ref vx, ref vy, ref vz, ref rpm, ref sidespin);
    }

    private ShotResult SimulateBallFlightSync()
    {
        float v = player.velocity;
        float angleDeg = player.angle;
        float rpm = player.rpm;
        float sidespin = player.sidespin;
        float temp = player.temperature_K;

        float m = ball.mass;
        float radius = ball.radius;

        Vector3 pos = ball.position;
        Vector3 startPos = pos;

        // Physical constants
        float CS_area = Mathf.PI * radius * radius;

        // Initial velocity components with 3D launch
        // Start with launch angle in vertical plane
        float vx = v * Mathf.Cos(angleDeg * Mathf.Deg2Rad);
        float vy = v * Mathf.Sin(angleDeg * Mathf.Deg2Rad);
        float vz = 0;

        // Apply face angle to initial velocity
        // Face angle affects the horizontal launch direction
        if (player.faceAngle != 0)
        {
            float faceAngleRad = player.faceAngle * Mathf.Deg2Rad;
            float horizontalSpeed = Mathf.Sqrt(vx * vx + vz * vz);
            
            // Rotate the horizontal velocity components by face angle
            float newVx = horizontalSpeed * Mathf.Cos(faceAngleRad);
            float newVz = horizontalSpeed * Mathf.Sin(faceAngleRad);
            
            vx = newVx;
            vz = newVz;
            
            // Face angle also affects spin axis
            // Open face adds more sidespin
            sidespin += rpm * Mathf.Sin(faceAngleRad) * 0.5f;
        }

        // Apply face angle to initial velocity
        // Face angle affects the horizontal launch direction
        if (player.faceAngle != 0)
        {
            float faceAngleRad = player.faceAngle * Mathf.Deg2Rad;
            float horizontalSpeed = Mathf.Sqrt(vx * vx + vz * vz);
            
            // Rotate the horizontal velocity components by face angle
            float newVx = horizontalSpeed * Mathf.Cos(faceAngleRad);
            float newVz = horizontalSpeed * Mathf.Sin(faceAngleRad);
            
            vx = newVx;
            vz = newVz;
            
            // Face angle also affects spin axis
            // Open face adds more sidespin
            sidespin += rpm * Mathf.Sin(faceAngleRad) * 0.5f;
        }

        float t = 0;
        Vector3 carryPosition = Vector3.zero;
        bool isCarrying = true;
        float localMaxHeight = 0f;
        int localBounceCount = 0;
        int iterations = 0;

        while ((vx > 0.01f || Mathf.Abs(vz) > 0.01f || vy > 0) && iterations < MAX_ITERATIONS)
        {
            iterations++;
            
            // Calculate atmospheric conditions
            float altitudeFactor = 1 - TEMPERATURE_LAPSE_RATE * pos.y / temp;
            float p = SEA_LEVEL_PRESSURE * Mathf.Pow(altitudeFactor, (GRAVITY * MOLAR_MASS_AIR / (GAS_CONSTANT * TEMPERATURE_LAPSE_RATE)));
            float rho = airDensity;

            // Calculate Reynolds number and drag coefficient
            float viscosity = Mathf.Pow(temp / 273, 1.5f) * ((273 + 111) / (temp + 111));
            float currentV = Mathf.Sqrt(vx * vx + vy * vy + vz * vz);
            float Re = currentV * 0.04267f / viscosity;

            float Cd = Re < 7.5e4f
                ? 1.29e-10f * Re * Re - 2.59e-5f * Re + 1.50f
                : 1.91e-11f * Re * Re - 5.40e-6f * Re + 0.56f;

            // Spin decay
            rpm *= Mathf.Pow(SPIN_DECAY_RATE, dt);
            sidespin *= Mathf.Pow(SPIN_DECAY_RATE, dt);

            // Magnus effect calculations
            float S = currentV > 0 ? 2 * Mathf.PI * rpm / 60 * radius / currentV : 0;
            float Sz = currentV > 0 ? 2 * Mathf.PI * sidespin / 60 * radius / currentV : 0;
            float Cl = -3.25f * S * S + 1.99f * S;
            float Clz = -3.25f * Sz * Sz + 1.99f * Sz;

            // Force calculations
            float dragForce = Cd * CS_area * rho * currentV * currentV / (2 * m);
            float liftForce = Cl * rho * CS_area * currentV * currentV / (2 * m);
            float sideForce = Clz * rho * CS_area * currentV * currentV / (2 * m);

            // Add wind effects to velocity components
            float windEffectX = windVector.x * 0.05f;
            float windEffectY = windVector.y * 0.02f;
            float windEffectZ = windVector.z * 0.05f;

            // Acceleration components
            float ax = currentV > 0 ? -dragForce * (vx / currentV) + windEffectX : 0;
            float ay = -GRAVITY + (vy > 0 ? liftForce : 0) - (currentV > 0 ? dragForce * (vy / currentV) : 0) + windEffectY;
            float az = sideForce - (currentV > 0 ? dragForce * (vz / currentV) : 0) + windEffectZ;

            // Update velocities
            vx += ax * dt;
            vy += ay * dt;
            vz += az * dt;

            // Update position
            pos.x += vx * dt;
            pos.y += vy * dt;
            pos.z += vz * dt;

            t += dt;

            // Track maximum height
            if (pos.y > localMaxHeight)
            {
                localMaxHeight = pos.y;
            }

            // Ground collision detection
            RaycastHit groundHit;
            if (pos.y <= 0 && vy < 0)
            {
                if (isCarrying)
                {
                    carryPosition = pos;
                    isCarrying = false;
                }

                pos.y = 0;
                Vector3 groundNormal = Vector3.up;
                HandleGroundCollisionWithNormal(pos, groundNormal, ref vx, ref vy, ref vz, ref rpm, ref sidespin);
                localBounceCount++;
            }
            else if (CheckGroundCollision(pos, new Vector3(vx, vy, vz), out groundHit))
            {
                if (isCarrying)
                {
                    carryPosition = pos;
                    isCarrying = false;
                }

                HandleGroundCollision(groundHit, ref pos, ref vx, ref vy, ref vz, ref rpm, ref sidespin);
                localBounceCount++;
            }

            // Check if ball has stopped
            if (Mathf.Abs(vx) < 0.01f && Mathf.Abs(vz) < 0.01f && vy <= 0 && pos.y <= 0.1f)
            {
                break;
            }
        }

        // If ball is still carrying when simulation ends
        if (isCarrying)
        {
            carryPosition = pos;
        }

        // Calculate distances
        float carryDist = Vector3.Distance(
            new Vector3(startPos.x, 0, startPos.z),
            new Vector3(carryPosition.x, 0, carryPosition.z)
        );

        float totalDist = Vector3.Distance(
            new Vector3(startPos.x, 0, startPos.z),
            new Vector3(pos.x, 0, pos.z)
        );

        // Calculate lateral deviation
        Vector3 targetLine = new Vector3(intendedDirection.x, 0, intendedDirection.z).normalized;
        Vector3 actualLine = new Vector3(pos.x - startPos.x, 0, pos.z - startPos.z);
        float lateralDev = Vector3.Cross(targetLine, actualLine).magnitude;

        // Create result
        return new ShotResult
        {
            finalPosition = pos,
            carryDistance = carryDist,
            totalDistance = totalDist,
            flightTime = t,
            maxHeight = localMaxHeight,
            lateralDeviation = lateralDev,
            bounces = localBounceCount
        };
    }

    IEnumerator SimulateBallFlight()
    {
        float v = player.velocity;
        float angleDeg = player.angle;
        float rpm = player.rpm;
        float sidespin = player.sidespin;
        float temp = player.temperature_K;

        float m = ball.mass;
        float radius = ball.radius;

        Vector3 pos = ball.position;

        // Physical constants
        float CS_area = Mathf.PI * radius * radius;

        // Initial velocity components with 3D launch
        // Start with launch angle in vertical plane
        float vx = v * Mathf.Cos(angleDeg * Mathf.Deg2Rad);
        float vy = v * Mathf.Sin(angleDeg * Mathf.Deg2Rad);
        float vz = 0;

        // Apply face angle to initial velocity
        // Face angle affects the horizontal launch direction
        if (player.faceAngle != 0)
        {
            float faceAngleRad = player.faceAngle * Mathf.Deg2Rad;
            float horizontalSpeed = Mathf.Sqrt(vx * vx + vz * vz);
            
            // Rotate the horizontal velocity components by face angle
            float newVx = horizontalSpeed * Mathf.Cos(faceAngleRad);
            float newVz = horizontalSpeed * Mathf.Sin(faceAngleRad);
            
            vx = newVx;
            vz = newVz;
            
            // Face angle also affects spin axis
            // Open face adds more sidespin
            sidespin += rpm * Mathf.Sin(faceAngleRad) * 0.5f;
        }

        float t = 0;
        Vector3 carryPosition = Vector3.zero;
        bool isCarrying = true;
        int iterations = 0;

        while ((vx > 0.01f || Mathf.Abs(vz) > 0.01f || vy > 0) && iterations < MAX_ITERATIONS)
        {
            iterations++;
            
            // Calculate atmospheric conditions
            float altitudeFactor = 1 - TEMPERATURE_LAPSE_RATE * pos.y / temp;
            float p = SEA_LEVEL_PRESSURE * Mathf.Pow(altitudeFactor, (GRAVITY * MOLAR_MASS_AIR / (GAS_CONSTANT * TEMPERATURE_LAPSE_RATE)));
            float rho = airDensity;

            // Calculate Reynolds number and drag coefficient
            float viscosity = Mathf.Pow(temp / 273, 1.5f) * ((273 + 111) / (temp + 111));
            float currentV = Mathf.Sqrt(vx * vx + vy * vy + vz * vz);
            float Re = currentV * 0.04267f / viscosity;

            float Cd = Re < 7.5e4f
                ? 1.29e-10f * Re * Re - 2.59e-5f * Re + 1.50f
                : 1.91e-11f * Re * Re - 5.40e-6f * Re + 0.56f;

            // Spin decay
            rpm *= Mathf.Pow(SPIN_DECAY_RATE, dt);
            sidespin *= Mathf.Pow(SPIN_DECAY_RATE, dt);

            // Magnus effect calculations
            float S = currentV > 0 ? 2 * Mathf.PI * rpm / 60 * radius / currentV : 0;
            float Sz = currentV > 0 ? 2 * Mathf.PI * sidespin / 60 * radius / currentV : 0;
            float Cl = -3.25f * S * S + 1.99f * S;
            float Clz = -3.25f * Sz * Sz + 1.99f * Sz;

            // Force calculations
            float dragForce = Cd * CS_area * rho * currentV * currentV / (2 * m);
            float liftForce = Cl * rho * CS_area * currentV * currentV / (2 * m);
            float sideForce = Clz * rho * CS_area * currentV * currentV / (2 * m);

            // Add wind effects to velocity components
            float windEffectX = windVector.x * 0.05f;
            float windEffectY = windVector.y * 0.02f;
            float windEffectZ = windVector.z * 0.05f;

            // Acceleration components
            float ax = currentV > 0 ? -dragForce * (vx / currentV) + windEffectX : 0;
            float ay = -GRAVITY + (vy > 0 ? liftForce : 0) - (currentV > 0 ? dragForce * (vy / currentV) : 0) + windEffectY;
            float az = sideForce - (currentV > 0 ? dragForce * (vz / currentV) : 0) + windEffectZ;

            // Update velocities
            vx += ax * dt;
            vy += ay * dt;
            vz += az * dt;

            // Update position
            pos.x += vx * dt;
            pos.y += vy * dt;
            pos.z += vz * dt;

            t += dt;

            // Track maximum height
            if (pos.y > maxHeight)
            {
                maxHeight = pos.y;
                OnHeightUpdate?.Invoke(maxHeight);
            }

            // Ground collision detection
            RaycastHit groundHit;
            if (pos.y <= 0 && vy < 0)
            {
                if (isCarrying)
                {
                    carryPosition = pos;
                    isCarrying = false;
                }

                pos.y = 0;
                Vector3 groundNormal = Vector3.up;
                HandleGroundCollisionWithNormal(pos, groundNormal, ref vx, ref vy, ref vz, ref rpm, ref sidespin);
            }
            else if (CheckGroundCollision(pos, new Vector3(vx, vy, vz), out groundHit))
            {
                if (isCarrying)
                {
                    carryPosition = pos;
                    isCarrying = false;
                }

                HandleGroundCollision(groundHit, ref pos, ref vx, ref vy, ref vz, ref rpm, ref sidespin);
            }

            // Update transform and fire events
            transform.position = pos;
            OnPositionUpdate?.Invoke(pos);

            // Check if ball has stopped
            if (Mathf.Abs(vx) < 0.01f && Mathf.Abs(vz) < 0.01f && vy <= 0 && pos.y <= 0.1f)
            {
                break;
            }

            yield return new WaitForSeconds(dt);
        }

        // Finalize shot data
        currentShotData.finalPosition = pos;
        currentShotData.flightTime = t;
        currentShotData.bounces = bounceCount;
        currentShotData.maxHeight = maxHeight;
        currentShotData.distanceToTarget = Vector3.Distance(currentShotData.startPosition, pos);

        // Calculate carry and total distance properly
        currentShotData.carryDistance = Vector3.Distance(
            new Vector3(currentShotData.startPosition.x, 0, currentShotData.startPosition.z),
            new Vector3(carryPosition.x, 0, carryPosition.z)
        );

        currentShotData.totalDistance = Vector3.Distance(
            new Vector3(currentShotData.startPosition.x, 0, currentShotData.startPosition.z),
            new Vector3(pos.x, 0, pos.z)
        );

        // Calculate lateral deviation
        Vector3 targetLine = new Vector3(intendedDirection.x, 0, intendedDirection.z).normalized;
        Vector3 actualLine = new Vector3(pos.x - currentShotData.startPosition.x, 0, pos.z - currentShotData.startPosition.z);
        currentShotData.lateralDeviation = Vector3.Cross(targetLine, actualLine).magnitude;

        // Fire completion event
        OnShotComplete?.Invoke(currentShotData);
    }
}